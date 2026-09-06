namespace Enzo.Diagrams.LlmBenchmarks;

public sealed record BenchmarkOptions(
    int Runs,
    int MaxRepairAttempts,
    string Model,
    double Temperature,
    double TopP,
    int MaxOutputTokens,
    string? ScenarioFilter,
    string? CategoryFilter,
    string LanguageFilter,
    string OutputDirectory,
    string? ResumeFile,
    string? ReevaluateFile,
    string MermaidCommand)
{
    public static BenchmarkOptions Parse(string[] args, string repositoryRoot)
    {
        var values = ReadArgs(args);
        return new BenchmarkOptions(
            Int(values, "runs", 5),
            Int(values, "max-repair-attempts", 3),
            String(values, "model", Environment.GetEnvironmentVariable("ENZO_LLM_BENCHMARK_MODEL") ?? Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o-mini"),
            Double(values, "temperature", 0.2),
            Double(values, "top-p", 1),
            Int(values, "max-output-tokens", 1200),
            NullableString(values, "scenario"),
            NullableString(values, "category"),
            String(values, "language", "all").ToLowerInvariant(),
            String(values, "output-directory", Path.Combine(repositoryRoot, "benchmarks", "results", "llm-generation-cost")),
            NullableString(values, "resume"),
            NullableString(values, "reevaluate"),
            String(values, "mermaid-command", Environment.GetEnvironmentVariable("MERMAID_CLI") ?? MermaidExecutableResolver.GetDefaultExecutable()));
    }

    private static Dictionary<string, string> ReadArgs(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var keyValue = arg[2..].Split('=', 2);
            values[keyValue[0]] = keyValue.Length == 2 ? keyValue[1] : index + 1 < args.Length ? args[++index] : "true";
        }

        return values;
    }

    private static string String(IReadOnlyDictionary<string, string> values, string key, string fallback) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

    private static string? NullableString(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static int Int(IReadOnlyDictionary<string, string> values, string key, int fallback) =>
        values.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) ? parsed : fallback;

    private static double Double(IReadOnlyDictionary<string, string> values, string key, double fallback) =>
        values.TryGetValue(key, out var value) && double.TryParse(value, out var parsed) ? parsed : fallback;
}
