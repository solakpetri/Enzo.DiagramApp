using System.ComponentModel;

namespace Enzo.Diagrams.Mcp;

public sealed record RenderDiagramMetadata(
    [property: Description("MIME type of the returned image content.")]
    string ContentType,
    [property: Description("Rendered image format.")]
    string Format,
    [property: Description("Parsed Enzo.Diagrams DSL diagram kind.")]
    string DiagramFormat,
    [property: Description("Rendered PNG byte length before MCP base64 encoding.")]
    int ByteLength);
