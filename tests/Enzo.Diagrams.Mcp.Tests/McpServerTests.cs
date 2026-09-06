using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using ModelContextProtocol.Client;
using Xunit;

namespace Enzo.Diagrams.Mcp.Tests;

public sealed class McpServerTests
{
    [Fact]
    public async Task Health_ReturnsServiceStatus()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetFromJsonAsync<JsonElement>("/health");

        Assert.Equal("healthy", response.GetProperty("status").GetString());
        Assert.Equal("Enzo.Diagrams.Mcp", response.GetProperty("service").GetString());
    }

    [Fact]
    public async Task Mcp_ToolDiscovery_ExposesEnzoStatus()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var httpClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost")
        });
        await using var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri("http://localhost/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp
            },
            httpClient,
            loggerFactory: null,
            ownsHttpClient: false);
        await using var mcpClient = await McpClient.CreateAsync(transport);

        var tools = await mcpClient.ListToolsAsync();

        var statusTool = Assert.Single(tools, tool => tool.Name == "enzo_status");
        Assert.Contains("service information", statusTool.Description);
    }
}
