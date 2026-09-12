using Enzo.Diagrams.Domain;
using Xunit;

namespace Enzo.Diagrams.Application.Tests;

public sealed class DiagramServiceTests
{
    [Fact]
    public void Validate_WithValidSource_ReturnsSuccessfulParseResult()
    {
        var service = new DiagramService(new FakeRenderer());

        var result = service.Validate(ValidSequenceSource);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.ParseResult.SequenceDiagram);
        Assert.Equal("Checkout", result.ParseResult.SequenceDiagram.Name);
        Assert.Null(result.LimitViolation);
    }

    [Fact]
    public void Validate_WithUndefinedReference_ReturnsValidationFailure()
    {
        var service = new DiagramService(new FakeRenderer());

        var result = service.Validate("sequence Checkout\nactor Customer\nCustomer -> Api: Checkout");

        Assert.False(result.IsSuccess);
        Assert.Empty(result.ParseResult.Errors);
        var error = Assert.Single(result.ParseResult.ValidationErrors);
        Assert.Equal("UnknownMessageTarget", error.Kind);
        Assert.Contains("Api", error.Message);
    }

    [Fact]
    public void Render_WithSvgFormat_ReturnsSvgAndDoesNotRenderPng()
    {
        var renderer = new FakeRenderer();
        var service = new DiagramService(renderer);

        var result = service.Render(ValidSequenceSource, DiagramRenderFormat.Svg);

        Assert.True(result.IsSuccess);
        Assert.Equal(FakeRenderer.Svg, result.Svg);
        Assert.Null(result.Png);
        Assert.Equal(1, renderer.RenderSvgCalls);
        Assert.Equal(0, renderer.RenderPngCalls);
    }

    [Fact]
    public void Render_WithPngFormat_ReturnsPngFromRenderedSvg()
    {
        var renderer = new FakeRenderer();
        var service = new DiagramService(renderer);

        var result = service.Render(ValidSequenceSource, DiagramRenderFormat.Png, pngOptions: new PngRenderOptions(100, 100, 10_000));

        Assert.True(result.IsSuccess);
        Assert.Null(result.Svg);
        Assert.Equal(FakeRenderer.Png, result.Png);
        Assert.Equal(FakeRenderer.Svg, renderer.LastPngInput);
        Assert.Equal(new PngRenderOptions(100, 100, 10_000), renderer.LastPngOptions);
    }

    [Fact]
    public void Render_WithInvalidSource_ReturnsInvalidResultWithoutRendering()
    {
        var renderer = new FakeRenderer();
        var service = new DiagramService(renderer);

        var result = service.Render("flow Broken\nstart Begin \"Begin\"", DiagramRenderFormat.Svg);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Svg);
        Assert.Null(result.Png);
        Assert.Equal(0, renderer.RenderSvgCalls);
        Assert.Contains(result.Validation.ParseResult.ValidationErrors, error => error.Kind == "MissingEndNode");
    }

    [Fact]
    public void Validate_WhenDiagramExceedsLimits_ReturnsLimitViolation()
    {
        var service = new DiagramService(new FakeRenderer());

        var result = service.Validate(ValidSequenceSource, new DiagramComplexityLimits(MaxDiagramElements: 2, MaxDiagramConnections: 10));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.LimitViolation);
        Assert.Equal("diagram_too_complex", result.LimitViolation.Code);
    }

    private const string ValidSequenceSource = """
        sequence Checkout
        actor Customer "Customer"
        participant Web "Web App"
        participant Api "Order API"
        Customer -> Web: Checkout
        Web -> Api: POST /orders
        Api --> Web: 201 Created
        Web --> Customer: Confirmation
        """;

    private sealed class FakeRenderer : IDiagramRenderer
    {
        public const string Svg = "<svg />";
        public static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47];

        public int RenderSvgCalls { get; private set; }
        public int RenderPngCalls { get; private set; }
        public string? LastPngInput { get; private set; }
        public PngRenderOptions? LastPngOptions { get; private set; }

        public string RenderSvg(DiagramParseResult result)
        {
            RenderSvgCalls++;
            Assert.True(result.IsSuccess);
            return Svg;
        }

        public byte[] RenderPng(string svg, PngRenderOptions? options = null)
        {
            RenderPngCalls++;
            LastPngInput = svg;
            LastPngOptions = options;
            return Png;
        }
    }
}
