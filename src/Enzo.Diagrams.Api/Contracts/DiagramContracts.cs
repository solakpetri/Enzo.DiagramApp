using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Enzo.Diagrams.Api;

internal sealed record ValidateDiagramRequest(
    [property: Required]
    [property: MinLength(1)]
    [property: Description("Enzo.Diagrams DSL source. The first declaration is flow, sequence, or bpmn.")]
    string Source);

internal sealed record ValidateDiagramResponse(
    [property: Description("True when the submitted DSL parses and validates successfully.")]
    bool Valid);

internal sealed record BatchValidateDiagramRequest(
    [property: Required]
    [property: Description("Diagrams to validate. A batch can contain between 1 and 50 items.")]
    IReadOnlyList<BatchValidateDiagramItemRequest>? Items);

internal sealed record BatchValidateDiagramItemRequest(
    [property: Description("Optional client-provided identifier echoed in the response. Defaults to the zero-based item index.")]
    string? Id,
    [property: Required]
    [property: MinLength(1)]
    [property: Description("Enzo.Diagrams DSL source. The first declaration is flow, sequence, or bpmn.")]
    string Source);

internal sealed record BatchValidateDiagramResponse(
    [property: Description("Number of submitted diagrams.")]
    int Total,
    [property: Description("Number of diagrams that parsed and validated successfully.")]
    int Valid,
    [property: Description("Per-diagram validation outcomes in request order.")]
    IReadOnlyList<BatchValidateDiagramItemResponse> Items);

internal sealed record BatchValidateDiagramItemResponse(
    [property: Description("Client-provided identifier, or the zero-based item index when omitted.")]
    string Id,
    [property: Description("True when this diagram parses and validates successfully.")]
    bool Valid,
    [property: Description("Syntax, semantic, or limit errors for this diagram.")]
    IReadOnlyList<DiagramProblemError> Errors);

internal sealed record RenderDiagramRequest(
    [property: Required]
    [property: MinLength(1)]
    [property: Description("Enzo.Diagrams DSL source. The first declaration is flow, sequence, or bpmn.")]
    string Source,
    [property: Required]
    [property: RegularExpression("^(svg|png)$")]
    [property: Description("Render format. Supported values are svg and png.")]
    string Format,
    [property: RegularExpression("^(raw|url)$")]
    [property: Description("Result delivery mode. Use raw for the rendered response body, or url for a temporary hosted PNG URL.")]
    string? Delivery = null);

internal sealed record HostedRenderDiagramResponse(
    [property: Description("Opaque render identifier for the temporary hosted image.")]
    string Id,
    [property: Description("Render format. Hosted results currently support png.")]
    string Format,
    [property: Description("Hosted image content type.")]
    string ContentType,
    [property: Description("Short-lived read-only URL for the actual Enzo-rendered image.")]
    string Url,
    [property: Description("UTC timestamp when the hosted image URL expires.")]
    DateTimeOffset ExpiresAt);

internal sealed record DiagramProblemError(
    string Type,
    int? Line,
    int? Column,
    string Message,
    string? Code = null);
