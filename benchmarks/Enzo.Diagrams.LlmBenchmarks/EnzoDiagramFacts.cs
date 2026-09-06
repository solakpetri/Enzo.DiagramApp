using Enzo.Diagrams.Benchmarks;
using Enzo.Diagrams.Language;

namespace Enzo.Diagrams.LlmBenchmarks;

public static class EnzoDiagramFacts
{
    public static DiagramFacts Extract(string source)
    {
        var parsed = DiagramParser.Parse(source);
        if (!parsed.IsSuccess)
        {
            throw new BenchmarkValidationException(string.Join("; ", parsed.Errors.Select(error => error.Message).Concat(parsed.ValidationErrors.Select(error => error.Message))));
        }

        if (parsed.Flowchart is { } flow)
        {
            return new DiagramFacts(
                "flow",
                flow.Nodes.SelectMany(node => new[] { node.Id, node.Label }).Concat(flow.Edges.Select(edge => edge.Label).OfType<string>()).ToList(),
                flow.Nodes.Count,
                flow.Edges.Count,
                flow.Nodes.Count(node => node.Kind == FlowchartNodeKind.Decision),
                0,
                0);
        }

        if (parsed.SequenceDiagram is { } sequence)
        {
            return new DiagramFacts(
                "sequence",
                sequence.Participants.SelectMany(participant => new[] { participant.Id, participant.DisplayName }).OfType<string>().Concat(sequence.Messages.Select(message => message.Label)).ToList(),
                0,
                0,
                0,
                sequence.Participants.Count,
                sequence.Messages.Count);
        }

        var bpmn = parsed.BpmnDiagram!;
        return new DiagramFacts(
            "process",
            bpmn.Elements.SelectMany(element => new[] { element.Id, element.Label }).Concat(bpmn.Flows.Select(flow => flow.Label).OfType<string>()).ToList(),
            bpmn.Elements.Count,
            bpmn.Flows.Count,
            bpmn.Elements.Count(element => element.Kind == BpmnElementKind.ExclusiveGateway),
            0,
            0);
    }
}
