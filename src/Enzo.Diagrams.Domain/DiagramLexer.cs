namespace Enzo.Diagrams.Domain;

public sealed class DiagramLexer
{
    private readonly string _source;
    private readonly bool _readLineTextAfterColon;
    private readonly List<SyntaxToken> _tokens = [];
    private readonly List<DiagramSyntaxError> _errors = [];
    private int _index;
    private int _line = 1;
    private int _column = 1;

    private DiagramLexer(string source, bool readLineTextAfterColon)
    {
        _source = source;
        _readLineTextAfterColon = readLineTextAfterColon;
    }

    public static TokenizeResult Tokenize(string source, bool readLineTextAfterColon = false)
    {
        var lexer = new DiagramLexer(source, readLineTextAfterColon);
        lexer.Tokenize();

        return new TokenizeResult(lexer._tokens, lexer._errors);
    }

    private void Tokenize()
    {
        while (!IsAtEnd)
        {
            var current = Current;
            if (current is ' ' or '\t')
            {
                Advance();
                continue;
            }

            if (current is '\r' or '\n')
            {
                ReadNewLine();
                continue;
            }

            if (IsIdentifierStart(current))
            {
                ReadIdentifierOrKeyword();
                continue;
            }

            if (current == '"')
            {
                ReadString();
                continue;
            }

            if (current == '-' && Peek() == '-' && Peek(2) == '>')
            {
                AddToken(TokenKind.DashedArrow, "-->");
                Advance();
                Advance();
                Advance();
                continue;
            }

            if (current == '-' && Peek() == '>')
            {
                AddToken(TokenKind.Arrow, "->");
                Advance();
                Advance();
                continue;
            }

            if (current == ':')
            {
                AddToken(TokenKind.Colon, ":");
                Advance();

                if (_readLineTextAfterColon)
                {
                    ReadLineText();
                }

                continue;
            }

            _errors.Add(new DiagramSyntaxError(_line, _column, $"Unexpected character '{current}'."));
            Advance();
        }

        _tokens.Add(new SyntaxToken(TokenKind.EndOfFile, string.Empty, _line, _column));
    }

    private void ReadIdentifierOrKeyword()
    {
        var line = _line;
        var column = _column;
        var start = _index;

        while (!IsAtEnd && IsIdentifierPart(Current))
        {
            Advance();
        }

        var text = _source[start.._index];
        var kind = text switch
        {
            "flow" => TokenKind.Flow,
            "sequence" => TokenKind.Sequence,
            "start" => TokenKind.Start,
            "task" => TokenKind.Task,
            "decision" => TokenKind.Decision,
            "end" => TokenKind.End,
            "actor" => TokenKind.Actor,
            "participant" => TokenKind.Participant,
            _ => TokenKind.Identifier
        };

        _tokens.Add(new SyntaxToken(kind, text, line, column));
    }

    private void ReadString()
    {
        var line = _line;
        var column = _column;
        Advance();

        var start = _index;
        while (!IsAtEnd && Current is not '"' and not '\r' and not '\n')
        {
            Advance();
        }

        if (IsAtEnd || Current is '\r' or '\n')
        {
            var partialText = _source[start.._index];
            _tokens.Add(new SyntaxToken(TokenKind.String, partialText, line, column));
            _errors.Add(new DiagramSyntaxError(line, column, "Unterminated string literal."));
            return;
        }

        var text = _source[start.._index];
        _tokens.Add(new SyntaxToken(TokenKind.String, text, line, column));
        Advance();
    }

    private void ReadLineText()
    {
        while (!IsAtEnd && Current is (' ' or '\t'))
        {
            Advance();
        }

        if (IsAtEnd || Current is '\r' or '\n')
        {
            return;
        }

        var line = _line;
        var column = _column;
        var start = _index;

        while (!IsAtEnd && Current is not '\r' and not '\n')
        {
            Advance();
        }

        var text = _source[start.._index].TrimEnd(' ', '\t');
        if (text.Length > 0)
        {
            _tokens.Add(new SyntaxToken(TokenKind.LineText, text, line, column));
        }
    }

    private void ReadNewLine()
    {
        var line = _line;
        var column = _column;

        if (Current == '\r' && Peek() == '\n')
        {
            Advance();
        }

        AdvanceLine();
        _tokens.Add(new SyntaxToken(TokenKind.EndOfLine, string.Empty, line, column));
    }

    private void AddToken(TokenKind kind, string text)
    {
        _tokens.Add(new SyntaxToken(kind, text, _line, _column));
    }

    private void Advance()
    {
        _index++;
        _column++;
    }

    private void AdvanceLine()
    {
        _index++;
        _line++;
        _column = 1;
    }

    private char Peek()
    {
        return Peek(1);
    }

    private char Peek(int offset)
    {
        var next = _index + offset;

        return next >= _source.Length ? '\0' : _source[next];
    }

    private char Current => _source[_index];

    private bool IsAtEnd => _index >= _source.Length;

    private static bool IsIdentifierStart(char value)
    {
        return char.IsAsciiLetter(value) || value == '_';
    }

    private static bool IsIdentifierPart(char value)
    {
        return char.IsAsciiLetterOrDigit(value) || value == '_';
    }
}
