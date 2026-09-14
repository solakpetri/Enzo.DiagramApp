namespace Enzo.Diagrams.Domain;

public static class SequenceValidator
{
    public static IReadOnlyList<SequenceValidationError> Validate(SequenceDiagram diagram)
    {
        var errors = new List<SequenceValidationError>();
        var participantsById = new Dictionary<string, SequenceParticipant>(StringComparer.Ordinal);

        foreach (var participant in diagram.Participants)
        {
            if (participantsById.TryGetValue(participant.Id, out var existingParticipant))
            {
                errors.Add(new SequenceValidationError(
                    SequenceValidationErrorKind.DuplicateParticipantIdentifier,
                    participant.Line,
                    participant.Column,
                    $"Participant identifier '{participant.Id}' is already declared on line {existingParticipant.Line}.",
                    ParticipantId: participant.Id));
                continue;
            }

            participantsById.Add(participant.Id, participant);
        }

        foreach (var message in diagram.Messages)
        {
            if (!participantsById.ContainsKey(message.FromId))
            {
                errors.Add(UnknownParticipantError(
                    SequenceValidationErrorKind.UnknownMessageSource,
                    message,
                    message.FromId));
            }

            if (!participantsById.ContainsKey(message.ToId))
            {
                errors.Add(UnknownParticipantError(
                    SequenceValidationErrorKind.UnknownMessageTarget,
                    message,
                    message.ToId));
            }
        }

        return errors;
    }

    private static SequenceValidationError UnknownParticipantError(
        SequenceValidationErrorKind kind,
        SequenceMessage message,
        string participantId)
    {
        return new SequenceValidationError(
            kind,
            message.Line,
            message.Column,
            $"Unknown participant '{participantId}' referenced by message on line {message.Line}.",
            ParticipantId: participantId,
            MessageFromId: message.FromId,
            MessageToId: message.ToId);
    }
}
