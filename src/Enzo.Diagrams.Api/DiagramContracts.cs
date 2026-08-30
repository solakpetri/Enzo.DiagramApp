namespace Enzo.Diagrams.Api;

internal sealed record ValidateDiagramRequest(string? Source);

internal sealed record ValidateDiagramResponse(bool Valid);

internal sealed record RenderDiagramRequest(string? Source, string? Format);

internal sealed record DiagramProblemError(
    string Type,
    int? Line,
    int? Column,
    string Message,
    string? Code = null);
