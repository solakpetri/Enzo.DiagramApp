using Enzo.Diagrams.Language;

namespace Enzo.Diagrams.Benchmarks;

public static class EnzoStructureReader
{
    public static DiagramStructure Read(DiagramParseResult result)
    {
        if (result.Flowchart is { } flowchart)
        {
            return new DiagramStructure(
                "flow",
                flowchart.Nodes.Select(node => node.Label).Order().ToList(),
                flowchart.Edges.Select(edge => $"{edge.FromId}->{edge.ToId}:{edge.Label}" ).Order().ToList(),
                flowchart.Edges.Where(edge => edge.Label is not null).Select(edge => edge.Label!).Order().ToList());
        }

        if (result.SequenceDiagram is { } sequence)
        {
            return new DiagramStructure(
                "sequence",
                sequence.Participants.Select(participant => participant.DisplayName ?? participant.Id).Order().ToList(),
                sequence.Messages.Select(message => $"{message.FromId}{Arrow(message.Kind)}{message.ToId}:{message.Label}" ).Order().ToList(),
                []);
        }

        var bpmn = result.BpmnDiagram!;
        return new DiagramStructure(
            "process",
            bpmn.Elements.Select(element => element.Label).Order().ToList(),
            bpmn.Flows.Select(flow => $"{flow.FromId}->{flow.ToId}:{flow.Label}" ).Order().ToList(),
            bpmn.Flows.Where(flow => flow.Label is not null).Select(flow => flow.Label!).Order().ToList());
    }

    private static string Arrow(SequenceMessageKind kind)
    {
        return kind == SequenceMessageKind.Response ? "-->" : "->";
    }
}
