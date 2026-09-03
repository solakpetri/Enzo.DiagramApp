using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Enzo.Diagrams.Api;

internal sealed record ValidateDiagramRequest(
    [property: Required]
    [property: MinLength(1)]
    [property: Description("Enzo.Diagrams DSL source. The first declaration is flow, sequence, or bpmn.")]
    string? Source);

internal sealed record ValidateDiagramResponse(
    [property: Description("True when the submitted DSL parses and validates successfully.")]
    bool Valid);

internal sealed record RenderDiagramRequest(
    [property: Required]
    [property: MinLength(1)]
    [property: Description("Enzo.Diagrams DSL source. The first declaration is flow, sequence, or bpmn.")]
    string? Source,
    [property: Required]
    [property: RegularExpression("^(svg|png)$")]
    [property: Description("Render format. Supported values are svg and png.")]
    string? Format);

internal sealed record DiagramProblemError(
    string Type,
    int? Line,
    int? Column,
    string Message,
    string? Code = null);
