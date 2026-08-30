using System.Globalization;
using System.Net;
using System.Text;
using Enzo.Diagrams.Language;

namespace Enzo.Diagrams.Rendering;

public static class FlowchartSvgRenderer
{
    public static string Render(FlowchartLayout layout)
    {
        var builder = new StringBuilder();

        builder.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        builder.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{Number(layout.Bounds.Width)}\" height=\"{Number(layout.Bounds.Height)}\" viewBox=\"0 0 {Number(layout.Bounds.Width)} {Number(layout.Bounds.Height)}\">");
        builder.AppendLine("<defs><marker id=\"arrow\" markerWidth=\"10\" markerHeight=\"10\" refX=\"8\" refY=\"3\" orient=\"auto\" markerUnits=\"strokeWidth\"><path d=\"M0,0 L0,6 L9,3 z\" fill=\"#475569\" /></marker></defs>");
        builder.AppendLine("<rect width=\"100%\" height=\"100%\" fill=\"#f8fafc\" />");

        foreach (var connection in layout.Connections)
        {
            RenderConnection(builder, connection);
        }

        foreach (var node in layout.Nodes)
        {
            RenderNode(builder, node);
        }

        builder.AppendLine("</svg>");

        return builder.ToString();
    }

    private static void RenderConnection(StringBuilder builder, PositionedFlowchartConnection connection)
    {
        var points = string.Join(" ", connection.Points.Select(point => $"{Number(point.X)},{Number(point.Y)}"));
        builder.AppendLine($"<polyline points=\"{points}\" fill=\"none\" stroke=\"#475569\" stroke-width=\"2\" marker-end=\"url(#arrow)\" />");

        if (connection.Edge.Label is null || connection.Points.Count == 0)
        {
            return;
        }

        var midpoint = connection.Points[connection.Points.Count / 2];
        builder.AppendLine($"<text x=\"{Number(midpoint.X + 8)}\" y=\"{Number(midpoint.Y - 8)}\" fill=\"#334155\" font-family=\"Arial, sans-serif\" font-size=\"12\">{Escape(connection.Edge.Label)}</text>");
    }

    private static void RenderNode(StringBuilder builder, PositionedFlowchartNode node)
    {
        var centerX = node.X + (node.Size.Width / 2);
        var centerY = node.Y + (node.Size.Height / 2);

        if (node.Node.Kind == FlowchartNodeKind.Decision)
        {
            var points = string.Join(" ",
                $"{Number(centerX)},{Number(node.Y)}",
                $"{Number(node.X + node.Size.Width)},{Number(centerY)}",
                $"{Number(centerX)},{Number(node.Y + node.Size.Height)}",
                $"{Number(node.X)},{Number(centerY)}");

            builder.AppendLine($"<polygon points=\"{points}\" fill=\"#fef3c7\" stroke=\"#d97706\" stroke-width=\"2\" />");
        }
        else
        {
            var radius = node.Node.Kind is FlowchartNodeKind.Start or FlowchartNodeKind.End ? 28 : 8;
            var fill = node.Node.Kind == FlowchartNodeKind.Start ? "#dcfce7" : node.Node.Kind == FlowchartNodeKind.End ? "#fee2e2" : "#dbeafe";
            var stroke = node.Node.Kind == FlowchartNodeKind.Start ? "#16a34a" : node.Node.Kind == FlowchartNodeKind.End ? "#dc2626" : "#2563eb";

            builder.AppendLine($"<rect x=\"{Number(node.X)}\" y=\"{Number(node.Y)}\" width=\"{Number(node.Size.Width)}\" height=\"{Number(node.Size.Height)}\" rx=\"{radius}\" fill=\"{fill}\" stroke=\"{stroke}\" stroke-width=\"2\" />");
        }

        builder.AppendLine($"<text x=\"{Number(centerX)}\" y=\"{Number(centerY)}\" text-anchor=\"middle\" dominant-baseline=\"middle\" fill=\"#0f172a\" font-family=\"Arial, sans-serif\" font-size=\"14\">{Escape(node.Node.Label)}</text>");
    }

    private static string Number(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string Escape(string value)
    {
        return WebUtility.HtmlEncode(value);
    }
}
