namespace Enzo.Diagrams.Language;

public sealed class SequenceParser
{
    private readonly IReadOnlyList<SyntaxToken> _tokens;
    private readonly List<DiagramSyntaxError> _errors;
    private int _position;

    private SequenceParser(TokenizeResult tokenizeResult)
    {
        _tokens = tokenizeResult.Tokens;
        _errors = [.. tokenizeResult.Errors];
    }

    public static SequenceParseResult Parse(string source)
    {
        var parser = new SequenceParser(DiagramLexer.Tokenize(source));

        return parser.ParseSequence();
    }

    private SequenceParseResult ParseSequence()
    {
        SkipEndOfLines();

        var sequenceToken = Expect(TokenKind.Sequence, "Expected sequence declaration.");
        var nameToken = Expect(TokenKind.Identifier, "Expected sequence name.");
        RequireLineEnd();

        var participants = new List<SequenceParticipant>();
        var messages = new List<SequenceMessage>();

        while (Current.Kind != TokenKind.EndOfFile)
        {
            SkipEndOfLines();

            if (Current.Kind == TokenKind.EndOfFile)
            {
                break;
            }

            if (TryReadParticipant(participants))
            {
                continue;
            }

            if (Current.Kind == TokenKind.Identifier)
            {
                ReadMessage(messages);
                continue;
            }

            AddError($"Unexpected token '{Current.Text}'.");
            SynchronizeLine();
        }

        if (_errors.Count > 0 || sequenceToken is null || nameToken is null)
        {
            return new SequenceParseResult(null, _errors, []);
        }

        var diagram = new SequenceDiagram(nameToken.Text, participants, messages, sequenceToken.Line, sequenceToken.Column);

        return new SequenceParseResult(diagram, _errors, []);
    }

    private bool TryReadParticipant(List<SequenceParticipant> participants)
    {
        var kind = Current.Kind switch
        {
            TokenKind.Actor => SequenceParticipantKind.Actor,
            TokenKind.Participant => SequenceParticipantKind.Participant,
            _ => (SequenceParticipantKind?)null
        };

        if (kind is null)
        {
            return false;
        }

        var errorCount = _errors.Count;
        Advance();

        var idToken = Expect(TokenKind.Identifier, "Expected participant identifier.");
        RequireLineEnd();

        if (_errors.Count == errorCount && idToken is not null)
        {
            participants.Add(new SequenceParticipant(kind.Value, idToken.Text, idToken.Line, idToken.Column));
        }

        return true;
    }

    private void ReadMessage(List<SequenceMessage> messages)
    {
        var errorCount = _errors.Count;
        var fromToken = Expect(TokenKind.Identifier, "Expected source participant identifier.");
        var kind = ReadMessageKind();
        var toToken = Expect(TokenKind.Identifier, "Expected target participant identifier.");
        Expect(TokenKind.Colon, "Expected ':' before message label.");
        var label = ReadMessageLabel();
        RequireLineEnd();

        if (_errors.Count == errorCount && fromToken is not null && toToken is not null && kind is not null && label is not null)
        {
            messages.Add(new SequenceMessage(fromToken.Text, toToken.Text, kind.Value, label, fromToken.Line, fromToken.Column));
        }
    }

    private SequenceMessageKind? ReadMessageKind()
    {
        if (Current.Kind is TokenKind.Arrow or TokenKind.DashedArrow)
        {
            var kind = Current.Kind == TokenKind.Arrow
                ? SequenceMessageKind.Synchronous
                : SequenceMessageKind.Response;
            Advance();

            return kind;
        }

        AddError("Expected '->' or '-->' in message declaration.");
        return null;
    }

    private string? ReadMessageLabel()
    {
        if (Current.Kind is TokenKind.EndOfLine or TokenKind.EndOfFile)
        {
            AddError("Expected message label after ':'.");
            return null;
        }

        var parts = new List<string>();
        while (Current.Kind is not TokenKind.EndOfLine and not TokenKind.EndOfFile)
        {
            if (Current.Kind is not TokenKind.Identifier and not TokenKind.String)
            {
                AddError("Expected message label after ':'.");
                return null;
            }

            parts.Add(Current.Text);
            Advance();
        }

        return string.Join(" ", parts);
    }

    private SyntaxToken? Expect(TokenKind kind, string message)
    {
        if (Current.Kind == kind)
        {
            return Advance();
        }

        AddError(message);

        return null;
    }

    private void RequireLineEnd()
    {
        if (Current.Kind == TokenKind.EndOfFile)
        {
            return;
        }

        if (Current.Kind == TokenKind.EndOfLine)
        {
            SkipEndOfLines();
            return;
        }

        AddError($"Unexpected token '{Current.Text}' at end of line.");
        SynchronizeLine();
    }

    private void SkipEndOfLines()
    {
        while (Current.Kind == TokenKind.EndOfLine)
        {
            Advance();
        }
    }

    private void SynchronizeLine()
    {
        while (Current.Kind is not TokenKind.EndOfLine and not TokenKind.EndOfFile)
        {
            Advance();
        }

        SkipEndOfLines();
    }

    private void AddError(string message)
    {
        _errors.Add(new DiagramSyntaxError(Current.Line, Current.Column, message));
    }

    private SyntaxToken Advance()
    {
        var token = Current;

        if (Current.Kind != TokenKind.EndOfFile)
        {
            _position++;
        }

        return token;
    }

    private SyntaxToken Current => _tokens[_position];
}
