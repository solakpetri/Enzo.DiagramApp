using Xunit;

namespace Enzo.Diagrams.Domain.Tests;

public sealed class BpmnParserTests
{
    [Fact]
    public void Parse_ValidSubset_ReturnsBpmnAst()
    {
        var result = BpmnParser.Parse(ValidBpmn);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Diagram);
        Assert.Equal("Order", result.Diagram.Name);
        Assert.Equal(5, result.Diagram.Elements.Count);
        Assert.Equal(4, result.Diagram.Flows.Count);
        Assert.Equal(new BpmnElement(BpmnElementKind.StartEvent, "Received", "Received", 3, 7), result.Diagram.Elements[0]);
        Assert.Equal(new BpmnElement(BpmnElementKind.ExclusiveGateway, "Available", "Stock available?", 5, 9), result.Diagram.Elements[2]);
        Assert.Equal(new BpmnSequenceFlow("Available", "Reserve", "yes", 11, 1), result.Diagram.Flows[2]);
    }

    [Fact]
    public void DiagramParser_BpmnDeclaration_ReturnsBpmnDiagramOnly()
    {
        var result = DiagramParser.Parse(ValidBpmn);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.BpmnDiagram);
        Assert.Null(result.Flowchart);
        Assert.Null(result.SequenceDiagram);
    }

    [Fact]
    public void Parse_TaskWithoutQuotedLabel_ReturnsSyntaxError()
    {
        var result = BpmnParser.Parse("bpmn Order\nstart Received\ntask Validate Validate order\nend Complete");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Line == 3 && error.Message.Contains("quoted BPMN element label"));
    }

    [Fact]
    public void Parse_UnsupportedPoolDeclaration_ReturnsSyntaxError()
    {
        var result = BpmnParser.Parse("bpmn Order\npool Sales\nstart Received\nend Complete");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Line == 2 && error.Message.Contains("sequence flow"));
    }

    private const string ValidBpmn = """
        bpmn Order

        start Received
        task Validate "Validate order"
        gateway Available "Stock available?"
        task Reserve "Reserve stock"
        end Complete

        Received -> Validate
        Validate -> Available
        Available -> Reserve : yes
        Reserve -> Complete
        """;
}
