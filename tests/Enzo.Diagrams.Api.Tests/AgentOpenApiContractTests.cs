using System.Text.Json;
using Xunit;

namespace Enzo.Diagrams.Api.Tests;

public sealed class AgentOpenApiContractTests
{
    [Fact]
    public void AgentOpenApiContract_IsValidAgentSchema()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "TestAssets",
            "agent.openapi.json")));
        var root = json.RootElement;

        Assert.Equal("3.1.0", root.GetProperty("openapi").GetString());
        Assert.Equal(
            "http://localhost:5085",
            root.GetProperty("servers")[0].GetProperty("url").GetString());

        var paths = root.GetProperty("paths");
        var validatePost = paths.GetProperty("/v1/validate").GetProperty("post");
        var renderPost = paths.GetProperty("/v1/render").GetProperty("post");

        Assert.Equal("validateDiagram", validatePost.GetProperty("operationId").GetString());
        Assert.Equal("renderDiagram", renderPost.GetProperty("operationId").GetString());
        AssertRequestSchema(validatePost, "ValidateDiagramRequest");
        AssertRequestSchema(renderPost, "RenderDiagramRequest");
        AssertResponseContent(renderPost, "200", "application/json");
        AssertApiKeySecurityScheme(root);
        AssertApiKeySecurityRequirement(validatePost);
        AssertApiKeySecurityRequirement(renderPost);

        var hostedResponse = root.GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("HostedRenderDiagramResponse");
        Assert.Contains("actual Enzo-rendered PNG", hostedResponse.GetProperty("properties").GetProperty("url").GetProperty("description").GetString());

        var sourceSchema = root.GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("EnzoDiagramSource");
        var description = sourceSchema.GetProperty("description").GetString()!;
        var examples = string.Join("\n", sourceSchema.GetProperty("examples").EnumerateArray().Select(example => example.GetString()));

        Assert.Contains("flow Example", description);
        Assert.Contains("start Begin \"Start\"", description);
        Assert.Contains("Source -> Target : label", description);
        Assert.Contains("The keyword node is invalid", description);
        Assert.Contains("Mermaid-style square-bracket node syntax must not be used", description);
        Assert.Contains("sequence Checkout", description);
        Assert.Contains("Api --> Web: 201 Created", description);
        Assert.Contains("bpmn Fulfillment", description);
        Assert.Contains("gateway Valid \"Order valid?\"", description);
        Assert.Contains("flow Example", examples);
        Assert.Contains("sequence Checkout", examples);
        Assert.Contains("bpmn Fulfillment", examples);
        Assert.DoesNotContain("graph TD", description);
        Assert.DoesNotContain("flowchart TD", description);
        Assert.DoesNotContain("A[", examples);
    }

    private static void AssertRequestSchema(JsonElement operation, string schemaName)
    {
        var schema = operation.GetProperty("requestBody")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema");

        Assert.Equal($"#/components/schemas/{schemaName}", schema.GetProperty("$ref").GetString());
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

    private static void AssertResponseContent(JsonElement operation, string statusCode, string mediaType)
    {
        Assert.True(operation.GetProperty("responses")
            .GetProperty(statusCode)
            .GetProperty("content")
            .GetProperty(mediaType)
            .ValueKind == JsonValueKind.Object);
    }

    private static void AssertApiKeySecurityRequirement(JsonElement operation)
    {
        var securityRequirement = Assert.Single(operation.GetProperty("security").EnumerateArray());
        var apiKeyRequirement = securityRequirement.GetProperty("ApiKey");

        Assert.Empty(apiKeyRequirement.EnumerateArray());
    }
}
