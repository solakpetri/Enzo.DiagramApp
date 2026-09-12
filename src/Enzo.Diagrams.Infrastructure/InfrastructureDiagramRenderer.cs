using Enzo.Diagrams.Application;
using Enzo.Diagrams.Domain;

namespace Enzo.Diagrams.Infrastructure;

public sealed class InfrastructureDiagramRenderer : IDiagramRenderer
{
    public string RenderSvg(DiagramParseResult result)
    {
        return DiagramSvgRenderer.Render(result);
    }

    public byte[] RenderPng(string svg, PngRenderOptions? options = null)
    {
        return options is null
            ? FlowchartPngRenderer.Render(svg)
            : FlowchartPngRenderer.Render(svg, options.MaxWidth, options.MaxHeight, options.MaxPixels);
    }
}

public sealed class RenderResultStorageOptions
{
    public string Store { get; init; } = "Local";

    public string? BlobConnectionString { get; init; }

    public string? BlobContainerName { get; init; }

    public string LocalDirectory { get; init; } = Path.Combine(Path.GetTempPath(), "enzo-diagrams-render-results");
}
