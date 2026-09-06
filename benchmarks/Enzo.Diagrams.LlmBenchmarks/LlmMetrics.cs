namespace Enzo.Diagrams.LlmBenchmarks;

public sealed record LanguageAggregate(
    string Language,
    int Runs,
    double AverageInitialInputTokens,
    double AverageOutputTokens,
    double AverageTotalTokens,
    double MedianTotalTokens,
    double AverageTokensToValidDiagram,
    double MedianTokensToValidDiagram,
    int MinimumTokensToValidDiagram,
    int MaximumTokensToValidDiagram,
    double StandardDeviationTokensToValidDiagram,
    double FirstPassValidityPercent,
    double EventualSuccessPercent,
    double AverageRepairAttempts,
    int TotalRepairTokens,
    double AverageGenerationOnlyTokensToValidDiagram,
    double AverageRepairOutputTokens);

public static class LlmMetrics
{
    public static LanguageAggregate Aggregate(string language, IEnumerable<LlmRunResult> results)
    {
        var runs = results.Where(result => result.Language == language).ToList();
        return new LanguageAggregate(
            language,
            runs.Count,
            Average(runs.Select(r => r.InputTokens)),
            Average(runs.Select(r => r.OutputTokens)),
            Average(runs.Select(r => r.TotalTokens)),
            Median(runs.Select(r => r.TotalTokens)),
            Average(runs.Select(r => r.TokensToValidDiagram)),
            Median(runs.Select(r => r.TokensToValidDiagram)),
            runs.Count == 0 ? 0 : runs.Min(r => r.TokensToValidDiagram),
            runs.Count == 0 ? 0 : runs.Max(r => r.TokensToValidDiagram),
            StdDev(runs.Select(r => r.TokensToValidDiagram)),
            Percent(runs.Count(r => r.FirstPassValid), runs.Count),
            Percent(runs.Count(r => r.FinalValid), runs.Count),
            Average(runs.Select(r => r.RepairAttempts)),
            runs.Sum(r => r.TotalRepairTokens),
            Average(runs.Select(r => r.OutputTokens + r.RepairOutputTokens)),
            Average(runs.Select(r => r.RepairOutputTokens)));
    }

    public static double Difference(double enzoValue, double mermaidValue) => mermaidValue == 0 ? 0 : (mermaidValue - enzoValue) / mermaidValue * 100;

    public static string Direction(double differencePercent)
    {
        var absolute = Math.Abs(differencePercent).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        return differencePercent switch
        {
            > 0 => $"Enzo uses {absolute}% fewer Tokens to Valid Diagram than Mermaid.",
            < 0 => $"Enzo uses {absolute}% more Tokens to Valid Diagram than Mermaid.",
            _ => "Enzo and Mermaid use the same Tokens to Valid Diagram."
        };
    }

    public static double Median(IEnumerable<int> values)
    {
        var ordered = values.Order().ToArray();
        if (ordered.Length == 0)
        {
            return 0;
        }

        var middle = ordered.Length / 2;
        return ordered.Length % 2 == 0 ? (ordered[middle - 1] + ordered[middle]) / 2.0 : ordered[middle];
    }

    private static double Average(IEnumerable<int> values)
    {
        var array = values.ToArray();
        return array.Length == 0 ? 0 : array.Average();
    }

    private static double Percent(int count, int total) => total == 0 ? 0 : (double)count / total * 100;

    private static double StdDev(IEnumerable<int> values)
    {
        var array = values.Select(value => (double)value).ToArray();
        if (array.Length == 0)
        {
            return 0;
        }

        var average = array.Average();
        return Math.Sqrt(array.Sum(value => Math.Pow(value - average, 2)) / array.Length);
    }
}
