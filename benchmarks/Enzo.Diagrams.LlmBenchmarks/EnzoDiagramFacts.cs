using Enzo.Diagrams.Benchmarks;
using Enzo.Diagrams.Domain;

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
            var participants = sequence.Participants.ToDictionary(participant => participant.Id, ParticipantLabel, StringComparer.Ordinal);
            return new DiagramFacts(
                "sequence",
                sequence.Participants.SelectMany(participant => new[] { participant.Id, participant.DisplayName }).OfType<string>().Concat(sequence.Messages.Select(message => message.Label)).ToList(),
                0,
                0,
                0,
                sequence.Participants.Count,
                sequence.Messages.Count)
            {
                Participants = participants.Values.ToList(),
                Interactions = sequence.Messages.Select(message => new DiagramInteraction(
                    EndpointLabel(participants, message.FromId),
                    EndpointLabel(participants, message.ToId),
                    message.Label)).ToList()
            };
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

    private static string ParticipantLabel(SequenceParticipant participant) =>
        participant.DisplayName is null || participant.DisplayName.Equals(participant.Id, StringComparison.Ordinal)
            ? participant.Id
            : $"{participant.Id} {participant.DisplayName}";

    private static string EndpointLabel(IReadOnlyDictionary<string, string> participants, string id) =>
        participants.TryGetValue(id, out var label) ? label : id;
}
