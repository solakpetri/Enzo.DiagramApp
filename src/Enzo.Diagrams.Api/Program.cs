using System.Text;
using System.Text.Json;
using Enzo.Diagrams.Api;
using Enzo.Diagrams.Application;
using Enzo.Diagrams.Domain;
using Enzo.Diagrams.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddOptions<EnzoOptions>()
    .Bind(builder.Configuration.GetSection(EnzoOptions.SectionName))
    .Validate(options => !builder.Environment.IsProduction() || options.ApiKeys.Count > 0,
        "Enzo:ApiKeys must contain at least one key in production.")
    .Validate(ValidateApiKeyOptions, "Enzo:ApiKeys is invalid.")
    .Validate(ValidateRenderResultOptions, "Enzo:RenderResults is invalid.")
    .Validate(options => ValidateRequestLimits(options.Limits), "Enzo:Limits is invalid.")
    .ValidateOnStart();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IDiagramRenderer, InfrastructureDiagramRenderer>();
builder.Services.AddScoped<DiagramService>();
builder.Services.AddSingleton(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<EnzoOptions>>().Value.RenderResults;
    var timeProvider = serviceProvider.GetRequiredService<TimeProvider>();

    return new LocalRenderResultStore(ToRenderResultStorageOptions(options), timeProvider);
});
builder.Services.AddSingleton<ILocalRenderResultReader>(serviceProvider =>
    serviceProvider.GetRequiredService<LocalRenderResultStore>());
builder.Services.AddSingleton<IRenderResultStore>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<EnzoOptions>>().Value.RenderResults;
    var timeProvider = serviceProvider.GetRequiredService<TimeProvider>();

    return string.Equals(options.Store, "AzureBlob", StringComparison.OrdinalIgnoreCase)
        ? new AzureBlobRenderResultStore(ToRenderResultStorageOptions(options), timeProvider)
        : serviceProvider.GetRequiredService<LocalRenderResultStore>();
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});
builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer(AddApiKeySecurityScheme);
});

var app = builder.Build();

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Results.Problem(
                title: "Server error.",
                detail: "An unexpected error occurred.",
                statusCode: StatusCodes.Status500InternalServerError).ExecuteAsync(context);
        });
    });
}

app.MapOpenApi();

app.MapPost("/v1/validate", async (
    HttpRequest httpRequest,
    DiagramService diagrams,
    IOptions<EnzoOptions> options,
    CancellationToken cancellationToken) =>
{
    var limits = options.Value.Limits;
    var (request, readError) = await ReadRequestAsync<ValidateDiagramRequest>(httpRequest, limits, cancellationToken);
    if (readError is not null)
    {
        return readError;
    }

    if (!TryGetSource(request!.Source, limits, out var source, out var inputError))
    {
        return inputError;
    }

    var result = diagrams.Validate(source, ToDiagramComplexityLimits(limits));

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
.WithMetadata(new ApiKeyScopeRequirement(ApiKeyScopes.Validate))
.AddEndpointFilter<ApiKeyEndpointFilter>()
.ProducesProblem(StatusCodes.Status413PayloadTooLarge)
.ProducesProblem(StatusCodes.Status400BadRequest);

app.MapPost("/v1/validate/batch", async (
    HttpRequest httpRequest,
    DiagramService diagrams,
    IOptions<EnzoOptions> options,
    CancellationToken cancellationToken) =>
{
    var limits = options.Value.Limits;
    var (request, readError) = await ReadRequestAsync<BatchValidateDiagramRequest>(httpRequest, limits, cancellationToken);
    if (readError is not null)
    {
        return readError;
    }

    if (!TryGetBatchValidationItems(request!, limits, out var items, out var inputError))
    {
        return inputError;
    }

    var results = diagrams.ValidateBatch(items, ToDiagramComplexityLimits(limits));
    var responseItems = results.Select(result => new BatchValidateDiagramItemResponse(
        result.Id,
        result.Validation.IsSuccess,
        result.Validation.IsSuccess ? [] : ToDiagramErrors(result.Validation))).ToArray();

    return Results.Ok(new BatchValidateDiagramResponse(
        responseItems.Length,
        responseItems.Count(item => item.Valid),
        responseItems));
})
.WithName("BatchValidateDiagrams")
.WithTags("Diagrams")
.WithSummary("Validate multiple Enzo.Diagrams DSL documents.")
.WithDescription("Parses and validates up to 50 source DSL documents in one request. Request shape errors return ProblemDetails. Per-diagram syntax, validation, and complexity errors are returned in the item results.")
.Accepts<BatchValidateDiagramRequest>("application/json")
.Produces<BatchValidateDiagramResponse>()
.ProducesProblem(StatusCodes.Status401Unauthorized)
.WithMetadata(new ApiKeyScopeRequirement(ApiKeyScopes.Validate))
.AddEndpointFilter<ApiKeyEndpointFilter>()
.ProducesProblem(StatusCodes.Status413PayloadTooLarge)
.ProducesProblem(StatusCodes.Status400BadRequest);

app.MapPost("/v1/render", async (
    HttpRequest httpRequest,
    DiagramService diagrams,
    IRenderResultStore renderResultStore,
    IOptions<EnzoOptions> options,
    CancellationToken cancellationToken) =>
{
    var limits = options.Value.Limits;
    var (request, readError) = await ReadRequestAsync<RenderDiagramRequest>(httpRequest, limits, cancellationToken);
    if (readError is not null)
    {
        return readError;
    }

    if (!TryGetSource(request!.Source, limits, out var source, out var inputError))
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

    var format = string.Equals(request.Format, "png", StringComparison.OrdinalIgnoreCase)
        ? DiagramRenderFormat.Png
        : DiagramRenderFormat.Svg;
    DiagramRenderResult result;
    try
    {
        result = diagrams.Render(
            source,
            format,
            ToDiagramComplexityLimits(limits),
            new PngRenderOptions(limits.MaxPngWidth, limits.MaxPngHeight, limits.MaxPngPixels));
    }
    catch (DiagramPngRenderException)
    {
        return InvalidRequestProblem("Diagram could not be rendered as PNG.", [
            new DiagramProblemError("rendering", null, null, "Diagram could not be rendered as PNG.", "png_render_failed")
        ]);
    }

    if (!result.IsSuccess)
    {
        return DiagramProblem(result.Validation);
    }

    if (string.Equals(request.Format, "png", StringComparison.OrdinalIgnoreCase))
    {
        try
        {
            var png = result.Png!;
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

            SetImageSecurityHeaders(httpRequest.HttpContext.Response);
            return Results.File(png, "image/png");
        }
        catch (RenderResultStoreConfigurationException)
        {
            return HostedRenderStorageProblem("Hosted render storage is not configured correctly.");
        }
        catch (RenderResultStoreException)
        {
            return HostedRenderStorageProblem("The rendered diagram could not be stored.");
        }
    }

    SetSvgSecurityHeaders(httpRequest.HttpContext.Response);
    return Results.Text(result.Svg!, "image/svg+xml", Encoding.UTF8);
})
.WithName("RenderDiagram")
.WithTags("Diagrams")
.WithSummary("Render Enzo.Diagrams DSL.")
.WithDescription("Parses, validates, lays out, and renders the source DSL as SVG or PNG. The format field supports svg and png. Delivery defaults to raw bytes. Use format png with delivery url to receive a short-lived read-only image URL for the actual Enzo-rendered PNG. Syntax errors, validation errors, malformed JSON, missing source or format values, unsupported formats or delivery combinations, PNG rasterization failures, and hosted storage failures return ProblemDetails with an errors extension.")
.Accepts<RenderDiagramRequest>("application/json")
.Produces(StatusCodes.Status200OK)
.Produces<HostedRenderDiagramResponse>()
.ProducesProblem(StatusCodes.Status401Unauthorized)
.WithMetadata(new ApiKeyScopeRequirement(ApiKeyScopes.Render))
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
.ProducesProblem(StatusCodes.Status413PayloadTooLarge)
.ProducesProblem(StatusCodes.Status400BadRequest);

app.MapGet("/v1/render-results/{id:regex(^[a-f0-9]{{32}}$)}", async (
    string id,
    HttpResponse response,
    ILocalRenderResultReader renderResultReader,
    CancellationToken cancellationToken) =>
{
    var result = await renderResultReader.GetAsync(id, cancellationToken);
    if (result is not null)
    {
        SetImageSecurityHeaders(response);
        response.Headers["Cache-Control"] = "no-store, max-age=0";
    }

    return result is null
        ? Results.NotFound()
        : Results.File(result.Bytes, result.ContentType);
})
.WithName("GetHostedRenderResult")
.ExcludeFromDescription();

app.Run();

static async Task<(T? Request, IResult? Error)> ReadRequestAsync<T>(
    HttpRequest httpRequest,
    RequestLimitOptions limits,
    CancellationToken cancellationToken)
{
    if (httpRequest.ContentLength > limits.MaxRequestBodyBytes)
    {
        return (default, PayloadTooLargeProblem(limits.MaxRequestBodyBytes));
    }

    try
    {
        await using var buffer = new MemoryStream();
        var bytesRead = 0;
        var chunk = new byte[8192];
        while (true)
        {
            var read = await httpRequest.Body.ReadAsync(chunk, cancellationToken);
            if (read == 0)
            {
                break;
            }

            bytesRead += read;
            if (bytesRead > limits.MaxRequestBodyBytes)
            {
                return (default, PayloadTooLargeProblem(limits.MaxRequestBodyBytes));
            }

            buffer.Write(chunk, 0, read);
        }

        buffer.Position = 0;
        var request = await JsonSerializer.DeserializeAsync<T>(buffer, JsonSerializerOptions.Web, cancellationToken);
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
    AddApiKeySecurityRequirement(document, "/v1/validate/batch");
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

static bool TryGetSource(string? value, RequestLimitOptions limits, out string source, out IResult error)
{
    source = value ?? string.Empty;
    if (string.IsNullOrWhiteSpace(value))
    {
        error = InvalidRequestProblem("Source is required.", [
            new DiagramProblemError("request", null, null, "Source is required.", "required")
        ]);
        return false;
    }

    if (value.Length > limits.MaxSourceCharacters)
    {
        error = InvalidRequestProblem("Source is too large.", [
            new DiagramProblemError("request", null, null, $"Source must be at most {limits.MaxSourceCharacters} characters.", "source_too_large")
        ]);
        return false;
    }

    if (value.Count(character => character == '\n') + 1 > limits.MaxSourceLines)
    {
        error = InvalidRequestProblem("Source contains too many lines.", [
            new DiagramProblemError("request", null, null, $"Source must contain at most {limits.MaxSourceLines} lines.", "source_too_large")
        ]);
        return false;
    }

    error = Results.Empty;
    return true;
}

static DiagramComplexityLimits ToDiagramComplexityLimits(RequestLimitOptions limits)
{
    return new DiagramComplexityLimits(limits.MaxDiagramElements, limits.MaxDiagramConnections);
}

static bool TryGetBatchValidationItems(
    BatchValidateDiagramRequest request,
    RequestLimitOptions limits,
    out IReadOnlyList<DiagramBatchValidationRequest> items,
    out IResult error)
{
    const int maxBatchValidationItems = 50;
    items = [];
    if (request.Items is null || request.Items.Count == 0)
    {
        error = InvalidRequestProblem("At least one batch item is required.", [
            new DiagramProblemError("request", null, null, "Items must contain at least one diagram.", "required")
        ]);
        return false;
    }

    if (request.Items.Count > maxBatchValidationItems)
    {
        error = InvalidRequestProblem("Too many batch items.", [
            new DiagramProblemError("request", null, null, $"Items must contain at most {maxBatchValidationItems} diagrams.", "batch_too_large")
        ]);
        return false;
    }

    var validationItems = new List<DiagramBatchValidationRequest>(request.Items.Count);
    for (var index = 0; index < request.Items.Count; index++)
    {
        var item = request.Items[index];
        if (!TryGetSource(item.Source, limits, out var source, out error))
        {
            return false;
        }

        validationItems.Add(new DiagramBatchValidationRequest(
            string.IsNullOrWhiteSpace(item.Id) ? index.ToString() : item.Id.Trim(),
            source));
    }

    items = validationItems;
    error = Results.Empty;
    return true;
}

static RenderResultStorageOptions ToRenderResultStorageOptions(RenderResultOptions options)
{
    return new RenderResultStorageOptions
    {
        Store = options.Store,
        BlobConnectionString = options.BlobConnectionString,
        BlobContainerName = options.BlobContainerName,
        LocalDirectory = options.LocalDirectory
    };
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

static bool ValidateApiKeyOptions(EnzoOptions options)
{
    var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var apiKey in options.ApiKeys)
    {
        if (string.IsNullOrWhiteSpace(apiKey.Id)
            || !ids.Add(apiKey.Id)
            || !IsSha256Hex(apiKey.Sha256)
            || apiKey.Scopes.Length == 0
            || apiKey.Scopes.Any(scope => !IsKnownApiKeyScope(scope)))
        {
            return false;
        }
    }

    return true;
}

static bool IsSha256Hex(string value)
{
    if (value.Length != 64)
    {
        return false;
    }

    try
    {
        return Convert.FromHexString(value).Length == 32;
    }
    catch (FormatException)
    {
        return false;
    }
    catch (ArgumentException)
    {
        return false;
    }
}

static bool IsKnownApiKeyScope(string scope)
{
    return string.Equals(scope, ApiKeyScopes.All, StringComparison.OrdinalIgnoreCase)
        || string.Equals(scope, ApiKeyScopes.Validate, StringComparison.OrdinalIgnoreCase)
        || string.Equals(scope, ApiKeyScopes.Render, StringComparison.OrdinalIgnoreCase);
}

static bool ValidateRequestLimits(RequestLimitOptions limits)
{
    return limits.MaxRequestBodyBytes is >= 1024 and <= 1024 * 1024
        && limits.MaxSourceCharacters is >= 1024 and <= 256 * 1024
        && limits.MaxSourceLines is >= 10 and <= 10_000
        && limits.MaxDiagramElements is >= 10 and <= 5_000
        && limits.MaxDiagramConnections is >= 10 and <= 10_000
        && limits.MaxPngWidth is >= 100 and <= 20_000
        && limits.MaxPngHeight is >= 100 and <= 20_000
        && limits.MaxPngPixels is >= 10_000 and <= 100_000_000;
}

static Uri GetRequestBaseUri(HttpRequest request)
{
    var pathBase = request.PathBase.ToString().Trim('/');
    var basePath = string.IsNullOrEmpty(pathBase) ? "/" : $"/{pathBase}/";

    return new Uri($"{request.Scheme}://{request.Host}{basePath}");
}

static IResult DiagramProblem(DiagramValidationResult result)
{
    if (result.LimitViolation is not null)
    {
        return InvalidRequestProblem("Diagram is too complex.", [
            new DiagramProblemError("request", null, null, result.LimitViolation.Message, result.LimitViolation.Code)
        ]);
    }

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

static IResult PayloadTooLargeProblem(int maxRequestBodyBytes)
{
    return Results.Problem(
        title: "Request body is too large.",
        detail: $"Request body must be at most {maxRequestBodyBytes} bytes.",
        statusCode: StatusCodes.Status413PayloadTooLarge,
        extensions: new Dictionary<string, object?>
        {
            ["errors"] = new[]
            {
                new DiagramProblemError("request", null, null, "Request body exceeds the configured limit.", "request_too_large")
            }
        });
}

static void SetSvgSecurityHeaders(HttpResponse response)
{
    SetImageSecurityHeaders(response);
    response.Headers["Content-Security-Policy"] = "default-src 'none'; img-src 'none'; script-src 'none'; object-src 'none'; style-src 'unsafe-inline'";
}

static void SetImageSecurityHeaders(HttpResponse response)
{
    response.Headers["X-Content-Type-Options"] = "nosniff";
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

static IReadOnlyList<DiagramProblemError> ToDiagramErrors(DiagramValidationResult result)
{
    if (result.LimitViolation is not null)
    {
        return [new DiagramProblemError("request", null, null, result.LimitViolation.Message, result.LimitViolation.Code)];
    }

    var errors = new List<DiagramProblemError>();

    errors.AddRange(result.ParseResult.Errors.Select(error =>
        new DiagramProblemError("syntax", error.Line, error.Column, error.Message)));

    errors.AddRange(result.ParseResult.ValidationErrors.Select(error =>
        new DiagramProblemError("validation", error.Line, error.Column, error.Message, error.Kind)));

    return errors;
}

public partial class Program;
