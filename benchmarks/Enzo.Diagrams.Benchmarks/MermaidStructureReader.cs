using System.Text.RegularExpressions;

namespace Enzo.Diagrams.Benchmarks;

public static partial class MermaidStructureReader
{
    public static DiagramStructure Read(string source, string category)
    {
        return category == "sequence" ? ReadSequence(source) : ReadFlowLike(source, category);
    }

    private static DiagramStructure ReadFlowLike(string source, string category)
    {
        var elements = new Dictionary<string, string>(StringComparer.Ordinal);
        var connections = new List<string>();
        var branches = new List<string>();
        var lines = Lines(source).ToList();
        if (lines.Count == 0 || !lines[0].StartsWith("flowchart ", StringComparison.Ordinal))
        {
            throw new BenchmarkValidationException("Mermaid flow/process scenario must start with 'flowchart'.");
        }

        foreach (var line in lines.Skip(1))
        {
            var edge = MermaidEdgeRegex().Match(line);
            edge = edge.Success ? edge : MermaidLabeledEdgeRegex().Match(line);
            if (edge.Success)
            {
                var label = edge.Groups["label"].Value.Trim();
                connections.Add($"{edge.Groups["from"].Value}->{edge.Groups["to"].Value}:{(label.Length == 0 ? null : label)}");
                if (label.Length > 0)
                {
                    branches.Add(label);
                }

                continue;
            }

            var node = MermaidNodeRegex().Match(line);
            if (!node.Success)
            {
                throw new BenchmarkValidationException($"Unsupported Mermaid line: {line}");
            }

            elements[node.Groups["id"].Value] = node.Groups["label"].Value.Trim('"');
        }

        EnsureEndpointsExist(elements, connections);
        return new DiagramStructure(category, elements.Values.Order().ToList(), connections.Order().ToList(), branches.Order().ToList());
    }

    private static DiagramStructure ReadSequence(string source)
    {
        var participants = new Dictionary<string, string>(StringComparer.Ordinal);
        var messages = new List<string>();
        var lines = Lines(source).ToList();
        if (lines.Count == 0 || lines[0] != "sequenceDiagram")
        {
            throw new BenchmarkValidationException("Mermaid sequence scenario must start with 'sequenceDiagram'.");
        }

        foreach (var line in lines.Skip(1))
        {
            var participant = MermaidParticipantRegex().Match(line);
            if (participant.Success)
            {
                participants[participant.Groups["id"].Value] = participant.Groups["name"].Success
                    ? participant.Groups["name"].Value.Trim('"')
                    : participant.Groups["id"].Value;
                continue;
            }

            var message = MermaidMessageRegex().Match(line);
            if (!message.Success)
            {
                throw new BenchmarkValidationException($"Unsupported Mermaid line: {line}");
            }

            var arrow = message.Groups["arrow"].Value.StartsWith("--", StringComparison.Ordinal) ? "-->" : "->";
            messages.Add($"{message.Groups["from"].Value}{arrow}{message.Groups["to"].Value}:{message.Groups["label"].Value.Trim()}");
        }

        EnsureEndpointsExist(participants, messages);
        return new DiagramStructure("sequence", participants.Values.Order().ToList(), messages.Order().ToList(), []);
    }

    private static void EnsureEndpointsExist(Dictionary<string, string> elements, IEnumerable<string> connections)
    {
        foreach (var endpoint in connections.SelectMany(ConnectionEndpoints))
        {
            if (!elements.ContainsKey(endpoint))
            {
                throw new BenchmarkValidationException($"Mermaid connection references unknown element: {endpoint}");
            }
        }
    }

    private static IEnumerable<string> ConnectionEndpoints(string connection)
    {
        var header = connection.Split(':', 2)[0];
        var separator = header.Contains("-->", StringComparison.Ordinal) ? "-->" : "->";
        return header.Split(separator, StringSplitOptions.None);
    }

    private static IEnumerable<string> Lines(string source)
    {
        return source.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim()).Where(line => line.Length > 0);
    }

    [GeneratedRegex("^(?<id>[A-Za-z_][A-Za-z0-9_]*)\\s*(?:\\[\\\"?(?<label>[^\\]\\\"]+)\\\"?\\]|\\{\\\"?(?<label>[^}\\\"]+)\\\"?\\}|\\(\\[\\\"?(?<label>[^\\]\\\"]+)\\\"?\\]\\)|\\(\\\"?(?<label>[^)\\\"]+)\\\"?\\))$")]
    private static partial Regex MermaidNodeRegex();

    [GeneratedRegex("^(?<from>[A-Za-z_][A-Za-z0-9_]*)\\s*-->\\s*(?<to>[A-Za-z_][A-Za-z0-9_]*)$")]
    private static partial Regex MermaidEdgeRegex();

    [GeneratedRegex("^(?<from>[A-Za-z_][A-Za-z0-9_]*)\\s*--\\s+(?<label>.*?)\\s+-->\\s*(?<to>[A-Za-z_][A-Za-z0-9_]*)$")]
    private static partial Regex MermaidLabeledEdgeRegex();

    [GeneratedRegex("^(actor|participant)\\s+(?<id>[A-Za-z_][A-Za-z0-9_]*)(?:\\s+as\\s+(?<name>.+))?$")]
    private static partial Regex MermaidParticipantRegex();

    [GeneratedRegex("^(?<from>[A-Za-z_][A-Za-z0-9_]*)\\s*(?<arrow>-{1,2}>>)\\s*(?<to>[A-Za-z_][A-Za-z0-9_]*):\\s*(?<label>.+)$")]
    private static partial Regex MermaidMessageRegex();
}
