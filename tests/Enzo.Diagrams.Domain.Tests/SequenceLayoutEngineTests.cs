using Xunit;

namespace Enzo.Diagrams.Domain.Tests;

public sealed class SequenceLayoutEngineTests
{
    [Fact]
    public void Layout_Sequence_OrdersParticipantsLeftToRight()
    {
        var diagram = Parse("""
            sequence Checkout
            actor Customer
            participant API
            participant Payment
            Customer -> API: Checkout
            API -> Payment: Charge
            Payment --> API: Success
            """);

        var layout = SequenceLayoutEngine.Layout(diagram);

        Assert.True(Participant(layout, "Customer").LifelineX < Participant(layout, "API").LifelineX);
        Assert.True(Participant(layout, "API").LifelineX < Participant(layout, "Payment").LifelineX);
        Assert.Equal(3, layout.Messages.Count);
    }

    [Fact]
    public void Layout_Sequence_OrdersMessagesTopToBottom()
    {
        var diagram = Parse("""
            sequence Checkout
            actor Customer
            participant API
            Customer -> API: Checkout
            API --> Customer: Confirmed
            """);

        var layout = SequenceLayoutEngine.Layout(diagram);

        Assert.True(layout.Messages[0].Start.Y < layout.Messages[1].Start.Y);
        Assert.Equal(424, layout.Bounds.Width);
        Assert.Equal(288, layout.Bounds.Height);
    }

    private static SequenceDiagram Parse(string source)
    {
        var result = SequenceParser.Parse(source);

        Assert.True(result.IsSuccess);
        return result.SequenceDiagram!;
    }

    private static PositionedSequenceParticipant Participant(SequenceLayout layout, string id)
    {
        return layout.Participants.Single(participant => participant.Participant.Id == id);
    }
}
