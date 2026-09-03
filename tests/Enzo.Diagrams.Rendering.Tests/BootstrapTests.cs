using Enzo.Diagrams.Language;
using Xunit;

namespace Enzo.Diagrams.Rendering.Tests;

public sealed class BootstrapTests
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    public void ProjectBuilds()
    {
    }

    [Fact]
    public void PngRenderer_RasterizesGeneratedSvg()
    {
        var result = FlowchartParser.Parse("""
            flow Checkout
            start Begin "Order received"
            end Complete "Complete order"
            Begin -> Complete
            """);
        var layout = FlowchartLayoutEngine.Layout(result.Flowchart!);
        var svg = FlowchartSvgRenderer.Render(layout);

        var png = FlowchartPngRenderer.Render(svg);

        Assert.True(png.Take(PngSignature.Length).SequenceEqual(PngSignature));
    }

    [Fact]
    public void SequenceSvgRenderer_RendersMessagesAndRasterizes()
    {
        var result = SequenceParser.Parse("""
            sequence Checkout
            actor Customer
            participant API "Order API"
            Customer -> API: Checkout
            API --> Customer: Confirmed
            """);
        var layout = SequenceLayoutEngine.Layout(result.SequenceDiagram!);
        var svg = SequenceSvgRenderer.Render(layout);

        var png = FlowchartPngRenderer.Render(svg);

        Assert.Contains("Customer", svg);
        Assert.Contains("Order API", svg);
        Assert.Contains("stroke-dasharray=\"6 6\"", svg);
        Assert.True(png.Take(PngSignature.Length).SequenceEqual(PngSignature));
    }

    [Fact]
    public void BpmnSvgRenderer_RendersSubsetShapesAndRasterizes()
    {
        var result = BpmnParser.Parse("""
            bpmn Order
            start Received
            task Validate "Validate order"
            gateway Available "Stock available?"
            end Complete
            Received -> Validate
            Validate -> Available
            Available -> Complete : yes
            """);
        var svg = BpmnSvgRenderer.Render(BpmnLayoutEngine.Layout(result.Diagram!));

        var png = FlowchartPngRenderer.Render(svg);

        Assert.Contains("<circle", svg);
        Assert.Contains("<polygon", svg);
        Assert.Contains("Stock available?", svg);
        Assert.True(png.Take(PngSignature.Length).SequenceEqual(PngSignature));
    }

    [Fact]
    public void PngRenderer_InvalidSvg_ThrowsControlledException()
    {
        var exception = Assert.Throws<FlowchartPngRenderException>(() => FlowchartPngRenderer.Render("not svg"));

        Assert.Equal("SVG could not be rasterized.", exception.Message);
    }

    [Fact]
    public void FlowchartSvgRenderer_UsesCrossPlatformFontFamily()
    {
        var result = FlowchartParser.Parse("""
            flow Test
            start Begin "Begin"
            task Work "Do work"
            end Complete "Complete"
            Begin -> Work
            Work -> Complete
            """);
        var layout = FlowchartLayoutEngine.Layout(result.Flowchart!);
        var svg = FlowchartSvgRenderer.Render(layout);

        Assert.Contains("DejaVu Sans", svg);
        Assert.DoesNotContain("font-family=\"Arial", svg);
    }

    [Fact]
    public void SequenceSvgRenderer_UsesCrossPlatformFontFamily()
    {
        var result = SequenceParser.Parse("""
            sequence Test
            actor User
            participant API
            User -> API: Request
            """);
        var layout = SequenceLayoutEngine.Layout(result.SequenceDiagram!);
        var svg = SequenceSvgRenderer.Render(layout);

        Assert.Contains("DejaVu Sans", svg);
        Assert.DoesNotContain("font-family=\"Arial", svg);
    }

    [Fact]
    public void BpmnSvgRenderer_UsesCrossPlatformFontFamily()
    {
        var result = BpmnParser.Parse("""
            bpmn Test
            start Begin
            task Work "Do work"
            end Complete
            Begin -> Work
            Work -> Complete
            """);
        var svg = BpmnSvgRenderer.Render(BpmnLayoutEngine.Layout(result.Diagram!));

        Assert.Contains("DejaVu Sans", svg);
        Assert.DoesNotContain("font-family=\"Arial", svg);
    }
}
