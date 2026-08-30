namespace Enzo.Diagrams.Language;

public sealed record BpmnParseResult(
    BpmnDiagram? Diagram,
    IReadOnlyList<DiagramSyntaxError> Errors,
    IReadOnlyList<BpmnValidationError> ValidationErrors)
{
    public bool IsSuccess => Diagram is not null && Errors.Count == 0 && ValidationErrors.Count == 0;
}
