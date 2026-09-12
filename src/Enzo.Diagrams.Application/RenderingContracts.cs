using Enzo.Diagrams.Domain;

namespace Enzo.Diagrams.Application;

public interface IDiagramRenderer
{
    string RenderSvg(DiagramParseResult result);

    byte[] RenderPng(string svg, PngRenderOptions? options = null);
}

public class DiagramRenderException(string message, Exception? innerException = null) : Exception(message, innerException);

public class DiagramPngRenderException(string message, Exception? innerException = null) : DiagramRenderException(message, innerException);
