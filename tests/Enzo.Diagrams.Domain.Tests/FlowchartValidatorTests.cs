using Xunit;

namespace Enzo.Diagrams.Domain.Tests;

public sealed class FlowchartValidatorTests
{
    [Fact]
    public void Validate_DuplicateNodeIdentifierOnSameLine_ReturnsValidationError()
    {
        var flowchart = new Flowchart(
            "Checkout",
            [
                new FlowchartNode(FlowchartNodeKind.Start, "Begin", "Start", 2, 7),
                new FlowchartNode(FlowchartNodeKind.End, "Begin", "Done", 2, 27)
            ],
            [],
            1,
            1);

        var error = Assert.Single(FlowchartValidator.Validate(flowchart));
        Assert.Equal(FlowchartValidationErrorKind.DuplicateNodeIdentifier, error.Kind);
        Assert.Equal("Begin", error.NodeId);
        Assert.Equal(2, error.Line);
        Assert.Equal(27, error.Column);
    }

    [Fact]
    public void Validate_Cycle_ReturnsValidationError()
    {
        var flowchart = new Flowchart(
            "Checkout",
            [
                new FlowchartNode(FlowchartNodeKind.Start, "Begin", "Start", 2, 7),
                new FlowchartNode(FlowchartNodeKind.Task, "Validate", "Validate", 3, 6),
                new FlowchartNode(FlowchartNodeKind.End, "Complete", "Done", 4, 5)
            ],
            [
                new FlowchartEdge("Begin", "Validate", null, 5, 1),
                new FlowchartEdge("Validate", "Begin", null, 6, 1),
                new FlowchartEdge("Validate", "Complete", null, 7, 1)
            ],
            1,
            1);

        var error = Assert.Single(FlowchartValidator.Validate(flowchart));
        Assert.Equal(FlowchartValidationErrorKind.CycleDetected, error.Kind);
        Assert.Equal("Begin", error.NodeId);
        Assert.Equal(2, error.Line);
        Assert.Equal(7, error.Column);
    }
}
