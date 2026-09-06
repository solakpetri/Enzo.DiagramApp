using System.ComponentModel;
using System.Reflection;
using ModelContextProtocol.Server;

namespace Enzo.Diagrams.Mcp;

[McpServerToolType]
public static class EnzoStatusTool
{
    [McpServerTool(Name = "enzo_status"), Description("Returns basic Enzo.Diagrams MCP service information.")]
    public static EnzoStatus EnzoStatus()
    {
        var assembly = Assembly.GetExecutingAssembly().GetName();

        return new EnzoStatus(
            "Enzo.Diagrams.Mcp",
            assembly.Version?.ToString() ?? "unknown",
            ["mcp", "http", "tools"]);
    }
}
