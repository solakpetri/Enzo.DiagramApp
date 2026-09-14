namespace Enzo.Diagrams.Domain;

public sealed class BpmnParser
{
    private readonly IReadOnlyList<SyntaxToken> _tokens;
    private readonly List<DiagramSyntaxError> _errors;
    private int _position;

    private BpmnParser(TokenizeResult tokenizeResult)
    {
        _tokens = tokenizeResult.Tokens;
        _errors = [.. tokenizeResult.Errors];
    }

    public static BpmnParseResult Parse(string source)
    {
        var parser = new BpmnParser(DiagramLexer.Tokenize(source));

        return parser.ParseDiagram();
    }

    private BpmnParseResult ParseDiagram()
    {
        SkipEndOfLines();

        var bpmnToken = ExpectKeyword("bpmn", "Expected bpmn declaration.");
        var nameToken = Expect(TokenKind.Identifier, "Expected bpmn name.");
        RequireLineEnd();

        var elements = new List<BpmnElement>();
        var flows = new List<BpmnSequenceFlow>();

        while (Current.Kind != TokenKind.EndOfFile)
        {
            SkipEndOfLines();

            if (Current.Kind == TokenKind.EndOfFile)
            {
                break;
            }

            if (TryReadElement(elements))
            {
                continue;
            }

            if (Current.Kind == TokenKind.Identifier)
            {
                ReadFlow(flows);
                continue;
            }

            AddError($"Unexpected token '{Current.Text}'.");
            SynchronizeLine();
        }

        if (_errors.Count > 0 || bpmnToken is null || nameToken is null)
        {
            return new BpmnParseResult(null, _errors, []);
        }

        var diagram = new BpmnDiagram(nameToken.Text, elements, flows, bpmnToken.Line, bpmnToken.Column);
        return new BpmnParseResult(diagram, _errors, BpmnValidator.Validate(diagram));
    }

    private bool TryReadElement(List<BpmnElement> elements)
    {
        var kind = Current.Kind switch
        {
            TokenKind.Start => BpmnElementKind.StartEvent,
            TokenKind.Task => BpmnElementKind.Task,
            TokenKind.End => BpmnElementKind.EndEvent,
            TokenKind.Identifier when Current.Text == "gateway" => BpmnElementKind.ExclusiveGateway,
            _ => (BpmnElementKind?)null
        };

        if (kind is null)
        {
            return false;
        }

        var errorCount = _errors.Count;
        Advance();

        var idToken = Expect(TokenKind.Identifier, "Expected BPMN element identifier.");
        var labelToken = kind is BpmnElementKind.Task or BpmnElementKind.ExclusiveGateway
            ? Expect(TokenKind.String, "Expected quoted BPMN element label.")
            : null;
        RequireLineEnd();

        if (_errors.Count == errorCount && idToken is not null)
        {
            elements.Add(new BpmnElement(kind.Value, idToken.Text, labelToken?.Text ?? idToken.Text, idToken.Line, idToken.Column));
        }

        return true;
    }

    private void ReadFlow(List<BpmnSequenceFlow> flows)
    {
        var errorCount = _errors.Count;
        var fromToken = Expect(TokenKind.Identifier, "Expected source BPMN element identifier.");
        Expect(TokenKind.Arrow, "Expected '->' in sequence flow declaration.");
        var toToken = Expect(TokenKind.Identifier, "Expected target BPMN element identifier.");
        string? label = null;

        if (Match(TokenKind.Colon))
        {
            if (Current.Kind is TokenKind.Identifier or TokenKind.String)
            {
                label = Current.Text;
                Advance();
            }
            else
            {
                AddError("Expected sequence flow label after ':'.");
            }
        }

        RequireLineEnd();

        if (_errors.Count == errorCount && fromToken is not null && toToken is not null)
        {
            flows.Add(new BpmnSequenceFlow(fromToken.Text, toToken.Text, label, fromToken.Line, fromToken.Column));
        }
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

    private SyntaxToken? ExpectKeyword(string text, string message)
    {
        if (Current.Kind == TokenKind.Identifier && Current.Text == text)
        {
            return Advance();
        }

        AddError(message);
        return null;
    }

    private bool Match(TokenKind kind)
    {
        if (Current.Kind != kind)
        {
            return false;
        }

        Advance();
        return true;
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
