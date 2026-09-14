namespace Enzo.Diagrams.Domain;

public sealed record SequenceLayout(
    IReadOnlyList<PositionedSequenceParticipant> Participants,
    IReadOnlyList<PositionedSequenceMessage> Messages,
    DiagramBounds Bounds);

public sealed record PositionedSequenceParticipant(
    SequenceParticipant Participant,
    double X,
    double Y,
    FlowchartNodeSize Size,
    double LifelineX,
    double LifelineStartY,
    double LifelineEndY);

public sealed record PositionedSequenceMessage(
    SequenceMessage Message,
    DiagramPoint Start,
    DiagramPoint End,
    DiagramPoint LabelPosition);
