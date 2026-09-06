using Enzo.Diagrams.Language;
using Enzo.Diagrams.Rendering;

namespace Enzo.Diagrams.Mcp;

public sealed class EnzoDiagramRenderer : IEnzoDiagramRenderer
{
    public byte[] RenderPng(DiagramParseResult result)
    {
        var svg = DiagramSvgRenderer.Render(result);

        return FlowchartPngRenderer.Render(svg);
    }
}
