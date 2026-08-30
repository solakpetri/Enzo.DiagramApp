namespace Enzo.Diagrams.Language;

public sealed record Flowchart(
    string Name,
    IReadOnlyList<FlowchartNode> Nodes,
    IReadOnlyList<FlowchartEdge> Edges);

public sealed record FlowchartNode(
    FlowchartNodeKind Kind,
    string Id,
    string Label);

public sealed record FlowchartEdge(
    string FromId,
    string ToId,
    string? Label);

public enum FlowchartNodeKind
{
    Start,
    Task,
    Decision,
    End
}
