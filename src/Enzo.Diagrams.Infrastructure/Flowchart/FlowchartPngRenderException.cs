using Enzo.Diagrams.Application;

namespace Enzo.Diagrams.Infrastructure;

public sealed class FlowchartPngRenderException : DiagramPngRenderException
{
    public FlowchartPngRenderException(string message)
        : base(message)
    {
    }

    public FlowchartPngRenderException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
