using Xunit;

namespace Enzo.Diagrams.Domain.Tests;

public sealed class BpmnLayoutEngineTests
{
    [Fact]
    public void Layout_BasicSubset_OrdersElementsTopToBottom()
    {
        var diagram = BpmnParser.Parse("""
            bpmn Order
            start Received
            task Validate "Validate order"
            gateway Available "Stock available?"
            end Complete
            Received -> Validate
            Validate -> Available
            Available -> Complete : yes
            """).Diagram!;

        var layout = BpmnLayoutEngine.Layout(diagram);

        Assert.True(Element(layout, "Received").Y < Element(layout, "Validate").Y);
        Assert.True(Element(layout, "Validate").Y < Element(layout, "Available").Y);
        Assert.True(Element(layout, "Available").Y < Element(layout, "Complete").Y);
    }

    private static PositionedBpmnElement Element(BpmnLayout layout, string id)
    {
        return layout.Elements.Single(element => element.Element.Id == id);
    }
}
