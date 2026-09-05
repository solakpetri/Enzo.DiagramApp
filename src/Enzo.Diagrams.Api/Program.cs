using System.Text;
using System.Text.Json;
using Enzo.Diagrams.Api;
using Enzo.Diagrams.Language;
using Enzo.Diagrams.Rendering;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddOpenApi("v1");

var app = builder.Build();

app.UseForwardedHeaders();
app.MapOpenApi();

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
.WithName("ValidateDiagram")
.WithTags("Diagrams")
.WithSummary("Validate Enzo.Diagrams DSL.")
.WithDescription("Parses and validates the source DSL without rendering. Syntax errors, validation errors, malformed JSON, and missing source values return ProblemDetails with an errors extension.")
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
.WithName("RenderDiagram")
.WithTags("Diagrams")
.WithSummary("Render Enzo.Diagrams DSL.")
.WithDescription("Parses, validates, lays out, and renders the source DSL as SVG or PNG. The format field supports svg and png. Syntax errors, validation errors, malformed JSON, missing source or format values, unsupported formats, and PNG rasterization failures return ProblemDetails with an errors extension.")
.Accepts<RenderDiagramRequest>("application/json")
.Produces(StatusCodes.Status200OK)
.AddOpenApiOperationTransformer((operation, _, _) =>
{
    if (operation.Responses is null || !operation.Responses.TryGetValue("200", out var response))
    {
        return Task.CompletedTask;
    }

    var content = response.Content ?? throw new InvalidOperationException("Generated OpenAPI response content is unavailable.");

    response.Description = "Rendered SVG or PNG diagram.";
    content.TryAdd("image/svg+xml", new OpenApiMediaType
    {
        Schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Description = "SVG document."
        }
    });
    content.TryAdd("image/png", new OpenApiMediaType
    {
        Schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Format = "binary",
            Description = "PNG image bytes."
        }
    });

    return Task.CompletedTask;
})
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
