using Xunit;

namespace Enzo.Diagrams.Language.Tests;

public sealed class BpmnValidatorTests
{
    [Fact]
    public void Parse_DuplicateElementIdentifier_ReturnsValidationError()
    {
        var result = BpmnParser.Parse("bpmn Order\nstart Received\ntask Received \"Duplicate\"\nend Complete");

        var error = Assert.Single(result.ValidationErrors);
        Assert.False(result.IsSuccess);
        Assert.Equal(BpmnValidationErrorKind.DuplicateElementIdentifier, error.Kind);
        Assert.Equal("Received", error.ElementId);
    }

    [Fact]
    public void Parse_UnknownFlowTarget_ReturnsValidationError()
    {
        var result = BpmnParser.Parse("bpmn Order\nstart Received\nend Complete\nReceived -> Missing");

        var error = Assert.Single(result.ValidationErrors);
        Assert.False(result.IsSuccess);
        Assert.Equal(BpmnValidationErrorKind.UnknownFlowTarget, error.Kind);
        Assert.Equal("Missing", error.ElementId);
        Assert.Equal("Received", error.FlowFromId);
        Assert.Equal("Missing", error.FlowToId);
    }

    [Fact]
    public void Parse_MissingStartEvent_ReturnsValidationError()
    {
        var result = BpmnParser.Parse("bpmn Order\ntask Validate \"Validate order\"\nend Complete");

        var error = Assert.Single(result.ValidationErrors);
        Assert.False(result.IsSuccess);
        Assert.Equal(BpmnValidationErrorKind.MissingStartEvent, error.Kind);
    }

    [Fact]
    public void Parse_CyclicSubset_ReturnsValidationError()
    {
        var result = BpmnParser.Parse("bpmn Order\nstart Received\ntask Validate \"Validate order\"\nend Complete\nReceived -> Validate\nValidate -> Received");

        var error = Assert.Single(result.ValidationErrors);
        Assert.False(result.IsSuccess);
        Assert.Equal(BpmnValidationErrorKind.CycleDetected, error.Kind);
    }
}
