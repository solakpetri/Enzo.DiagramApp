namespace Enzo.Diagrams.Language;

public sealed record FlowchartLayout(
    IReadOnlyList<PositionedFlowchartNode> Nodes,
    IReadOnlyList<PositionedFlowchartConnection> Connections,
    DiagramBounds Bounds);

public sealed record PositionedFlowchartNode(
    FlowchartNode Node,
    double X,
    double Y,
    FlowchartNodeSize Size);

public sealed record FlowchartNodeSize(double Width, double Height);

public sealed record PositionedFlowchartConnection(
    FlowchartEdge Edge,
    IReadOnlyList<DiagramPoint> Points);

public sealed record DiagramPoint(double X, double Y);

public sealed record DiagramBounds(
    double X,
    double Y,
    double Width,
    double Height);
