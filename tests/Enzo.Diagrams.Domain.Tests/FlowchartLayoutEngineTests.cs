using Xunit;

namespace Enzo.Diagrams.Domain.Tests;

public sealed class FlowchartLayoutEngineTests
{
    [Fact]
    public void Layout_SameFlowchart_ReturnsSameNodePositions()
    {
        var flowchart = Parse("""
            flow Checkout
            start Begin "Order received"
            task Validate "Validate order"
            decision Available "Stock available?"
            task Reserve "Reserve stock"
            end Complete "Complete order"
            end Reject "Reject order"
            Begin -> Validate
            Validate -> Available
            Available -> Reserve : yes
            Available -> Reject : no
            Reserve -> Complete
            """);

        var first = FlowchartLayoutEngine.Layout(flowchart);
        var second = FlowchartLayoutEngine.Layout(flowchart);

        Assert.Equal(NodeSnapshot(first), NodeSnapshot(second));
    }

    [Fact]
    public void Layout_LinearFlowchart_OrdersNodesTopToBottom()
    {
        var flowchart = Parse("""
            flow Checkout
            start Begin "Order received"
            task Validate "Validate order"
            end Complete "Complete order"
            Begin -> Validate
            Validate -> Complete
            """);

        var layout = FlowchartLayoutEngine.Layout(flowchart);

        Assert.True(Node(layout, "Begin").Y < Node(layout, "Validate").Y);
        Assert.True(Node(layout, "Validate").Y < Node(layout, "Complete").Y);
    }

    [Fact]
    public void Layout_LinearFlowchart_ReturnsCanvasBounds()
    {
        var flowchart = Parse("""
            flow Checkout
            start Begin "Order received"
            end Complete "Complete order"
            Begin -> Complete
            """);

        var layout = FlowchartLayoutEngine.Layout(flowchart);

        Assert.Equal(new DiagramBounds(0, 0, 240, 304), layout.Bounds);
        Assert.All(layout.Nodes, node =>
        {
            Assert.True(node.X >= layout.Bounds.X);
            Assert.True(node.Y >= layout.Bounds.Y);
            Assert.True(node.X + node.Size.Width <= layout.Bounds.Width);
            Assert.True(node.Y + node.Size.Height <= layout.Bounds.Height);
        });
    }

    [Fact]
    public void Layout_DecisionFlowchart_SpreadsBranchesHorizontally()
    {
        var flowchart = Parse("""
            flow Approval
            start Begin "Begin"
            decision Review "Approve?"
            task Approve "Approve"
            task Reject "Reject"
            end Complete "Complete"
            Begin -> Review
            Review -> Approve : yes
            Review -> Reject : no
            Approve -> Complete
            Reject -> Complete
            """);

        var layout = FlowchartLayoutEngine.Layout(flowchart);
        var review = Node(layout, "Review");
        var approve = Node(layout, "Approve");
        var reject = Node(layout, "Reject");
        var complete = Node(layout, "Complete");

        Assert.Equal(approve.Y, reject.Y);
        Assert.True(review.Y < approve.Y);
        Assert.True(approve.Y < complete.Y);
        Assert.True(approve.X < review.X);
        Assert.True(reject.X > review.X);
        Assert.Equal(new DiagramBounds(0, 0, 480, 624), layout.Bounds);
    }

    private static Flowchart Parse(string source)
    {
        var result = FlowchartParser.Parse(source);

        Assert.True(result.IsSuccess);
        return result.Flowchart!;
    }

    private static PositionedFlowchartNode Node(FlowchartLayout layout, string id)
    {
        return layout.Nodes.Single(node => node.Node.Id == id);
    }

    private static IReadOnlyList<(string Id, double X, double Y, double Width, double Height)> NodeSnapshot(
        FlowchartLayout layout)
    {
        return layout.Nodes
            .Select(node => (node.Node.Id, node.X, node.Y, node.Size.Width, node.Size.Height))
            .ToList();
    }
}
