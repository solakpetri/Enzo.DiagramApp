namespace Enzo.Diagrams.Domain;

public sealed record SequenceValidationError(
    SequenceValidationErrorKind Kind,
    int Line,
    int Column,
    string Message,
    string? ParticipantId = null,
    string? MessageFromId = null,
    string? MessageToId = null);

public enum SequenceValidationErrorKind
{
    DuplicateParticipantIdentifier,
    UnknownMessageSource,
    UnknownMessageTarget
}
