using System.Net.Http.Json;
using System.Text.Json;
using Enzo.Diagrams.Language;
using Enzo.Diagrams.Mcp;
using Enzo.Diagrams.Rendering;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace Enzo.Diagrams.Mcp.Tests;

public sealed class McpServerTests
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private const string PizzaSequenceSource = """
        sequence PizzaMaking

        actor Customer "Customer"
        participant Cook "Cook"
        participant Oven "Oven"

        Customer -> Cook: Order pizza
        Cook -> Cook: Prepare dough
        Cook -> Cook: Add sauce and toppings
        Cook -> Oven: Put pizza in oven
        Oven --> Cook: Pizza baked
        Cook -> Customer: Serve pizza
        """;

    private const string ValidFlowSource = """
        flow Checkout
        start Begin "Order received"
        task Validate "Validate order"
        end Complete "Complete order"
        Begin -> Validate
        Validate -> Complete
        """;

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
        await using var mcpClient = await CreateMcpClientAsync(factory);

        var tools = await mcpClient.ListToolsAsync();

        var statusTool = Assert.Single(tools, tool => tool.Name == "enzo_status");
        Assert.Contains("service information", statusTool.Description);
        var renderTool = Assert.Single(tools, tool => tool.Name == "render_diagram");
        var inputSchema = renderTool.JsonSchema.ToString();
        Assert.Contains("source", inputSchema);
        Assert.DoesNotContain("format", inputSchema);
        Assert.DoesNotContain("services", inputSchema);
        Assert.DoesNotContain("cancellationToken", inputSchema);
    }

    [Theory]
    [InlineData(PizzaSequenceSource, "sequence")]
    [InlineData(ValidFlowSource, "flow")]
    public async Task RenderDiagram_ValidDsl_ReturnsPngImageContent(string source, string diagramFormat)
    {
        await using var factory = new WebApplicationFactory<Program>();
        await using var mcpClient = await CreateMcpClientAsync(factory);

        var result = await mcpClient.CallToolAsync("render_diagram", new Dictionary<string, object?>
        {
            ["source"] = source
        });

        Assert.NotEqual(true, result.IsError);
        var image = Assert.IsType<ImageContentBlock>(Assert.Single(result.Content));
        Assert.Equal("image/png", image.MimeType);
        var png = image.DecodedData.ToArray();
        Assert.NotEmpty(png);
        Assert.True(png.Take(PngSignature.Length).SequenceEqual(PngSignature));

        var metadata = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal("image/png", metadata.GetProperty("contentType").GetString());
        Assert.Equal("png", metadata.GetProperty("format").GetString());
        Assert.Equal(diagramFormat, metadata.GetProperty("diagramFormat").GetString());
        Assert.Equal(png.Length, metadata.GetProperty("byteLength").GetInt32());
    }

    [Fact]
    public async Task RenderDiagram_InvalidDsl_ReturnsCleanMcpError()
    {
        await using var factory = new WebApplicationFactory<Program>();
        await using var mcpClient = await CreateMcpClientAsync(factory);

        var result = await mcpClient.CallToolAsync("render_diagram", new Dictionary<string, object?>
        {
            ["source"] = "not a diagram"
        });

        Assert.True(result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Equal("Diagram DSL is invalid.", text.Text);

        var metadata = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal("syntax", metadata.GetProperty("errors")[0].GetProperty("type").GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RenderDiagram_MissingSource_ReturnsCleanMcpError(string? source)
    {
        await using var factory = new WebApplicationFactory<Program>();
        await using var mcpClient = await CreateMcpClientAsync(factory);

        var result = await mcpClient.CallToolAsync("render_diagram", new Dictionary<string, object?>
        {
            ["source"] = source
        });

        Assert.True(result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Equal("Source is required.", text.Text);

        var metadata = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal("required", metadata.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task RenderDiagram_RendererFailure_ReturnsCleanMcpError()
    {
        const string secret = "renderer-secret-api-key";
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEnzoDiagramRenderer>();
                services.AddSingleton<IEnzoDiagramRenderer>(new FailingRenderer(secret));
            }));
        await using var mcpClient = await CreateMcpClientAsync(factory);

        var result = await mcpClient.CallToolAsync("render_diagram", new Dictionary<string, object?>
        {
            ["source"] = ValidFlowSource
        });

        Assert.True(result.IsError);
        var serialized = JsonSerializer.Serialize(result);
        Assert.DoesNotContain(secret, serialized);
        Assert.Contains("Diagram could not be rendered as PNG.", serialized);
    }

    private static async Task<McpClient> CreateMcpClientAsync(WebApplicationFactory<Program> factory)
    {
        var httpClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost")
        });
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri("http://localhost/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp
            },
            httpClient,
            loggerFactory: null,
            ownsHttpClient: true);

        return await McpClient.CreateAsync(transport);
    }

    private sealed class FailingRenderer(string message) : IEnzoDiagramRenderer
    {
        public Task<byte[]> RenderPngAsync(DiagramParseResult result, CancellationToken cancellationToken)
        {
            throw new FlowchartPngRenderException(message);
        }
    }
}
