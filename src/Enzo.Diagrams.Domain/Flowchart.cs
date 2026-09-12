namespace Enzo.Diagrams.Domain;

public sealed record Flowchart(
    string Name,
    IReadOnlyList<FlowchartNode> Nodes,
    IReadOnlyList<FlowchartEdge> Edges,
    int Line = 0,
    int Column = 0);

public sealed record FlowchartNode(
    FlowchartNodeKind Kind,
    string Id,
    string Label,
    int Line = 0,
    int Column = 0);

public sealed record FlowchartEdge(
    string FromId,
    string ToId,
    string? Label,
    int Line = 0,
    int Column = 0);

public enum FlowchartNodeKind
{
    Start,
    Task,
    Decision,
    End
}
