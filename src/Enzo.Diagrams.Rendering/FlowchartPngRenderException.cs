namespace Enzo.Diagrams.Rendering;

public sealed class FlowchartPngRenderException : Exception
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
