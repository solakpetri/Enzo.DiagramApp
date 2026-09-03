using System.Globalization;
using System.Net;
using System.Text;
using Enzo.Diagrams.Language;

namespace Enzo.Diagrams.Rendering;

public static class SequenceSvgRenderer
{
    public static string Render(SequenceLayout layout)
    {
        var builder = new StringBuilder();

        builder.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        builder.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{Number(layout.Bounds.Width)}\" height=\"{Number(layout.Bounds.Height)}\" viewBox=\"0 0 {Number(layout.Bounds.Width)} {Number(layout.Bounds.Height)}\">");
        builder.AppendLine("<defs><marker id=\"sequence-arrow\" markerWidth=\"10\" markerHeight=\"10\" refX=\"8\" refY=\"3\" orient=\"auto\" markerUnits=\"strokeWidth\"><path d=\"M0,0 L0,6 L9,3 z\" fill=\"#334155\" /></marker></defs>");
        builder.AppendLine("<rect width=\"100%\" height=\"100%\" fill=\"#f8fafc\" />");

        foreach (var participant in layout.Participants)
        {
            RenderParticipant(builder, participant);
        }

        foreach (var message in layout.Messages)
        {
            RenderMessage(builder, message);
        }

        builder.AppendLine("</svg>");

        return builder.ToString();
    }

    private static void RenderParticipant(StringBuilder builder, PositionedSequenceParticipant participant)
    {
        var fill = participant.Participant.Kind == SequenceParticipantKind.Actor ? "#fef3c7" : "#dbeafe";
        var stroke = participant.Participant.Kind == SequenceParticipantKind.Actor ? "#d97706" : "#2563eb";
        var centerX = participant.X + (participant.Size.Width / 2);
        var centerY = participant.Y + (participant.Size.Height / 2);

        builder.AppendLine($"<rect x=\"{Number(participant.X)}\" y=\"{Number(participant.Y)}\" width=\"{Number(participant.Size.Width)}\" height=\"{Number(participant.Size.Height)}\" rx=\"8\" fill=\"{fill}\" stroke=\"{stroke}\" stroke-width=\"2\" />");
        builder.AppendLine($"<text x=\"{Number(centerX)}\" y=\"{Number(centerY)}\" text-anchor=\"middle\" dominant-baseline=\"middle\" fill=\"#0f172a\" font-family=\"DejaVu Sans, Arial, sans-serif\" font-size=\"14\">{Escape(participant.Participant.DisplayName ?? participant.Participant.Id)}</text>");
        builder.AppendLine($"<line x1=\"{Number(participant.LifelineX)}\" y1=\"{Number(participant.LifelineStartY)}\" x2=\"{Number(participant.LifelineX)}\" y2=\"{Number(participant.LifelineEndY)}\" stroke=\"#94a3b8\" stroke-width=\"2\" stroke-dasharray=\"6 6\" />");
    }

    private static void RenderMessage(StringBuilder builder, PositionedSequenceMessage message)
    {
        var dash = message.Message.Kind == SequenceMessageKind.Response ? " stroke-dasharray=\"6 6\"" : string.Empty;

        builder.AppendLine($"<line x1=\"{Number(message.Start.X)}\" y1=\"{Number(message.Start.Y)}\" x2=\"{Number(message.End.X)}\" y2=\"{Number(message.End.Y)}\" stroke=\"#334155\" stroke-width=\"2\" marker-end=\"url(#sequence-arrow)\"{dash} />");
        builder.AppendLine($"<text x=\"{Number(message.LabelPosition.X)}\" y=\"{Number(message.LabelPosition.Y)}\" text-anchor=\"middle\" fill=\"#334155\" font-family=\"DejaVu Sans, Arial, sans-serif\" font-size=\"12\">{Escape(message.Message.Label)}</text>");
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
