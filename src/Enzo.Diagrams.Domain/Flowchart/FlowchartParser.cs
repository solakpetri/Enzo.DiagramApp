namespace Enzo.Diagrams.Domain;

public sealed class FlowchartParser
{
    private readonly IReadOnlyList<SyntaxToken> _tokens;
    private readonly List<DiagramSyntaxError> _errors;
    private int _position;

    private FlowchartParser(TokenizeResult tokenizeResult)
    {
        _tokens = tokenizeResult.Tokens;
        _errors = [.. tokenizeResult.Errors];
    }

    public static FlowchartParseResult Parse(string source)
    {
        var parser = new FlowchartParser(DiagramLexer.Tokenize(source));

        return parser.ParseFlowchart();
    }

    private FlowchartParseResult ParseFlowchart()
    {
        SkipEndOfLines();

        var flowToken = Expect(TokenKind.Flow, "Expected flow declaration.");
        var nameToken = Expect(TokenKind.Identifier, "Expected flow name.");
        RequireLineEnd();

        var nodes = new List<FlowchartNode>();
        var edges = new List<FlowchartEdge>();

        while (Current.Kind != TokenKind.EndOfFile)
        {
            SkipEndOfLines();

            if (Current.Kind == TokenKind.EndOfFile)
            {
                break;
            }

            if (TryReadNode(nodes))
            {
                continue;
            }

            if (Current.Kind == TokenKind.Identifier)
            {
                ReadEdge(edges);
                continue;
            }

            AddError($"Unexpected token '{Current.Text}'.");
            SynchronizeLine();
        }

        if (_errors.Count > 0 || flowToken is null || nameToken is null)
        {
            return new FlowchartParseResult(null, _errors, []);
        }

        var flowchart = new Flowchart(nameToken.Text, nodes, edges, flowToken.Line, flowToken.Column);
        var validationErrors = FlowchartValidator.Validate(flowchart);

        return new FlowchartParseResult(flowchart, _errors, validationErrors);
    }

    private bool TryReadNode(List<FlowchartNode> nodes)
    {
        var kind = Current.Kind switch
        {
            TokenKind.Start => FlowchartNodeKind.Start,
            TokenKind.Task => FlowchartNodeKind.Task,
            TokenKind.Decision => FlowchartNodeKind.Decision,
            TokenKind.End => FlowchartNodeKind.End,
            _ => (FlowchartNodeKind?)null
        };

        if (kind is null)
        {
            return false;
        }

        var errorCount = _errors.Count;
        Advance();

        var idToken = Expect(TokenKind.Identifier, "Expected node identifier.");
        var labelToken = Expect(TokenKind.String, "Expected quoted node label.");
        RequireLineEnd();

        if (_errors.Count == errorCount && idToken is not null && labelToken is not null)
        {
            nodes.Add(new FlowchartNode(kind.Value, idToken.Text, labelToken.Text, idToken.Line, idToken.Column));
        }

        return true;
    }

    private void ReadEdge(List<FlowchartEdge> edges)
    {
        var errorCount = _errors.Count;
        var fromToken = Expect(TokenKind.Identifier, "Expected source node identifier.");
        Expect(TokenKind.Arrow, "Expected '->' in edge declaration.");
        var toToken = Expect(TokenKind.Identifier, "Expected target node identifier.");
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
                AddError("Expected edge label after ':'.");
            }
        }

        RequireLineEnd();

        if (_errors.Count == errorCount && fromToken is not null && toToken is not null)
        {
            edges.Add(new FlowchartEdge(fromToken.Text, toToken.Text, label, fromToken.Line, fromToken.Column));
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
