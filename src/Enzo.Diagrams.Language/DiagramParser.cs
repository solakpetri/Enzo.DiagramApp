namespace Enzo.Diagrams.Language;

public static class DiagramParser
{
    public static DiagramParseResult Parse(string source)
    {
        var tokenizeResult = DiagramLexer.Tokenize(source);
        var firstToken = tokenizeResult.Tokens.First(token => token.Kind != TokenKind.EndOfLine);

        return firstToken.Kind switch
        {
            TokenKind.Flow => FromFlowchart(FlowchartParser.Parse(source)),
            TokenKind.Sequence => FromSequence(SequenceParser.Parse(source)),
            _ => new DiagramParseResult(null, null, [.. tokenizeResult.Errors, new DiagramSyntaxError(firstToken.Line, firstToken.Column, "Expected flow or sequence declaration.")], [])
        };
    }

    private static DiagramParseResult FromFlowchart(FlowchartParseResult result)
    {
        return new DiagramParseResult(
            result.Flowchart,
            null,
            result.Errors,
            result.ValidationErrors.Select(error => new DiagramValidationError(error.Kind.ToString(), error.Line, error.Column, error.Message)).ToList());
    }

    private static DiagramParseResult FromSequence(SequenceParseResult result)
    {
        return new DiagramParseResult(
            null,
            result.SequenceDiagram,
            result.Errors,
            result.ValidationErrors.Select(error => new DiagramValidationError(error.Kind.ToString(), error.Line, error.Column, error.Message)).ToList());
    }
}
