using Xunit;

namespace Enzo.Diagrams.Language.Tests;

public sealed class FlowchartParserTests
{
    [Fact]
    public void Parse_ValidFlowchart_ReturnsAst()
    {
        const string source = """
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
            """;

        var result = FlowchartParser.Parse(source);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Flowchart);
        Assert.Equal("Checkout", result.Flowchart.Name);
        Assert.Equal(6, result.Flowchart.Nodes.Count);
        Assert.Equal(5, result.Flowchart.Edges.Count);

        Assert.Equal(new FlowchartNode(FlowchartNodeKind.Start, "Begin", "Order received"), result.Flowchart.Nodes[0]);
        Assert.Equal(new FlowchartNode(FlowchartNodeKind.Decision, "Available", "Stock available?"), result.Flowchart.Nodes[2]);
        Assert.Equal(new FlowchartEdge("Available", "Reserve", "yes"), result.Flowchart.Edges[2]);
        Assert.Equal(new FlowchartEdge("Reserve", "Complete", null), result.Flowchart.Edges[4]);
    }

    [Fact]
    public void Parse_MissingFlowName_ReturnsLineError()
    {
        var result = FlowchartParser.Parse("flow\nstart Begin \"Order received\"");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Flowchart);
        Assert.Contains(result.Errors, error => error.Line == 1 && error.Message.Contains("flow name"));
    }

    [Fact]
    public void Parse_NodeWithoutQuotedLabel_ReturnsLineError()
    {
        var result = FlowchartParser.Parse("flow Checkout\ntask Validate Validate order");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Line == 2 && error.Message.Contains("quoted node label"));
    }

    [Fact]
    public void Parse_EdgeWithoutArrow_ReturnsLineError()
    {
        var result = FlowchartParser.Parse("flow Checkout\nBegin Validate");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Line == 2 && error.Message.Contains("->"));
    }

    [Fact]
    public void Parse_EdgeLabelWithoutValue_ReturnsLineError()
    {
        var result = FlowchartParser.Parse("flow Checkout\nAvailable -> Reserve :");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Line == 2 && error.Message.Contains("edge label"));
    }
}
