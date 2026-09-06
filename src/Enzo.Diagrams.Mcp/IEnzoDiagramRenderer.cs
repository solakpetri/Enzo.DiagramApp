using Enzo.Diagrams.Language;

namespace Enzo.Diagrams.Mcp;

public interface IEnzoDiagramRenderer
{
    byte[] RenderPng(DiagramParseResult result);
}
