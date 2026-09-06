using System.Text.Json;

namespace Enzo.Diagrams.Benchmarks;

public static class BenchmarkWriters
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void Write(BenchmarkRunResult result, string resultsDirectory)
    {
        File.WriteAllText(Path.Combine(resultsDirectory, "enzo-vs-mermaid-results.json"), JsonSerializer.Serialize(result, JsonOptions));
        File.WriteAllText(Path.Combine(resultsDirectory, "enzo-vs-mermaid-summary.md"), MarkdownReport.Generate(result));
    }
}
