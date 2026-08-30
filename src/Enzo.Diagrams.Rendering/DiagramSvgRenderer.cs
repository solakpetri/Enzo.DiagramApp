using Enzo.Diagrams.Language;

namespace Enzo.Diagrams.Rendering;

public static class DiagramSvgRenderer
{
    public static string Render(DiagramParseResult result)
    {
        if (!result.IsSuccess)
        {
            throw new ArgumentException("Diagram parse result must be successful.", nameof(result));
        }

        if (result.Flowchart is not null)
        {
            return FlowchartSvgRenderer.Render(FlowchartLayoutEngine.Layout(result.Flowchart));
        }

        if (result.BpmnDiagram is not null)
        {
            return BpmnSvgRenderer.Render(BpmnLayoutEngine.Layout(result.BpmnDiagram));
        }

        return SequenceSvgRenderer.Render(SequenceLayoutEngine.Layout(result.SequenceDiagram!));
    }
}
