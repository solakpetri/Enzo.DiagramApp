namespace Enzo.Diagrams.LlmBenchmarks;

public static class SourceNormalizer
{
    public static (string Source, bool Applied) RemoveMarkdownFence(string source)
    {
        var trimmed = source.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return (trimmed, !string.Equals(source, trimmed, StringComparison.Ordinal));
        }

        var lines = trimmed.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (lines.Length < 2 || !lines[^1].StartsWith("```", StringComparison.Ordinal))
        {
            return (trimmed, !string.Equals(source, trimmed, StringComparison.Ordinal));
        }

        return (string.Join('\n', lines.Skip(1).SkipLast(1)).Trim(), true);
    }
}
