namespace Enzo.Diagrams.Benchmarks;

public static class DiagramEquivalence
{
    public static IReadOnlyList<string> Compare(DiagramStructure enzo, DiagramStructure mermaid)
    {
        var errors = new List<string>();
        AddIfDifferent(errors, "diagram kind", enzo.Kind, mermaid.Kind);
        AddIfDifferent(errors, "element count", enzo.ElementCount, mermaid.ElementCount);
        AddIfDifferent(errors, "connection count", enzo.ConnectionCount, mermaid.ConnectionCount);
        AddSetDifference(errors, "element labels", enzo.Elements, mermaid.Elements);
        AddSetDifference(errors, "connections/messages", enzo.Connections, mermaid.Connections);
        AddSetDifference(errors, "connection/message labels", Labels(enzo.Connections), Labels(mermaid.Connections));
        AddSetDifference(errors, "decision branches", enzo.BranchLabels, mermaid.BranchLabels);
        return errors;
    }

    private static IReadOnlyList<string> Labels(IEnumerable<string> connections)
    {
        return connections.Select(connection => connection.Split(':', 2).ElementAtOrDefault(1) ?? string.Empty)
            .Where(label => label.Length > 0)
            .Order()
            .ToList();
    }

    private static void AddIfDifferent<T>(List<string> errors, string label, T enzo, T mermaid)
    {
        if (!EqualityComparer<T>.Default.Equals(enzo, mermaid))
        {
            errors.Add($"Different {label}: Enzo={enzo}, Mermaid={mermaid}");
        }
    }

    private static void AddSetDifference(List<string> errors, string label, IReadOnlyList<string> enzo, IReadOnlyList<string> mermaid)
    {
        if (!enzo.SequenceEqual(mermaid))
        {
            errors.Add($"Different {label}.");
        }
    }
}
