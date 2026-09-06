using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Enzo.Diagrams.Language;
using Enzo.Diagrams.Rendering;
using Microsoft.Extensions.DependencyInjection;
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
    [Description("Renders complete Enzo.Diagrams DSL as an Enzo-generated PNG image. Input is only the source DSL; output format defaults to PNG.")]
    public static CallToolResult RenderDiagram(
        [Required]
        [MinLength(1)]
        [Description("Complete Enzo.Diagrams DSL source. The first declaration is flow, sequence, or bpmn.")]
        string source,
        IServiceProvider services)
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
            var png = services.GetRequiredService<IEnzoDiagramRenderer>().RenderPng(parseResult);
            var metadata = new RenderDiagramMetadata(PngContentType, PngFormat, DiagramFormat(parseResult), png.Length);

            return new CallToolResult
            {
                Content = [ImageContentBlock.FromBytes(png, PngContentType)],
                StructuredContent = JsonSerializer.SerializeToElement(metadata, JsonOptions)
            };
        }
        catch (FlowchartPngRenderException)
        {
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
        return [
            .. result.Errors.Select(error => new
            {
                type = "syntax",
                line = error.Line,
                column = error.Column,
                message = error.Message
            }),
            .. result.ValidationErrors.Select(error => new
            {
                type = "validation",
                line = error.Line,
                column = error.Column,
                message = error.Message,
                code = error.Kind
            })
        ];
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
