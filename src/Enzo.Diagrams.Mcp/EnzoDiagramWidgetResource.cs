using System.ComponentModel;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Enzo.Diagrams.Mcp;

[McpServerResourceType]
public static class EnzoDiagramWidgetResource
{
    public const string ResourceUri = "ui://widget/enzo-diagram.html";
    public const string MimeType = "text/html+skybridge";

    [McpServerResource(UriTemplate = ResourceUri, Name = "enzo_diagram_widget", Title = "Enzo diagram", MimeType = MimeType)]
    [Description("Minimal ChatGPT component that displays the Enzo-rendered PNG returned by render_diagram.")]
    public static ReadResourceResult GetEnzoDiagramWidget()
    {
        return new ReadResourceResult
        {
            Contents =
            [
                new TextResourceContents
                {
                    Uri = ResourceUri,
                    MimeType = MimeType,
                    Text = Html,
                    Meta = new JsonObject
                    {
                        ["ui"] = new JsonObject
                        {
                            ["prefersBorder"] = true,
                            ["csp"] = new JsonObject
                            {
                                ["connectDomains"] = new JsonArray(),
                                ["resourceDomains"] = new JsonArray()
                            }
                        },
                        ["openai/widgetDescription"] = "Displays the Enzo-rendered PNG returned by render_diagram.",
                        ["openai/widgetPrefersBorder"] = true,
                        ["openai/widgetCSP"] = new JsonObject
                        {
                            ["connect_domains"] = new JsonArray(),
                            ["resource_domains"] = new JsonArray()
                        }
                    }
                }
            ]
        };
    }

    private const string Html = """
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <style>
            body { margin: 0; font: 12px system-ui, sans-serif; color: #444; }
            img { display: block; max-width: 100%; height: auto; }
            #meta { margin-top: 6px; }
            [hidden] { display: none; }
          </style>
        </head>
        <body>
          <img id="diagram" alt="Enzo-rendered diagram" hidden>
          <div id="meta" hidden></div>
          <script>
            const img = document.getElementById('diagram');
            const meta = document.getElementById('meta');

            function mcpResult() {
              const response = window.openai && window.openai.toolResponseMetadata;
              return response && (response.mcp_tool_result || response.call_tool_result || response);
            }

            function render() {
              const result = mcpResult();
              const dataUrl = result && result._meta && result._meta.diagramDataUrl;

              if (!dataUrl) {
                return;
              }

              img.src = dataUrl;
              img.hidden = false;

              const output = window.openai && window.openai.toolOutput;
              if (output && output.diagramFormat && output.byteLength) {
                meta.textContent = output.diagramFormat + ' PNG, ' + output.byteLength + ' bytes';
                meta.hidden = false;
              }

              window.openai && window.openai.notifyIntrinsicHeight && window.openai.notifyIntrinsicHeight();
            }

            window.addEventListener('openai:set_globals', render, { passive: true });
            render();
          </script>
        </body>
        </html>
        """;
}
