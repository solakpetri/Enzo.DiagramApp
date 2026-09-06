using System.Text;
using System.Text.Json;
using Enzo.Diagrams.Api;
using Enzo.Diagrams.Language;
using Enzo.Diagrams.Rendering;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddOptions<EnzoOptions>()
    .Bind(builder.Configuration.GetSection(EnzoOptions.SectionName))
    .Validate(options => !builder.Environment.IsProduction() || !string.IsNullOrWhiteSpace(options.ApiKey),
        "Enzo:ApiKey must be configured in production.")
    .Validate(ValidateRenderResultOptions, "Enzo:RenderResults is invalid.")
    .ValidateOnStart();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<EnzoOptions>>().Value.RenderResults;
    var timeProvider = serviceProvider.GetRequiredService<TimeProvider>();

    return new LocalRenderResultStore(options, timeProvider);
});
builder.Services.AddSingleton<ILocalRenderResultReader>(serviceProvider =>
    serviceProvider.GetRequiredService<LocalRenderResultStore>());
builder.Services.AddSingleton<IRenderResultStore>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<EnzoOptions>>().Value.RenderResults;
    var timeProvider = serviceProvider.GetRequiredService<TimeProvider>();

    return string.Equals(options.Store, "AzureBlob", StringComparison.OrdinalIgnoreCase)
        ? new AzureBlobRenderResultStore(options, timeProvider)
        : serviceProvider.GetRequiredService<LocalRenderResultStore>();
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer(AddApiKeySecurityScheme);
});

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
.ProducesProblem(StatusCodes.Status401Unauthorized)
.AddEndpointFilter<ApiKeyEndpointFilter>()
.ProducesProblem(StatusCodes.Status400BadRequest);

app.MapPost("/v1/render", async (
    HttpRequest httpRequest,
    IRenderResultStore renderResultStore,
    IOptions<EnzoOptions> options,
    CancellationToken cancellationToken) =>
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

    if (!TryGetRenderDelivery(request.Delivery, out var delivery))
    {
        return InvalidRequestProblem("Only raw and url delivery are supported.", [
            new DiagramProblemError("request", null, null, "Delivery must be 'raw' or 'url'.", "unsupported_delivery")
        ]);
    }

    if (string.Equals(delivery, "url", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(request.Format, "png", StringComparison.OrdinalIgnoreCase))
    {
        return InvalidRequestProblem("Hosted URL delivery currently supports PNG only.", [
            new DiagramProblemError("request", null, null, "Use format 'png' with delivery 'url'.", "unsupported_delivery_format")
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
            var png = FlowchartPngRenderer.Render(svg);
            if (string.Equals(delivery, "url", StringComparison.OrdinalIgnoreCase))
            {
                var storedResult = await renderResultStore.StoreAsync(
                    png,
                    "image/png",
                    TimeSpan.FromMinutes(options.Value.RenderResults.UrlLifetimeMinutes),
                    GetRequestBaseUri(httpRequest),
                    cancellationToken);

                return Results.Ok(new HostedRenderDiagramResponse(
                    storedResult.Id,
                    "png",
                    "image/png",
                    storedResult.Url,
                    storedResult.ExpiresAt));
            }

            return Results.File(png, "image/png");
        }
        catch (FlowchartPngRenderException)
        {
            return InvalidRequestProblem("Diagram could not be rendered as PNG.", [
                new DiagramProblemError("rendering", null, null, "Diagram could not be rendered as PNG.", "png_render_failed")
            ]);
        }
        catch (RenderResultStoreConfigurationException)
        {
            return HostedRenderStorageProblem("Hosted render storage is not configured correctly.");
        }
        catch (RenderResultStoreException)
        {
            return HostedRenderStorageProblem("The rendered diagram could not be stored.");
        }
        catch (Azure.RequestFailedException)
        {
            return HostedRenderStorageProblem("The rendered diagram could not be stored.");
        }
    }

    return Results.Text(svg, "image/svg+xml", Encoding.UTF8);
})
.WithName("RenderDiagram")
.WithTags("Diagrams")
.WithSummary("Render Enzo.Diagrams DSL.")
.WithDescription("Parses, validates, lays out, and renders the source DSL as SVG or PNG. The format field supports svg and png. Delivery defaults to raw bytes. Use format png with delivery url to receive a short-lived read-only image URL for the actual Enzo-rendered PNG. Syntax errors, validation errors, malformed JSON, missing source or format values, unsupported formats or delivery combinations, PNG rasterization failures, and hosted storage failures return ProblemDetails with an errors extension.")
.Accepts<RenderDiagramRequest>("application/json")
.Produces(StatusCodes.Status200OK)
.Produces<HostedRenderDiagramResponse>()
.ProducesProblem(StatusCodes.Status401Unauthorized)
.AddEndpointFilter<ApiKeyEndpointFilter>()
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
    content["application/json"] = new OpenApiMediaType
    {
        Schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Description = "Hosted PNG render result. The url points to the actual generated diagram image and can be presented to the user.",
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["id"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Opaque temporary render identifier." },
                ["format"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Render format. Hosted URL delivery currently returns png." },
                ["contentType"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Hosted image content type." },
                ["url"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uri", Description = "Short-lived read-only URL for the actual Enzo-rendered PNG." },
                ["expiresAt"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "date-time", Description = "UTC timestamp when the URL expires." }
            },
            Required = new HashSet<string> { "id", "format", "contentType", "url", "expiresAt" }
        }
    };

    return Task.CompletedTask;
})
.ProducesProblem(StatusCodes.Status503ServiceUnavailable)
.ProducesProblem(StatusCodes.Status400BadRequest);

app.MapGet("/v1/render-results/{id}", async (
    string id,
    ILocalRenderResultReader renderResultReader,
    CancellationToken cancellationToken) =>
{
    var result = await renderResultReader.GetAsync(id, cancellationToken);
    return result is null
        ? Results.NotFound()
        : Results.File(result.Bytes, result.ContentType);
})
.WithName("GetHostedRenderResult")
.ExcludeFromDescription();

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

static Task AddApiKeySecurityScheme(OpenApiDocument document, OpenApiDocumentTransformerContext _, CancellationToken cancellationToken)
{
    document.Components ??= new OpenApiComponents();
    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
    document.Components.SecuritySchemes["ApiKey"] = new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = ApiKeyEndpointFilter.HeaderName
    };
    AddApiKeySecurityRequirement(document, "/v1/validate");
    AddApiKeySecurityRequirement(document, "/v1/render");

    return Task.CompletedTask;
}

static void AddApiKeySecurityRequirement(OpenApiDocument document, string path)
{
    if (document.Paths is null
        || !document.Paths.TryGetValue(path, out var pathItem)
        || pathItem.Operations is null
        || !pathItem.Operations.TryGetValue(HttpMethod.Post, out var operation))
    {
        return;
    }

    operation.Security ??= [];
    operation.Security.Add(new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("ApiKey", document, null)] = []
    });
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

static bool TryGetRenderDelivery(string? value, out string delivery)
{
    if (value is null)
    {
        delivery = "raw";
        return true;
    }

    delivery = value.Trim();
    return string.Equals(delivery, "raw", StringComparison.OrdinalIgnoreCase)
        || string.Equals(delivery, "url", StringComparison.OrdinalIgnoreCase);
}

static bool ValidateRenderResultOptions(EnzoOptions options)
{
    var renderResults = options.RenderResults;
    if (renderResults.UrlLifetimeMinutes is < 1 or > 60)
    {
        return false;
    }

    if (string.Equals(renderResults.Store, "Local", StringComparison.OrdinalIgnoreCase))
    {
        return !string.IsNullOrWhiteSpace(renderResults.LocalDirectory);
    }

    return string.Equals(renderResults.Store, "AzureBlob", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(renderResults.BlobConnectionString)
        && !string.IsNullOrWhiteSpace(renderResults.BlobContainerName);
}

static Uri GetRequestBaseUri(HttpRequest request)
{
    var pathBase = request.PathBase.ToString().Trim('/');
    var basePath = string.IsNullOrEmpty(pathBase) ? "/" : $"/{pathBase}/";

    return new Uri($"{request.Scheme}://{request.Host}{basePath}");
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

static IResult HostedRenderStorageProblem(string detail)
{
    return Results.Problem(
        title: "Hosted render storage is unavailable.",
        detail: detail,
        statusCode: StatusCodes.Status503ServiceUnavailable,
        extensions: new Dictionary<string, object?>
        {
            ["errors"] = new[]
            {
                new DiagramProblemError("storage", null, null, detail, "hosted_render_storage_unavailable")
            }
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
