namespace Enzo.Diagrams.Language;

public sealed record BpmnValidationError(
    BpmnValidationErrorKind Kind,
    int Line,
    int Column,
    string Message,
    string? ElementId = null,
    string? FlowFromId = null,
    string? FlowToId = null);

public enum BpmnValidationErrorKind
{
    DuplicateElementIdentifier,
    UnknownFlowSource,
    UnknownFlowTarget,
    MissingStartEvent,
    MissingEndEvent,
    CycleDetected
}
