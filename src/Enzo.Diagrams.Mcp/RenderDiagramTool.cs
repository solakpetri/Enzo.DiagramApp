using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using Enzo.Diagrams.Language;
using Enzo.Diagrams.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Enzo.Diagrams.Mcp;

[McpServerToolType]
public static class RenderDiagramTool
{
    private const string PngContentType = "image/png";
    private const string PngFormat = "png";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [McpServerTool(Name = "render_diagram", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false, UseStructuredContent = true, OutputSchemaType = typeof(RenderDiagramMetadata))]
    [McpMeta("ui", "", JsonValue = """{"resourceUri":"ui://widget/enzo-diagram.html"}""")]
    [McpMeta("openai/outputTemplate", EnzoDiagramWidgetResource.ResourceUri)]
    [McpMeta("openai/toolInvocation/invoking", "Rendering Enzo diagram")]
    [McpMeta("openai/toolInvocation/invoked", "Enzo diagram rendered")]
    [McpMeta("securitySchemes", "", JsonValue = """[{"type":"noauth"}]""")]
    [Description("Renders complete Enzo.Diagrams DSL as an Enzo-generated PNG image. Input is only the source DSL; output format defaults to PNG.")]
    public static async Task<CallToolResult> RenderDiagram(
        [Required]
        [MinLength(1)]
        [Description("Complete Enzo.Diagrams DSL source. The first declaration is flow, sequence, or bpmn.")]
        string? source,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return Error("Source is required.", new
            {
                errors = new[]
                {
                    new { type = "request", message = "Source is required.", code = "required" }
                }
            });
        }

        cancellationToken.ThrowIfCancellationRequested();
        var parseResult = DiagramParser.Parse(source);
        if (!parseResult.IsSuccess)
        {
            return Error("Diagram DSL is invalid.", new
            {
                errors = ToDiagramErrors(parseResult)
            });
        }

        try
        {
            var png = await services.GetRequiredService<IEnzoDiagramRenderer>().RenderPngAsync(parseResult, cancellationToken);
            var metadata = new RenderDiagramMetadata(PngContentType, PngFormat, DiagramFormat(parseResult), png.Length);

            return new CallToolResult
            {
                Content = [ImageContentBlock.FromBytes(png, PngContentType)],
                StructuredContent = JsonSerializer.SerializeToElement(metadata, JsonOptions),
                Meta = new JsonObject
                {
                    ["diagramDataUrl"] = $"data:{PngContentType};base64,{Convert.ToBase64String(png)}"
                }
            };
        }
        catch (FlowchartPngRenderException exception)
        {
            services.GetRequiredService<ILoggerFactory>()
                .CreateLogger("Enzo.Diagrams.Mcp.RenderDiagramTool")
                .LogError(exception, "Enzo PNG rendering failed.");

            return Error("Diagram could not be rendered as PNG.", new
            {
                errors = new[]
                {
                    new { type = "rendering", message = "Diagram could not be rendered as PNG.", code = "png_render_failed" }
                }
            });
        }
    }

    private static CallToolResult Error(string message, object structuredContent)
    {
        return new CallToolResult
        {
            IsError = true,
            Content = [new TextContentBlock { Text = message }],
            StructuredContent = JsonSerializer.SerializeToElement(structuredContent, JsonOptions)
        };
    }

    private static object[] ToDiagramErrors(DiagramParseResult result)
    {
        var errors = new List<object>();

        foreach (var error in result.Errors)
        {
            errors.Add(new
            {
                type = "syntax",
                line = error.Line,
                column = error.Column,
                message = error.Message
            });
        }

        foreach (var error in result.ValidationErrors)
        {
            errors.Add(new
            {
                type = "validation",
                line = error.Line,
                column = error.Column,
                message = error.Message,
                code = error.Kind
            });
        }

        return errors.ToArray();
    }

    private static string DiagramFormat(DiagramParseResult result)
    {
        if (result.Flowchart is not null)
        {
            return "flow";
        }

        return result.BpmnDiagram is not null ? "bpmn" : "sequence";
    }
}
