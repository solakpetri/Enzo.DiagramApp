using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
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

    [Fact]
    public async Task OpenApi_DocumentsPublicDiagramContract()
    {
        await using var factory = new WebApplicationFactory<global::Program>();
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
        AssertResponseContent(renderPost, "200", "image/svg+xml");
        AssertResponseContent(renderPost, "200", "image/png");
        AssertResponseContent(renderPost, "400", "application/problem+json");

        var renderRequestSchema = root.GetProperty("components").GetProperty("schemas").GetProperty("RenderDiagramRequest");
        Assert.Contains(renderRequestSchema.GetProperty("required").EnumerateArray(), property => property.GetString() == "source");
        Assert.Contains(renderRequestSchema.GetProperty("required").EnumerateArray(), property => property.GetString() == "format");
        Assert.Equal("^(svg|png)$", renderRequestSchema.GetProperty("properties").GetProperty("format").GetProperty("pattern").GetString());
        Assert.Equal("binary", GetResponseContent(renderPost, "200", "image/png").GetProperty("schema").GetProperty("format").GetString());
    }

    [Fact]
    public async Task Validate_ValidSource_ReturnsSuccess()
    {
        await using var factory = new WebApplicationFactory<global::Program>();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/v1/validate", new { source = ValidSource });

        response.EnsureSuccessStatusCode();
        await using var content = await response.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(content);
        Assert.True(json.RootElement.GetProperty("valid").GetBoolean());
    }

    [Fact]
    public async Task Validate_InvalidDsl_ReturnsProblemDetails()
    {
        await using var factory = new WebApplicationFactory<global::Program>();
        using var client = factory.CreateClient();

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
        await using var factory = new WebApplicationFactory<global::Program>();
        using var client = factory.CreateClient();

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
        await using var factory = new WebApplicationFactory<global::Program>();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/v1/render", new
        {
            source = ValidSource,
            format = "svg"
        });

        response.EnsureSuccessStatusCode();
        Assert.Equal("image/svg+xml", response.Content.Headers.ContentType?.MediaType);
        var svg = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("<?xml", svg);
        Assert.Contains("<svg", svg);
    }

    [Fact]
    public async Task Render_ValidSequence_ReturnsSvg()
    {
        await using var factory = new WebApplicationFactory<global::Program>();
        using var client = factory.CreateClient();

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
        await using var factory = new WebApplicationFactory<global::Program>();
        using var client = factory.CreateClient();

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
        await using var factory = new WebApplicationFactory<global::Program>();
        using var client = factory.CreateClient();

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
        await using var factory = new WebApplicationFactory<global::Program>();
        using var client = factory.CreateClient();

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

    [Fact]
    public async Task Validate_SequenceWithUnknownParticipant_ReturnsProblemDetails()
    {
        await using var factory = new WebApplicationFactory<global::Program>();
        using var client = factory.CreateClient();

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
    public async Task Render_MissingFormat_ReturnsProblemDetails()
    {
        await using var factory = new WebApplicationFactory<global::Program>();
        using var client = factory.CreateClient();

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
        await using var factory = new WebApplicationFactory<global::Program>();
        using var client = factory.CreateClient();

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
        await using var factory = new WebApplicationFactory<global::Program>();
        using var client = factory.CreateClient();
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
}
