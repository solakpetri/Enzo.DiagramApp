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
}
