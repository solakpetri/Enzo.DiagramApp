using System.Globalization;
using System.Net;
using System.Text;
using Enzo.Diagrams.Domain;

namespace Enzo.Diagrams.Infrastructure;

public static class BpmnSvgRenderer
{
    public static string Render(BpmnLayout layout)
    {
        var builder = new StringBuilder();

        builder.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        builder.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{Number(layout.Bounds.Width)}\" height=\"{Number(layout.Bounds.Height)}\" viewBox=\"0 0 {Number(layout.Bounds.Width)} {Number(layout.Bounds.Height)}\">");
        builder.AppendLine("<defs><marker id=\"bpmn-arrow\" markerWidth=\"10\" markerHeight=\"10\" refX=\"8\" refY=\"3\" orient=\"auto\" markerUnits=\"strokeWidth\"><path d=\"M0,0 L0,6 L9,3 z\" fill=\"#111827\" /></marker></defs>");
        builder.AppendLine("<rect width=\"100%\" height=\"100%\" fill=\"#ffffff\" />");

        foreach (var flow in layout.Flows)
        {
            RenderFlow(builder, flow);
        }

        foreach (var element in layout.Elements)
        {
            RenderElement(builder, element);
        }

        builder.AppendLine("</svg>");

        return builder.ToString();
    }

    private static void RenderFlow(StringBuilder builder, PositionedBpmnSequenceFlow flow)
    {
        var points = string.Join(" ", flow.Points.Select(point => $"{Number(point.X)},{Number(point.Y)}"));
        builder.AppendLine($"<polyline points=\"{points}\" fill=\"none\" stroke=\"#111827\" stroke-width=\"1.5\" marker-end=\"url(#bpmn-arrow)\" />");

        if (flow.Flow.Label is null || flow.Points.Count == 0)
        {
            return;
        }

        var middleIndex = flow.Points.Count / 2;
        var midpoint = flow.Points.Count % 2 == 0
            ? new DiagramPoint(
                (flow.Points[middleIndex - 1].X + flow.Points[middleIndex].X) / 2,
                (flow.Points[middleIndex - 1].Y + flow.Points[middleIndex].Y) / 2)
            : flow.Points[middleIndex];
        builder.AppendLine($"<text x=\"{Number(midpoint.X + 8)}\" y=\"{Number(midpoint.Y - 8)}\" fill=\"#111827\" font-family=\"DejaVu Sans, Arial, sans-serif\" font-size=\"12\">{Escape(flow.Flow.Label)}</text>");
    }

    private static void RenderElement(StringBuilder builder, PositionedBpmnElement element)
    {
        var centerX = element.X + (element.Size.Width / 2);
        var centerY = element.Y + (element.Size.Height / 2);

        switch (element.Element.Kind)
        {
            case BpmnElementKind.StartEvent:
                builder.AppendLine($"<circle cx=\"{Number(centerX)}\" cy=\"{Number(centerY)}\" r=\"28\" fill=\"#ffffff\" stroke=\"#111827\" stroke-width=\"2\" />");
                RenderExternalLabel(builder, element, centerX);
                break;
            case BpmnElementKind.EndEvent:
                builder.AppendLine($"<circle cx=\"{Number(centerX)}\" cy=\"{Number(centerY)}\" r=\"28\" fill=\"#ffffff\" stroke=\"#111827\" stroke-width=\"4\" />");
                RenderExternalLabel(builder, element, centerX);
                break;
            case BpmnElementKind.ExclusiveGateway:
                RenderGateway(builder, element, centerX, centerY);
                break;
            default:
                builder.AppendLine($"<rect x=\"{Number(element.X)}\" y=\"{Number(element.Y)}\" width=\"{Number(element.Size.Width)}\" height=\"{Number(element.Size.Height)}\" rx=\"10\" fill=\"#ffffff\" stroke=\"#111827\" stroke-width=\"2\" />");
                builder.AppendLine($"<text x=\"{Number(centerX)}\" y=\"{Number(centerY)}\" text-anchor=\"middle\" dominant-baseline=\"middle\" fill=\"#111827\" font-family=\"DejaVu Sans, Arial, sans-serif\" font-size=\"14\">{Escape(element.Element.Label)}</text>");
                break;
        }
    }

    private static void RenderGateway(StringBuilder builder, PositionedBpmnElement element, double centerX, double centerY)
    {
        var points = string.Join(" ",
            $"{Number(centerX)},{Number(element.Y)}",
            $"{Number(element.X + element.Size.Width)},{Number(centerY)}",
            $"{Number(centerX)},{Number(element.Y + element.Size.Height)}",
            $"{Number(element.X)},{Number(centerY)}");
        builder.AppendLine($"<polygon points=\"{points}\" fill=\"#ffffff\" stroke=\"#111827\" stroke-width=\"2\" />");
        builder.AppendLine($"<path d=\"M {Number(centerX - 14)} {Number(centerY - 14)} L {Number(centerX + 14)} {Number(centerY + 14)} M {Number(centerX + 14)} {Number(centerY - 14)} L {Number(centerX - 14)} {Number(centerY + 14)}\" stroke=\"#111827\" stroke-width=\"4\" stroke-linecap=\"round\" />");
        RenderExternalLabel(builder, element, centerX);
    }

    private static void RenderExternalLabel(StringBuilder builder, PositionedBpmnElement element, double centerX)
    {
        builder.AppendLine($"<text x=\"{Number(centerX)}\" y=\"{Number(element.Y + element.Size.Height + 18)}\" text-anchor=\"middle\" fill=\"#111827\" font-family=\"DejaVu Sans, Arial, sans-serif\" font-size=\"12\">{Escape(element.Element.Label)}</text>");
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
