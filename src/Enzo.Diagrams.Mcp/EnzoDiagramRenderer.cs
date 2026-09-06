using Enzo.Diagrams.Language;
using Enzo.Diagrams.Rendering;

namespace Enzo.Diagrams.Mcp;

public sealed class EnzoDiagramRenderer : IEnzoDiagramRenderer
{
    public Task<byte[]> RenderPngAsync(DiagramParseResult result, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var svg = DiagramSvgRenderer.Render(result);
        cancellationToken.ThrowIfCancellationRequested();

        var png = FlowchartPngRenderer.Render(svg);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(png);
    }
}
