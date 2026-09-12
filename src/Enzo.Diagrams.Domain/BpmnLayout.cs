namespace Enzo.Diagrams.Domain;

public sealed record BpmnLayout(
    IReadOnlyList<PositionedBpmnElement> Elements,
    IReadOnlyList<PositionedBpmnSequenceFlow> Flows,
    DiagramBounds Bounds);

public sealed record PositionedBpmnElement(
    BpmnElement Element,
    double X,
    double Y,
    BpmnElementSize Size);

public sealed record BpmnElementSize(double Width, double Height);

public sealed record PositionedBpmnSequenceFlow(
    BpmnSequenceFlow Flow,
    IReadOnlyList<DiagramPoint> Points);
