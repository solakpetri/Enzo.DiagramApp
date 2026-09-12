using Enzo.Diagrams.Domain;
using SkiaSharp;
using Xunit;

namespace Enzo.Diagrams.Infrastructure.Tests;

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
    public void PngRenderer_RendersFlowchartText()
    {
        var result = FlowchartParser.Parse("""
            flow FontTest

            start Begin "Begin"
            task Render "Render text"
            decision Valid "Text visible?"
            end Done "Done"

            Begin -> Render
            Render -> Valid
            Valid -> Done : yes
            """);
        var layout = FlowchartLayoutEngine.Layout(result.Flowchart!);
        var svg = FlowchartSvgRenderer.Render(layout);

        var changedPixels = CountChangedPixels(
            FlowchartPngRenderer.Render(svg),
            FlowchartPngRenderer.Render(RemoveTextElements(svg)));

        Assert.Contains("Text visible?", svg);
        Assert.True(changedPixels > 100, $"Expected rendered text to change PNG pixels, but only {changedPixels} pixels changed.");
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
    public void PngRenderer_RendersSequenceText()
    {
        var result = SequenceParser.Parse("""
            sequence FontTest

            actor Customer "Customer"
            participant Api "Diagram API"

            Customer -> Api: Render PNG
            Api --> Customer: Text visible
            """);
        var layout = SequenceLayoutEngine.Layout(result.SequenceDiagram!);
        var svg = SequenceSvgRenderer.Render(layout);

        var changedPixels = CountChangedPixels(
            FlowchartPngRenderer.Render(svg),
            FlowchartPngRenderer.Render(RemoveTextElements(svg)));

        Assert.Contains("Diagram API", svg);
        Assert.Contains("Text visible", svg);
        Assert.True(changedPixels > 100, $"Expected rendered text to change PNG pixels, but only {changedPixels} pixels changed.");
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

    [Fact]
    public void FlowchartSvgRenderer_EscapesUserControlledText()
    {
        var payload = InjectionPayload();
        var diagram = new Flowchart(
            "Injection",
            [
                new FlowchartNode(FlowchartNodeKind.Start, "Begin", payload),
                new FlowchartNode(FlowchartNodeKind.End, "Done", "Done")
            ],
            [new FlowchartEdge("Begin", "Done", payload)]);

        var svg = FlowchartSvgRenderer.Render(FlowchartLayoutEngine.Layout(diagram));

        AssertEscaped(svg);
    }

    [Fact]
    public void SequenceSvgRenderer_EscapesUserControlledText()
    {
        var payload = InjectionPayload();
        var diagram = new SequenceDiagram(
            "Injection",
            [
                new SequenceParticipant(SequenceParticipantKind.Actor, "User", DisplayName: payload),
                new SequenceParticipant(SequenceParticipantKind.Participant, "Api", DisplayName: "API")
            ],
            [new SequenceMessage("User", "Api", SequenceMessageKind.Synchronous, payload)]);

        var svg = SequenceSvgRenderer.Render(SequenceLayoutEngine.Layout(diagram));

        AssertEscaped(svg);
    }

    [Fact]
    public void BpmnSvgRenderer_EscapesUserControlledText()
    {
        var payload = InjectionPayload();
        var diagram = new BpmnDiagram(
            "Injection",
            [
                new BpmnElement(BpmnElementKind.StartEvent, "Begin", payload),
                new BpmnElement(BpmnElementKind.EndEvent, "Done", "Done")
            ],
            [new BpmnSequenceFlow("Begin", "Done", payload)]);

        var svg = BpmnSvgRenderer.Render(BpmnLayoutEngine.Layout(diagram));

        AssertEscaped(svg);
    }

    [Fact]
    public void PngRenderer_RejectsExcessiveDimensions()
    {
        const string svg = """
            <?xml version="1.0" encoding="UTF-8"?>
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="200" viewBox="0 0 200 200">
            <rect width="200" height="200" fill="#fff" />
            </svg>
            """;

        var exception = Assert.Throws<FlowchartPngRenderException>(() => FlowchartPngRenderer.Render(svg, 100, 100, 10_000));

        Assert.Equal("SVG dimensions exceed configured PNG rendering limits.", exception.Message);
    }

    private static int CountChangedPixels(byte[] firstPng, byte[] secondPng)
    {
        using var first = SKBitmap.Decode(firstPng);
        using var second = SKBitmap.Decode(secondPng);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.Width, second.Width);
        Assert.Equal(first.Height, second.Height);

        var changedPixels = 0;
        for (var y = 0; y < first.Height; y++)
        {
            for (var x = 0; x < first.Width; x++)
            {
                if (first.GetPixel(x, y) != second.GetPixel(x, y))
                {
                    changedPixels++;
                }
            }
        }

        return changedPixels;
    }

    private static string RemoveTextElements(string svg)
    {
        var result = svg;
        while (true)
        {
            var start = result.IndexOf("<text ", StringComparison.Ordinal);
            if (start < 0)
            {
                return result;
            }

            var end = result.IndexOf("</text>", start, StringComparison.Ordinal);
            if (end < 0)
            {
                return result;
            }

            result = result.Remove(start, end - start + "</text>".Length);
        }
    }

    private static string InjectionPayload()
    {
        return "</text><script>alert(1)</script><foreignObject onload=alert(1)>\"&<image href=javascript:alert(1)>";
    }

    private static void AssertEscaped(string svg)
    {
        Assert.Contains("&lt;script&gt;", svg);
        Assert.Contains("&lt;/text&gt;", svg);
        Assert.Contains("&lt;foreignObject", svg);
        Assert.DoesNotContain("<script", svg);
        Assert.DoesNotContain("<foreignObject", svg);
        Assert.DoesNotContain("<image", svg);
        Assert.DoesNotContain("</text><", svg);
    }
}
