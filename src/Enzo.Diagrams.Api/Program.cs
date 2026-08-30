using System.Text;
using System.Text.Json;
using Enzo.Diagrams.Api;
using Enzo.Diagrams.Language;
using Enzo.Diagrams.Rendering;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();

var app = builder.Build();

app.MapPost("/v1/validate", async (HttpRequest httpRequest, CancellationToken cancellationToken) =>
{
    var (request, readError) = await ReadRequestAsync<ValidateDiagramRequest>(httpRequest, cancellationToken);
    if (readError is not null)
    {
        return readError;
    }

    if (!TryGetSource(request!.Source, out var source, out var inputError))
    {
        return inputError;
    }

    var result = DiagramParser.Parse(source);
    return result.IsSuccess
        ? Results.Ok(new ValidateDiagramResponse(true))
        : DiagramProblem(result);
})
.Accepts<ValidateDiagramRequest>("application/json")
.Produces<ValidateDiagramResponse>()
.ProducesProblem(StatusCodes.Status400BadRequest);

app.MapPost("/v1/render", async (HttpRequest httpRequest, CancellationToken cancellationToken) =>
{
    var (request, readError) = await ReadRequestAsync<RenderDiagramRequest>(httpRequest, cancellationToken);
    if (readError is not null)
    {
        return readError;
    }

    if (!TryGetSource(request!.Source, out var source, out var inputError))
    {
        return inputError;
    }

    if (string.IsNullOrWhiteSpace(request.Format))
    {
        return InvalidRequestProblem("Format is required.", [
            new DiagramProblemError("request", null, null, "Format is required.", "required")
        ]);
    }

    if (!IsSupportedRenderFormat(request.Format))
    {
        return InvalidRequestProblem("Only SVG and PNG rendering are supported.", [
            new DiagramProblemError("request", null, null, "Format must be 'svg' or 'png'.", "unsupported_format")
        ]);
    }

    var result = DiagramParser.Parse(source);
    if (!result.IsSuccess)
    {
        return DiagramProblem(result);
    }

    var svg = DiagramSvgRenderer.Render(result);

    if (string.Equals(request.Format, "png", StringComparison.OrdinalIgnoreCase))
    {
        try
        {
            return Results.File(FlowchartPngRenderer.Render(svg), "image/png");
        }
        catch (FlowchartPngRenderException)
        {
            return InvalidRequestProblem("Diagram could not be rendered as PNG.", [
                new DiagramProblemError("rendering", null, null, "Diagram could not be rendered as PNG.", "png_render_failed")
            ]);
        }
    }

    return Results.Text(svg, "image/svg+xml", Encoding.UTF8);
})
.Accepts<RenderDiagramRequest>("application/json")
.Produces(StatusCodes.Status200OK, contentType: "image/svg+xml")
.Produces(StatusCodes.Status200OK, contentType: "image/png")
.ProducesProblem(StatusCodes.Status400BadRequest);

app.Run();

static async Task<(T? Request, IResult? Error)> ReadRequestAsync<T>(
    HttpRequest httpRequest,
    CancellationToken cancellationToken)
{
    try
    {
        var request = await httpRequest.ReadFromJsonAsync<T>(cancellationToken);
        return request is null
            ? (default, InvalidRequestProblem("Request body is required.", [
                new DiagramProblemError("request", null, null, "Request body is required.", "required")
            ]))
            : (request, null);
    }
    catch (JsonException)
    {
        return (default, InvalidRequestProblem("Request body must be valid JSON.", [
            new DiagramProblemError("request", null, null, "Request body must be valid JSON.", "malformed_json")
        ]));
    }
    catch (BadHttpRequestException)
    {
        return (default, InvalidRequestProblem("Request body must be valid JSON.", [
            new DiagramProblemError("request", null, null, "Request body must be valid JSON.", "malformed_json")
        ]));
    }
}

static bool TryGetSource(string? value, out string source, out IResult error)
{
    source = value ?? string.Empty;
    if (!string.IsNullOrWhiteSpace(value))
    {
        error = Results.Empty;
        return true;
    }

    error = InvalidRequestProblem("Source is required.", [
        new DiagramProblemError("request", null, null, "Source is required.", "required")
    ]);
    return false;
}

static bool IsSupportedRenderFormat(string format)
{
    return string.Equals(format, "svg", StringComparison.OrdinalIgnoreCase)
        || string.Equals(format, "png", StringComparison.OrdinalIgnoreCase);
}

static IResult DiagramProblem(DiagramParseResult result)
{
    return Results.Problem(
        title: "Diagram DSL is invalid.",
        detail: "The request source contains syntax or validation errors.",
        statusCode: StatusCodes.Status400BadRequest,
        extensions: new Dictionary<string, object?>
        {
            ["errors"] = ToDiagramErrors(result)
        });
}

static IResult InvalidRequestProblem(string detail, IReadOnlyList<DiagramProblemError> errors)
{
    return Results.Problem(
        title: "Invalid request.",
        detail: detail,
        statusCode: StatusCodes.Status400BadRequest,
        extensions: new Dictionary<string, object?>
        {
            ["errors"] = errors
        });
}

static IReadOnlyList<DiagramProblemError> ToDiagramErrors(DiagramParseResult result)
{
    var errors = new List<DiagramProblemError>();

    errors.AddRange(result.Errors.Select(error =>
        new DiagramProblemError("syntax", error.Line, error.Column, error.Message)));

    errors.AddRange(result.ValidationErrors.Select(error =>
        new DiagramProblemError("validation", error.Line, error.Column, error.Message, error.Kind)));

    return errors;
}

public partial class Program;
