namespace Enzo.Diagrams.Domain;

public static class SequenceLayoutEngine
{
    private const double HeaderWidth = 128;
    private const double HeaderHeight = 48;
    private const double ParticipantSpacing = 88;
    private const double MessageSpacing = 64;
    private const double Margin = 40;

    private static readonly FlowchartNodeSize HeaderSize = new(HeaderWidth, HeaderHeight);

    public static SequenceLayout Layout(SequenceDiagram diagram)
    {
        if (SequenceValidator.Validate(diagram).Count > 0)
        {
            throw new ArgumentException("Sequence diagram must be valid.", nameof(diagram));
        }

        if (diagram.Participants.Count == 0)
        {
            return new SequenceLayout([], [], new DiagramBounds(0, 0, 0, 0));
        }

        var lastMessageY = Margin + HeaderHeight + (diagram.Messages.Count * MessageSpacing);
        var lifelineStartY = Margin + HeaderHeight;
        var lifelineEndY = lastMessageY + (MessageSpacing / 2);
        var participants = PositionParticipants(diagram, lifelineStartY, lifelineEndY);
        var participantsById = participants.ToDictionary(participant => participant.Participant.Id, StringComparer.Ordinal);
        var messages = PositionMessages(diagram, participantsById);
        var bounds = new DiagramBounds(
            0,
            0,
            (Margin * 2) + (diagram.Participants.Count * HeaderWidth) + ((diagram.Participants.Count - 1) * ParticipantSpacing),
            lifelineEndY + Margin);

        return new SequenceLayout(participants, messages, bounds);
    }

    private static List<PositionedSequenceParticipant> PositionParticipants(
        SequenceDiagram diagram,
        double lifelineStartY,
        double lifelineEndY)
    {
        var participants = new List<PositionedSequenceParticipant>();
        var x = Margin;

        foreach (var participant in diagram.Participants)
        {
            participants.Add(new PositionedSequenceParticipant(
                participant,
                x,
                Margin,
                HeaderSize,
                x + (HeaderWidth / 2),
                lifelineStartY,
                lifelineEndY));
            x += HeaderWidth + ParticipantSpacing;
        }

        return participants;
    }

    private static List<PositionedSequenceMessage> PositionMessages(
        SequenceDiagram diagram,
        IReadOnlyDictionary<string, PositionedSequenceParticipant> participantsById)
    {
        var messages = new List<PositionedSequenceMessage>();

        for (var index = 0; index < diagram.Messages.Count; index++)
        {
            var message = diagram.Messages[index];
            var y = Margin + HeaderHeight + ((index + 1) * MessageSpacing);
            var start = new DiagramPoint(participantsById[message.FromId].LifelineX, y);
            var end = new DiagramPoint(participantsById[message.ToId].LifelineX, y);
            var label = new DiagramPoint((start.X + end.X) / 2, y - 10);

            messages.Add(new PositionedSequenceMessage(message, start, end, label));
        }

        return messages;
    }
}
