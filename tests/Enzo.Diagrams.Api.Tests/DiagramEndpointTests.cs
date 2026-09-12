using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Enzo.Diagrams.Api.Tests;

public sealed class DiagramEndpointTests
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private const string ValidSource = """
        flow Checkout
        start Begin "Order received"
        task Validate "Validate order"
        end Complete "Complete order"
        Begin -> Validate
        Validate -> Complete
        """;

    private const string ValidSequenceSource = """
        sequence Checkout
        actor Customer
        participant API
        participant Payment
        Customer -> API: Checkout
        API -> Payment: Charge
        Payment --> API: Success
        API --> Customer: Confirmed
        """;

    private const string ValidBpmnSource = """
        bpmn Order
        start Received
        task Validate "Validate order"
        gateway Available "Stock available?"
        end Complete
        Received -> Validate
        Validate -> Available
        Available -> Complete : yes
        """;

    private const string ApiKey = "test-api-key";

    [Fact]
    public async Task OpenApi_DocumentsPublicDiagramContract()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        response.EnsureSuccessStatusCode();
        await using var content = await response.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(content);
        var root = json.RootElement;
        var paths = root.GetProperty("paths");
        var validatePost = paths.GetProperty("/v1/validate").GetProperty("post");
        var renderPost = paths.GetProperty("/v1/render").GetProperty("post");

        Assert.Equal("Validate Enzo.Diagrams DSL.", validatePost.GetProperty("summary").GetString());
        Assert.Equal("Render Enzo.Diagrams DSL.", renderPost.GetProperty("summary").GetString());
        Assert.Contains("source DSL", renderPost.GetProperty("description").GetString());
        Assert.Contains("svg and png", renderPost.GetProperty("description").GetString());
        AssertRequestSchema(validatePost, "ValidateDiagramRequest");
        AssertRequestSchema(renderPost, "RenderDiagramRequest");
        AssertResponseContent(validatePost, "200", "application/json");
        AssertResponseContent(validatePost, "400", "application/problem+json");
        AssertResponseContent(validatePost, "413", "application/problem+json");
        AssertResponseContent(renderPost, "200", "image/svg+xml");
        AssertResponseContent(renderPost, "200", "image/png");
        AssertResponseContent(renderPost, "200", "application/json");
        AssertResponseContent(renderPost, "400", "application/problem+json");
        AssertResponseContent(renderPost, "413", "application/problem+json");
        AssertApiKeySecurityScheme(root);
        AssertApiKeySecurityRequirement(validatePost);
        AssertApiKeySecurityRequirement(renderPost);

        var renderRequestSchema = root.GetProperty("components").GetProperty("schemas").GetProperty("RenderDiagramRequest");
        Assert.Contains(renderRequestSchema.GetProperty("required").EnumerateArray(), property => property.GetString() == "source");
        Assert.Contains(renderRequestSchema.GetProperty("required").EnumerateArray(), property => property.GetString() == "format");
        Assert.Equal("^(svg|png)$", renderRequestSchema.GetProperty("properties").GetProperty("format").GetProperty("pattern").GetString());
        Assert.Equal("^(raw|url)$", renderRequestSchema.GetProperty("properties").GetProperty("delivery").GetProperty("pattern").GetString());
        Assert.Equal("binary", GetResponseContent(renderPost, "200", "image/png").GetProperty("schema").GetProperty("format").GetString());
        Assert.Contains(
            "actual generated diagram image",
            GetResponseContent(renderPost, "200", "application/json").GetProperty("schema").GetProperty("description").GetString());
    }

    [Fact]
    public async Task OpenApi_UsesForwardedHttpsScheme()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/openapi/v1.json");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");

        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        await using var content = await response.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(content);

        Assert.StartsWith("https://", json.RootElement.GetProperty("servers")[0].GetProperty("url").GetString());
    }

    [Fact]
    public async Task OpenApi_RemainsAccessibleWithoutApiKey()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Validate_ValidSource_ReturnsSuccess()
    {
        await using var factory = CreateFactory();
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync("/v1/validate", new { source = ValidSource });

        response.EnsureSuccessStatusCode();
        await using var content = await response.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(content);
        Assert.True(json.RootElement.GetProperty("valid").GetBoolean());
    }

    [Fact]
    public async Task Validate_InvalidDsl_ReturnsProblemDetails()
    {
        await using var factory = CreateFactory();
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync("/v1/validate", new
        {
            source = "flow Checkout\nstart Begin \"Start\""
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        await using var content = await response.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(content);
        Assert.Equal("Diagram DSL is invalid.", json.RootElement.GetProperty("title").GetString());
        Assert.Contains(json.RootElement.GetProperty("errors").EnumerateArray(), error =>
            error.GetProperty("code").GetString() == "MissingEndNode");
    }

    [Fact]
    public async Task Validate_SyntaxError_ReturnsProblemDetails()
    {
        await using var factory = CreateFactory();
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync("/v1/validate", new
        {
            source = "flow Checkout\ntask"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        await using var content = await response.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(content);
        Assert.Contains(json.RootElement.GetProperty("errors").EnumerateArray(), error =>
            error.GetProperty("type").GetString() == "syntax"
            && error.GetProperty("line").GetInt32() == 2);
    }

    [Fact]
    public async Task Render_ValidSource_ReturnsSvg()
    {
        await using var factory = CreateFactory();
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync("/v1/render", new
        {
            source = ValidSource,
            format = "svg"
        });

        response.EnsureSuccessStatusCode();
        Assert.Equal("image/svg+xml", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("default-src 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
        var svg = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("<?xml", svg);
        Assert.Contains("<svg", svg);
    }

    [Fact]
    public async Task Render_ValidSequence_ReturnsSvg()
    {
        await using var factory = CreateFactory();
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync("/v1/render", new
        {
            source = ValidSequenceSource,
            format = "svg"
        });

        response.EnsureSuccessStatusCode();
        Assert.Equal("image/svg+xml", response.Content.Headers.ContentType?.MediaType);
        var svg = await response.Content.ReadAsStringAsync();
        Assert.Contains("Customer", svg);
        Assert.Contains("Confirmed", svg);
    }

    [Fact]
    public async Task Render_ValidBpmnSubset_ReturnsSvg()
    {
        await using var factory = CreateFactory();
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync("/v1/render", new
        {
            source = ValidBpmnSource,
            format = "svg"
        });

        response.EnsureSuccessStatusCode();
        var svg = await response.Content.ReadAsStringAsync();
        Assert.Contains("Stock available?", svg);
        Assert.Contains("<polygon", svg);
    }

    [Fact]
    public async Task Validate_UnsupportedBpmnElement_ReturnsProblemDetails()
    {
        await using var factory = CreateFactory();
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync("/v1/validate", new
        {
            source = "bpmn Order\npool Sales\nstart Received\nend Complete"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var content = await response.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(content);
        Assert.Contains(json.RootElement.GetProperty("errors").EnumerateArray(), error =>
            error.GetProperty("message").GetString()!.Contains("sequence flow"));
    }

    [Fact]
    public async Task Render_PngFormat_ReturnsPng()
    {
        await using var factory = CreateFactory();
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync("/v1/render", new
        {
            source = ValidSource,
            format = "png"
        });

        response.EnsureSuccessStatusCode();
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        var png = await response.Content.ReadAsByteArrayAsync();
        Assert.True(png.Take(PngSignature.Length).SequenceEqual(PngSignature));
    }

    [Theory]
    [InlineData(ValidSource)]
    [InlineData(ValidSequenceSource)]
    [InlineData(ValidBpmnSource)]
    public async Task Render_HostedPngDelivery_ReturnsTemporaryImageUrl(string source)
    {
        await using var factory = CreateFactory();
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync("/v1/render", new
        {
            source,
            format = "png",
            delivery = "url"
        });

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        await using var content = await response.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(content);
        var root = json.RootElement;

        Assert.Equal("png", root.GetProperty("format").GetString());
        Assert.Equal("image/png", root.GetProperty("contentType").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("id").GetString()));
        Assert.True(root.GetProperty("expiresAt").GetDateTimeOffset() > DateTimeOffset.UtcNow);

        var imageResponse = await client.GetAsync(root.GetProperty("url").GetString());
        imageResponse.EnsureSuccessStatusCode();
        Assert.Equal("image/png", imageResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal("nosniff", imageResponse.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("no-store, max-age=0", imageResponse.Headers.CacheControl?.ToString());
        var png = await imageResponse.Content.ReadAsByteArrayAsync();
        Assert.True(png.Take(PngSignature.Length).SequenceEqual(PngSignature));
    }

    [Fact]
    public async Task HostedRenderResult_InvalidIdShape_ReturnsNotFound()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/v1/render-results/not-an-id");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Render_HostedPngDelivery_PassesRenderedBytesToStorage()
    {
        var store = new CapturingRenderResultStore();
        await using var factory = CreateFactory(store);
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync("/v1/render", new
        {
            source = ValidSource,
            format = "png",
            delivery = "url"
        });

        response.EnsureSuccessStatusCode();
        Assert.NotNull(store.Bytes);
        Assert.NotEmpty(store.Bytes);
        Assert.True(store.Bytes.Take(PngSignature.Length).SequenceEqual(PngSignature));
        Assert.Equal("image/png", store.ContentType);
        Assert.Equal(TimeSpan.FromMinutes(30), store.Lifetime);
        Assert.NotNull(store.RequestBaseUri);
    }

    [Fact]
    public async Task Render_StorageFailure_ReturnsSanitizedProblemDetails()
    {
        const string secret = "DefaultEndpointsProtocol=https;AccountKey=secret-account-key";
        await using var factory = CreateFactory(new FailingRenderResultStore(secret));
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync("/v1/render", new
        {
            source = ValidSource,
            format = "png",
            delivery = "url"
        });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Hosted render storage is unavailable.", body);
        Assert.DoesNotContain(secret, body);
        Assert.DoesNotContain("AccountKey", body);
    }

    [Fact]
    public async Task Render_UnexpectedProductionFailure_ReturnsGenericProblemDetails()
    {
        const string secret = "sensitive-storage-detail";
        await using var factory = CreateProductionFactory(new UnexpectedFailingRenderResultStore(secret));
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync("/v1/render", new
        {
            source = ValidSource,
            format = "png",
            delivery = "url"
        });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("An unexpected error occurred.", body);
        Assert.DoesNotContain(secret, body);
    }

    [Fact]
    public async Task Render_SvgUrlDelivery_ReturnsProblemDetails()
    {
        await using var factory = CreateFactory();
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync("/v1/render", new
        {
            source = ValidSource,
            format = "svg",
            delivery = "url"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var content = await response.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(content);
        Assert.Contains(json.RootElement.GetProperty("errors").EnumerateArray(), error =>
            error.GetProperty("code").GetString() == "unsupported_delivery_format");
    }

    [Fact]
    public async Task Render_BlankDelivery_ReturnsProblemDetails()
    {
        await using var factory = CreateFactory();
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync("/v1/render", new
        {
            source = ValidSource,
            format = "png",
            delivery = " "
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var content = await response.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(content);
        Assert.Contains(json.RootElement.GetProperty("errors").EnumerateArray(), error =>
            error.GetProperty("code").GetString() == "unsupported_delivery");
    }

    [Fact]
    public void RenderResultIds_AreOpaqueAndNonSequential()
    {
        var first = RenderResultIds.Create();
        var second = RenderResultIds.Create();

        Assert.Matches("^[a-f0-9]{32}$", first);
        Assert.Matches("^[a-f0-9]{32}$", second);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void InvalidAzureBlobStorageConfiguration_FailsClearlyWithoutSecrets()
    {
        using var factory = CreateFactory(("Enzo:RenderResults:Store", "AzureBlob"));

        var exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("Enzo:RenderResults is invalid.", exception.Message);
        Assert.DoesNotContain("ConnectionString", exception.Message);
        Assert.DoesNotContain("AccountKey", exception.Message);
    }

    [Fact]
    public async Task Validate_SequenceWithUnknownParticipant_ReturnsProblemDetails()
    {
        await using var factory = CreateFactory();
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync("/v1/validate", new
        {
            source = "sequence Checkout\nactor Customer\nCustomer -> API: Checkout"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var content = await response.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(content);
        Assert.Contains(json.RootElement.GetProperty("errors").EnumerateArray(), error =>
            error.GetProperty("code").GetString() == "UnknownMessageTarget");
    }

    [Fact]
    public async Task Validate_RequestBodyOverLimit_ReturnsPayloadTooLarge()
    {
        await using var factory = CreateFactory(("Enzo:Limits:MaxRequestBodyBytes", "1024"));
        using var client = CreateAuthenticatedClient(factory);
        using var content = new StringContent("{\"source\":\"" + new string('a', 2_000) + "\"}", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/v1/validate", content);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        await using var responseContent = await response.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(responseContent);
        Assert.Contains(json.RootElement.GetProperty("errors").EnumerateArray(), error =>
            error.GetProperty("code").GetString() == "request_too_large");
    }

    [Fact]
    public async Task Validate_SourceOverLimit_ReturnsProblemDetails()
    {
        await using var factory = CreateFactory(("Enzo:Limits:MaxSourceCharacters", "1024"));
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync("/v1/validate", new { source = new string('a', 1_025) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var content = await response.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(content);
        Assert.Contains(json.RootElement.GetProperty("errors").EnumerateArray(), error =>
            error.GetProperty("code").GetString() == "source_too_large");
    }

    [Fact]
    public async Task Validate_DiagramOverElementLimit_ReturnsProblemDetails()
    {
        await using var factory = CreateFactory(("Enzo:Limits:MaxDiagramElements", "10"));
        using var client = CreateAuthenticatedClient(factory);
        var source = "sequence Big\n" + string.Join("\n", Enumerable.Range(0, 11).Select(index => $"participant P{index}")) + "\nP0 -> P1: Request";

        var response = await client.PostAsJsonAsync("/v1/validate", new { source });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var content = await response.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(content);
        Assert.Contains(json.RootElement.GetProperty("errors").EnumerateArray(), error =>
            error.GetProperty("code").GetString() == "diagram_too_complex");
    }

    [Fact]
    public async Task Render_PngOverPixelLimit_ReturnsProblemDetails()
    {
        await using var factory = CreateFactory(("Enzo:Limits:MaxPngPixels", "10000"));
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync("/v1/render", new
        {
            source = ValidSource,
            format = "png"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var content = await response.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(content);
        Assert.Contains(json.RootElement.GetProperty("errors").EnumerateArray(), error =>
            error.GetProperty("code").GetString() == "png_render_failed");
    }

    [Fact]
    public async Task Render_MissingFormat_ReturnsProblemDetails()
    {
        await using var factory = CreateFactory();
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync("/v1/render", new { source = ValidSource });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var content = await response.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(content);
        Assert.Contains(json.RootElement.GetProperty("errors").EnumerateArray(), error =>
            error.GetProperty("code").GetString() == "required");
    }

    [Fact]
    public async Task Render_UnsupportedFormat_ReturnsProblemDetails()
    {
        await using var factory = CreateFactory();
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync("/v1/render", new
        {
            source = ValidSource,
            format = "pdf"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var content = await response.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(content);
        Assert.Contains(json.RootElement.GetProperty("errors").EnumerateArray(), error =>
            error.GetProperty("code").GetString() == "unsupported_format");
    }

    [Fact]
    public async Task Validate_MalformedRequest_ReturnsProblemDetails()
    {
        await using var factory = CreateFactory();
        using var client = CreateAuthenticatedClient(factory);
        using var content = new StringContent("{", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/v1/validate", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        await using var responseContent = await response.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(responseContent);
        Assert.Equal("Invalid request.", json.RootElement.GetProperty("title").GetString());
        Assert.Contains(json.RootElement.GetProperty("errors").EnumerateArray(), error =>
            error.GetProperty("code").GetString() == "malformed_json");
    }

    [Fact]
    public async Task Validate_MissingApiKey_ReturnsUnauthorized()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/v1/validate", new { source = ValidSource });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Validate_EmptyApiKey_ReturnsUnauthorized()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/validate")
        {
            Content = JsonContent.Create(new { source = ValidSource })
        };
        request.Headers.TryAddWithoutValidation("X-API-Key", string.Empty);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Validate_InvalidApiKey_ReturnsUnauthorizedWithoutExposingKeys()
    {
        const string invalidApiKey = "wrong-api-key";
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/validate")
        {
            Content = JsonContent.Create(new { source = ValidSource })
        };
        request.Headers.Add("X-API-Key", invalidApiKey);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(ApiKey, body);
        Assert.DoesNotContain(invalidApiKey, body);
    }

    [Fact]
    public async Task Validate_MultipleApiKeyHeaders_ReturnsUnauthorized()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/validate")
        {
            Content = JsonContent.Create(new { source = ValidSource })
        };
        request.Headers.Add("X-API-Key", [ApiKey, ApiKey]);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void Production_BlankApiKey_FailsStartup()
    {
        using var factory = new WebApplicationFactory<global::Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Production"));

        var exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("Enzo:ApiKey must be configured in production.", exception.Message);
    }

    [Fact]
    public async Task Render_CorrectApiKey_ReturnsSvg()
    {
        await using var factory = CreateFactory();
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync("/v1/render", new
        {
            source = ValidSource,
            format = "svg"
        });

        response.EnsureSuccessStatusCode();
        Assert.Equal("image/svg+xml", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Render_InvalidApiKey_ReturnsUnauthorized()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/render")
        {
            Content = JsonContent.Create(new { source = ValidSource, format = "svg" })
        };
        request.Headers.Add("X-API-Key", "wrong-api-key");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static void AssertRequestSchema(JsonElement operation, string schemaName)
    {
        var schema = operation.GetProperty("requestBody")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema");

        Assert.Equal($"#/components/schemas/{schemaName}", schema.GetProperty("$ref").GetString());
    }

    private static void AssertResponseContent(JsonElement operation, string statusCode, string mediaType)
    {
        Assert.True(GetResponseContent(operation, statusCode, mediaType).ValueKind == JsonValueKind.Object);
    }

    private static JsonElement GetResponseContent(JsonElement operation, string statusCode, string mediaType)
    {
        return operation.GetProperty("responses")
            .GetProperty(statusCode)
            .GetProperty("content")
            .GetProperty(mediaType);
    }

    private static void AssertApiKeySecurityScheme(JsonElement root)
    {
        var scheme = root.GetProperty("components")
            .GetProperty("securitySchemes")
            .GetProperty("ApiKey");

        Assert.Equal("apiKey", scheme.GetProperty("type").GetString());
        Assert.Equal("header", scheme.GetProperty("in").GetString());
        Assert.Equal("X-API-Key", scheme.GetProperty("name").GetString());
    }

    private static void AssertApiKeySecurityRequirement(JsonElement operation)
    {
        var securityRequirement = Assert.Single(operation.GetProperty("security").EnumerateArray());
        var apiKeyRequirement = securityRequirement.GetProperty("ApiKey");

        Assert.Empty(apiKeyRequirement.EnumerateArray());
    }

    private static WebApplicationFactory<global::Program> CreateFactory()
    {
        return new WebApplicationFactory<global::Program>()
            .WithWebHostBuilder(builder => builder.UseSetting("Enzo:ApiKey", ApiKey));
    }

    private static WebApplicationFactory<global::Program> CreateFactory(IRenderResultStore store)
    {
        return new WebApplicationFactory<global::Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Enzo:ApiKey", ApiKey);
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IRenderResultStore>();
                    services.AddSingleton(store);
                });
            });
    }

    private static WebApplicationFactory<global::Program> CreateProductionFactory(IRenderResultStore store)
    {
        return new WebApplicationFactory<global::Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.UseSetting("Enzo:ApiKey", ApiKey);
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IRenderResultStore>();
                    services.AddSingleton(store);
                });
            });
    }

    private static WebApplicationFactory<global::Program> CreateFactory(params (string Key, string Value)[] settings)
    {
        return new WebApplicationFactory<global::Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Enzo:ApiKey", ApiKey);
                foreach (var (key, value) in settings)
                {
                    builder.UseSetting(key, value);
                }
            });
    }

    private static HttpClient CreateAuthenticatedClient(WebApplicationFactory<global::Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", ApiKey);
        return client;
    }

    private sealed class CapturingRenderResultStore : IRenderResultStore
    {
        public byte[] Bytes { get; private set; } = [];

        public string? ContentType { get; private set; }

        public TimeSpan Lifetime { get; private set; }

        public Uri? RequestBaseUri { get; private set; }

        public ValueTask<StoredRenderResult> StoreAsync(
            byte[] bytes,
            string contentType,
            TimeSpan lifetime,
            Uri requestBaseUri,
            CancellationToken cancellationToken)
        {
            Bytes = bytes;
            ContentType = contentType;
            Lifetime = lifetime;
            RequestBaseUri = requestBaseUri;
            return ValueTask.FromResult(new StoredRenderResult(
                RenderResultIds.Create(),
                "https://storage.example.invalid/render-results/test.png?sv=redacted",
                DateTimeOffset.UtcNow.Add(lifetime)));
        }
    }

    private sealed class FailingRenderResultStore(string message) : IRenderResultStore
    {
        public ValueTask<StoredRenderResult> StoreAsync(
            byte[] bytes,
            string contentType,
            TimeSpan lifetime,
            Uri requestBaseUri,
            CancellationToken cancellationToken)
        {
            throw new RenderResultStoreException(message);
        }
    }

    private sealed class UnexpectedFailingRenderResultStore(string message) : IRenderResultStore
    {
        public ValueTask<StoredRenderResult> StoreAsync(
            byte[] bytes,
            string contentType,
            TimeSpan lifetime,
            Uri requestBaseUri,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException(message);
        }
    }
}
