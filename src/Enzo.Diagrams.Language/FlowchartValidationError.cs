namespace Enzo.Diagrams.Language;

public sealed record FlowchartValidationError(
    FlowchartValidationErrorKind Kind,
    int Line,
    int Column,
    string Message,
    string? NodeId = null,
    string? EdgeFromId = null,
    string? EdgeToId = null,
    string? Suggestion = null);

public enum FlowchartValidationErrorKind
{
    DuplicateNodeIdentifier,
    UnknownEdgeSource,
    UnknownEdgeTarget,
    MissingStartNode,
    MissingEndNode
}
