using Xunit;

namespace Enzo.Diagrams.Domain.Tests;

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

        Assert.Equal(new FlowchartNode(FlowchartNodeKind.Start, "Begin", "Order received", 3, 7), result.Flowchart.Nodes[0]);
        Assert.Equal(new FlowchartNode(FlowchartNodeKind.Decision, "Available", "Stock available?", 5, 10), result.Flowchart.Nodes[2]);
        Assert.Equal(new FlowchartEdge("Available", "Reserve", "yes", 12, 1), result.Flowchart.Edges[2]);
        Assert.Equal(new FlowchartEdge("Reserve", "Complete", null, 14, 1), result.Flowchart.Edges[4]);
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

    [Fact]
    public void Parse_DuplicateNodeIdentifier_ReturnsValidationError()
    {
        var result = FlowchartParser.Parse("flow Checkout\nstart Begin \"Start\"\ntask Begin \"Duplicate\"\nend Complete \"Done\"");

        var error = Assert.Single(result.ValidationErrors);
        Assert.False(result.IsSuccess);
        Assert.Equal(FlowchartValidationErrorKind.DuplicateNodeIdentifier, error.Kind);
        Assert.Equal("Begin", error.NodeId);
        Assert.Equal(3, error.Line);
        Assert.Equal(6, error.Column);
    }

    [Fact]
    public void Parse_UnknownEdgeSource_ReturnsValidationError()
    {
        var result = FlowchartParser.Parse("flow Checkout\nstart Begin \"Start\"\nend Complete \"Done\"\nBegn -> Complete");

        var error = Assert.Single(result.ValidationErrors);
        Assert.False(result.IsSuccess);
        Assert.Equal(FlowchartValidationErrorKind.UnknownEdgeSource, error.Kind);
        Assert.Equal("Begn", error.NodeId);
        Assert.Equal("Begn", error.EdgeFromId);
        Assert.Equal("Complete", error.EdgeToId);
        Assert.Equal("Begin", error.Suggestion);
        Assert.Contains("line 4", error.Message);
    }

    [Fact]
    public void Parse_UnknownEdgeTarget_ReturnsValidationError()
    {
        var result = FlowchartParser.Parse("flow Checkout\nstart Begin \"Start\"\nend Complete \"Done\"\nBegin -> Payments");

        var error = Assert.Single(result.ValidationErrors);
        Assert.False(result.IsSuccess);
        Assert.Equal(FlowchartValidationErrorKind.UnknownEdgeTarget, error.Kind);
        Assert.Equal("Payments", error.NodeId);
        Assert.Equal("Begin", error.EdgeFromId);
        Assert.Equal("Payments", error.EdgeToId);
        Assert.Equal(4, error.Line);
    }

    [Fact]
    public void Parse_MissingStartNode_ReturnsValidationError()
    {
        var result = FlowchartParser.Parse("flow Checkout\ntask Validate \"Validate\"\nend Complete \"Done\"");

        var error = Assert.Single(result.ValidationErrors);
        Assert.False(result.IsSuccess);
        Assert.Equal(FlowchartValidationErrorKind.MissingStartNode, error.Kind);
        Assert.Equal(1, error.Line);
        Assert.Equal(1, error.Column);
    }

    [Fact]
    public void Parse_MissingEndNode_ReturnsValidationError()
    {
        var result = FlowchartParser.Parse("flow Checkout\nstart Begin \"Start\"\ntask Validate \"Validate\"");

        var error = Assert.Single(result.ValidationErrors);
        Assert.False(result.IsSuccess);
        Assert.Equal(FlowchartValidationErrorKind.MissingEndNode, error.Kind);
        Assert.Equal(1, error.Line);
        Assert.Equal(1, error.Column);
    }
}
