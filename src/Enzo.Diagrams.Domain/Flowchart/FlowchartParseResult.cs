namespace Enzo.Diagrams.Domain;

public sealed record FlowchartParseResult(
    Flowchart? Flowchart,
    IReadOnlyList<DiagramSyntaxError> Errors,
    IReadOnlyList<FlowchartValidationError> ValidationErrors)
{
    public bool IsSuccess => Flowchart is not null && Errors.Count == 0 && ValidationErrors.Count == 0;
}
