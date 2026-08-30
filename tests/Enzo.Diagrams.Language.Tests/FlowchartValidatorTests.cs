using Xunit;

namespace Enzo.Diagrams.Language.Tests;

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
}
