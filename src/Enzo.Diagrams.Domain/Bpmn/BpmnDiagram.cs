namespace Enzo.Diagrams.Domain;

public sealed record BpmnDiagram(
    string Name,
    IReadOnlyList<BpmnElement> Elements,
    IReadOnlyList<BpmnSequenceFlow> Flows,
    int Line = 0,
    int Column = 0);

public sealed record BpmnElement(
    BpmnElementKind Kind,
    string Id,
    string Label,
    int Line = 0,
    int Column = 0);

public sealed record BpmnSequenceFlow(
    string FromId,
    string ToId,
    string? Label,
    int Line = 0,
    int Column = 0);

public enum BpmnElementKind
{
    StartEvent,
    Task,
    ExclusiveGateway,
    EndEvent
}
