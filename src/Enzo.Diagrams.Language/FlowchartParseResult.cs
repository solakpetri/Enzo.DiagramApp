namespace Enzo.Diagrams.Language;

public sealed record FlowchartParseResult(
    Flowchart? Flowchart,
    IReadOnlyList<DiagramSyntaxError> Errors)
{
    public bool IsSuccess => Flowchart is not null && Errors.Count == 0;
}
