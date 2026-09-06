using Enzo.Diagrams.Language;

namespace Enzo.Diagrams.Mcp;

public interface IEnzoDiagramRenderer
{
    Task<byte[]> RenderPngAsync(DiagramParseResult result, CancellationToken cancellationToken);
}
