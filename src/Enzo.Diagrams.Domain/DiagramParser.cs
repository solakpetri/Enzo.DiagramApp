namespace Enzo.Diagrams.Domain;

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
            TokenKind.Identifier when firstToken.Text == "bpmn" => FromBpmn(BpmnParser.Parse(source)),
            _ => new DiagramParseResult(null, null, null, [.. tokenizeResult.Errors, new DiagramSyntaxError(firstToken.Line, firstToken.Column, "Expected flow, sequence, or bpmn declaration.")], [])
        };
    }

    private static DiagramParseResult FromFlowchart(FlowchartParseResult result)
    {
        return new DiagramParseResult(
            result.Flowchart,
            null,
            null,
            result.Errors,
            result.ValidationErrors.Select(error => new DiagramValidationError(error.Kind.ToString(), error.Line, error.Column, error.Message)).ToList());
    }

    private static DiagramParseResult FromSequence(SequenceParseResult result)
    {
        return new DiagramParseResult(
            null,
            result.SequenceDiagram,
            null,
            result.Errors,
            result.ValidationErrors.Select(error => new DiagramValidationError(error.Kind.ToString(), error.Line, error.Column, error.Message)).ToList());
    }

    private static DiagramParseResult FromBpmn(BpmnParseResult result)
    {
        return new DiagramParseResult(
            null,
            null,
            result.Diagram,
            result.Errors,
            result.ValidationErrors.Select(error => new DiagramValidationError(error.Kind.ToString(), error.Line, error.Column, error.Message)).ToList());
    }
}
