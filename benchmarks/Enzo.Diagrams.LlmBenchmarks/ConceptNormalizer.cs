using System.Text;
using System.Text.RegularExpressions;

namespace Enzo.Diagrams.LlmBenchmarks;

public static partial class ConceptNormalizer
{
    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var spaced = SplitIdentifierCasing(value);
        var builder = new StringBuilder(spaced.Length);
        foreach (var ch in spaced.ToLowerInvariant())
        {
            builder.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }

        return WhitespaceRegex().Replace(builder.ToString(), " ").Trim()
            .Replace("saa s", "saas", StringComparison.Ordinal);
    }

    private static string SplitIdentifierCasing(string value) =>
        Regex.Replace(value, "(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", " ");

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();
}
