namespace Enzo.Diagrams.Language;

public sealed record SequenceDiagram(
    string Name,
    IReadOnlyList<SequenceParticipant> Participants,
    IReadOnlyList<SequenceMessage> Messages,
    int Line = 0,
    int Column = 0);

public sealed record SequenceParticipant(
    SequenceParticipantKind Kind,
    string Id,
    int Line = 0,
    int Column = 0,
    string? DisplayName = null);

public sealed record SequenceMessage(
    string FromId,
    string ToId,
    SequenceMessageKind Kind,
    string Label,
    int Line = 0,
    int Column = 0);

public enum SequenceParticipantKind
{
    Actor,
    Participant
}

public enum SequenceMessageKind
{
    Synchronous,
    Response
}
