using System.Text.RegularExpressions;
using Enzo.Diagrams.Benchmarks;

namespace Enzo.Diagrams.LlmBenchmarks;

public static partial class MermaidDiagramFacts
{
    public static DiagramFacts Extract(string source, string expectedKind)
    {
        var lines = Lines(source).ToList();
        if (lines.Count == 0)
        {
            throw new BenchmarkValidationException("Mermaid source is empty.");
        }

        if (lines[0].StartsWith("sequenceDiagram", StringComparison.OrdinalIgnoreCase))
        {
            return ExtractSequence(lines);
        }

        if (lines[0].StartsWith("flowchart", StringComparison.OrdinalIgnoreCase) || lines[0].StartsWith("graph", StringComparison.OrdinalIgnoreCase))
        {
            return ExtractFlowLike(lines, expectedKind == "process" ? "process" : "flow");
        }

        throw new BenchmarkValidationException($"Unsupported Mermaid diagram declaration: {lines[0]}");
    }

    private static DiagramFacts ExtractFlowLike(IReadOnlyList<string> lines, string kind)
    {
        var nodes = new Dictionary<string, (string Label, bool Decision)>(StringComparer.Ordinal);
        var edgeCount = 0;
        var labels = new List<string>();
        foreach (var line in lines.Skip(1))
        {
            if (IsIgnorableFlowLine(line))
            {
                continue;
            }

            var edge = FlowEdgeRegex().Match(line);
            if (edge.Success)
            {
                AddEndpoint(nodes, labels, edge.Groups["from"].Value);
                AddEndpoint(nodes, labels, edge.Groups["to"].Value);
                AddLabel(labels, edge.Groups["label"].Value);
                AddLabel(labels, edge.Groups["barlabel"].Value);
                edgeCount++;
                continue;
            }

            AddEndpoint(nodes, labels, line);
        }

        return new DiagramFacts(kind, labels, nodes.Count, edgeCount, nodes.Values.Count(node => node.Decision), 0, 0);
    }

    private static DiagramFacts ExtractSequence(IReadOnlyList<string> lines)
    {
        var participants = new Dictionary<string, string>(StringComparer.Ordinal);
        var labels = new List<string>();
        var interactions = 0;
        foreach (var line in lines.Skip(1))
        {
            if (IsIgnorableSequenceLine(line))
            {
                continue;
            }

            var participant = SequenceParticipantRegex().Match(line);
            if (participant.Success)
            {
                var id = participant.Groups["id"].Value;
                var name = participant.Groups["name"].Success ? Unquote(participant.Groups["name"].Value) : id;
                participants[id] = name;
                AddLabel(labels, id);
                AddLabel(labels, name);
                continue;
            }

            var message = SequenceMessageRegex().Match(line);
            if (message.Success)
            {
                AddParticipant(participants, labels, message.Groups["from"].Value);
                AddParticipant(participants, labels, message.Groups["to"].Value);
                AddLabel(labels, message.Groups["label"].Value);
                interactions++;
            }
        }

        return new DiagramFacts("sequence", labels, 0, 0, 0, participants.Count, interactions);
    }

    private static void AddEndpoint(Dictionary<string, (string Label, bool Decision)> nodes, List<string> labels, string text)
    {
        var endpoint = FlowEndpointRegex().Match(text.Trim());
        if (!endpoint.Success)
        {
            throw new BenchmarkValidationException($"Unsupported Mermaid flow element: {text}");
        }

        var id = endpoint.Groups["id"].Value;
        var label = FirstNonEmpty(endpoint.Groups["bracket"].Value, endpoint.Groups["brace"].Value, endpoint.Groups["paren"].Value, id);
        var decision = endpoint.Groups["brace"].Success;
        nodes[id] = (Unquote(label), decision || (nodes.TryGetValue(id, out var existing) && existing.Decision));
        AddLabel(labels, id);
        AddLabel(labels, label);
    }

    private static void AddParticipant(Dictionary<string, string> participants, List<string> labels, string id)
    {
        if (!participants.ContainsKey(id))
        {
            participants[id] = id;
            AddLabel(labels, id);
        }
    }

    private static void AddLabel(List<string> labels, string? label)
    {
        if (!string.IsNullOrWhiteSpace(label))
        {
            labels.Add(Unquote(label.Trim()));
        }
    }

    private static bool IsIgnorableFlowLine(string line) =>
        line.StartsWith("%%", StringComparison.Ordinal) || line.StartsWith("classDef ", StringComparison.OrdinalIgnoreCase) ||
        line.StartsWith("class ", StringComparison.OrdinalIgnoreCase) || line.Equals("end", StringComparison.OrdinalIgnoreCase) ||
        line.StartsWith("subgraph ", StringComparison.OrdinalIgnoreCase);

    private static bool IsIgnorableSequenceLine(string line) =>
        line.StartsWith("%%", StringComparison.Ordinal) || line.Equals("end", StringComparison.OrdinalIgnoreCase) ||
        SequenceControlRegex().IsMatch(line);

    private static IEnumerable<string> Lines(string source) =>
        source.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim()).Where(line => line.Length > 0);

    private static string FirstNonEmpty(params string[] values) => values.First(value => !string.IsNullOrWhiteSpace(value));
    private static string Unquote(string value) => value.Trim().Trim('"', '\'');

    [GeneratedRegex("^(?<from>[A-Za-z0-9_.$-]+(?:\\s*(?:\\[.*?\\]|\\{.*?\\}|\\(.*?\\)))?)\\s*(?:--\\|(?<barlabel>.*?)\\|-->|--\\s*(?<label>.*?)\\s*-->|-->|==>|-.->)\\s*(?<to>[A-Za-z0-9_.$-]+(?:\\s*(?:\\[.*?\\]|\\{.*?\\}|\\(.*?\\)))?)$")]
    private static partial Regex FlowEdgeRegex();

    [GeneratedRegex("^(?<id>[A-Za-z0-9_.$-]+)\\s*(?:\\[\\\"?(?<bracket>.*?)\\\"?\\]|\\{\\\"?(?<brace>.*?)\\\"?\\}|\\(\\[?\\\"?(?<paren>.*?)\\\"?\\]?\\))?$")]
    private static partial Regex FlowEndpointRegex();

    [GeneratedRegex("^(actor|participant)\\s+(?<id>[A-Za-z0-9_.$-]+)(?:\\s+as\\s+(?<name>.+))?$")]
    private static partial Regex SequenceParticipantRegex();

    [GeneratedRegex("^(?<from>[A-Za-z0-9_.$-]+)\\s*(?:-{1,2}>>|--?>|--?\\)|--?x)\\s*(?<to>[A-Za-z0-9_.$-]+):\\s*(?<label>.+)$")]
    private static partial Regex SequenceMessageRegex();

    [GeneratedRegex("^(alt|else|opt|loop|par|and|rect|critical|break|note|activate|deactivate)\\b", RegexOptions.IgnoreCase)]
    private static partial Regex SequenceControlRegex();
}
