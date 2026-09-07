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
    double FirstPassEquivalentSuccessPercent,
    double EquivalentSuccessPercent,
    double? AverageTokensToValidEquivalentDiagram,
    double? MedianTokensToValidEquivalentDiagram,
    int? MinimumTokensToValidEquivalentDiagram,
    int? MaximumTokensToValidEquivalentDiagram,
    int EquivalentUnresolvedCount,
    double EquivalentFailureRatePercent,
    double AverageRepairAttempts,
    int TotalRepairTokens,
    double AverageGenerationOnlyTokensToValidDiagram,
    double AverageRepairOutputTokens,
    double AverageSemanticRepairAttempts,
    double AverageSemanticRepairInputTokens,
    double AverageSemanticRepairOutputTokens,
    int RunsRepairedFromKindFailure,
    int RunsRepairedFromStructuralFailure,
    int RunsRepairedFromSemanticConceptFailure,
    int RunsUnresolvedAfterRepair,
    int RunsStoppedDueToStagnation);

public static class LlmMetrics
{
    public const double EquivalentCostComparisonThresholdPercent = 95;

    public static LanguageAggregate Aggregate(string language, IEnumerable<LlmRunResult> results)
    {
        var runs = results.Where(result => result.Language == language).ToList();
        var equivalentFailures = runs.Count(r => !r.EquivalentValid);
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
            Percent(runs.Count(r => r.Attempts.Any(a => a.Valid)), runs.Count),
            Percent(runs.Count(r => r.FirstPassEquivalentValid), runs.Count),
            Percent(runs.Count(r => r.EquivalentValid), runs.Count),
            NullableAverage(runs.Select(r => r.TokensToValidEquivalentDiagram)),
            NullableMedian(runs.Select(r => r.TokensToValidEquivalentDiagram)),
            NullableMin(runs.Select(r => r.TokensToValidEquivalentDiagram)),
            NullableMax(runs.Select(r => r.TokensToValidEquivalentDiagram)),
            equivalentFailures,
            Percent(equivalentFailures, runs.Count),
            Average(runs.Select(r => r.RepairAttempts)),
            runs.Sum(r => r.TotalRepairTokens),
            Average(runs.Select(r => r.OutputTokens + r.RepairOutputTokens)),
            Average(runs.Select(r => r.RepairOutputTokens)),
            Average(runs.Select(r => r.Attempts.Count(IsSemanticRepair))),
            Average(runs.Select(r => r.Attempts.Where(IsSemanticRepair).Sum(a => a.InputTokens))),
            Average(runs.Select(r => r.Attempts.Where(IsSemanticRepair).Sum(a => a.OutputTokens))),
            runs.Count(r => r.EquivalentValid && r.Attempts.Any(a => HasRepairType(a, "Kind"))),
            runs.Count(r => r.EquivalentValid && r.Attempts.Any(a => HasRepairType(a, "Structure"))),
            runs.Count(r => r.EquivalentValid && r.Attempts.Any(a => HasRepairType(a, "Semantic"))),
            runs.Count(r => !r.EquivalentValid && r.RepairAttempts > 0),
            runs.Count(r => r.RepairStoppedReason is not null));
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

    public static string EquivalentDirection(double? enzoValue, double? mermaidValue) =>
        EquivalentDirection(enzoValue, mermaidValue, 100, 100);

    public static string EquivalentDirection(double? enzoValue, double? mermaidValue, double enzoEquivalentPercent, double mermaidEquivalentPercent, double thresholdPercent = EquivalentCostComparisonThresholdPercent)
    {
        if (enzoEquivalentPercent < thresholdPercent || mermaidEquivalentPercent < thresholdPercent)
        {
            return $"Cost comparison unresolved because equivalent validity is below the {thresholdPercent:0.#}% comparison threshold.";
        }

        if (enzoValue is null || mermaidValue is null)
        {
            return "Tokens to Valid Equivalent Diagram is unresolved for at least one language.";
        }

        var difference = Difference(enzoValue.Value, mermaidValue.Value);
        var absolute = Math.Abs(difference).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        return difference switch
        {
            > 0 => $"Enzo uses {absolute}% fewer Tokens to Valid Equivalent Diagram than Mermaid.",
            < 0 => $"Enzo uses {absolute}% more Tokens to Valid Equivalent Diagram than Mermaid.",
            _ => "Enzo and Mermaid use the same Tokens to Valid Equivalent Diagram."
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

    private static double? NullableAverage(IEnumerable<int?> values)
    {
        var array = NullableValues(values).ToArray();
        return array.Length == 0 ? null : array.Average();
    }

    private static double? NullableMedian(IEnumerable<int?> values)
    {
        var ordered = NullableValues(values).Order().ToArray();
        if (ordered.Length == 0)
        {
            return null;
        }

        var middle = ordered.Length / 2;
        return ordered.Length % 2 == 0 ? (ordered[middle - 1] + ordered[middle]) / 2.0 : ordered[middle];
    }

    private static IEnumerable<int> NullableValues(IEnumerable<int?> values) => values.Where(value => value.HasValue).Select(value => value!.Value);
    private static int? NullableMin(IEnumerable<int?> values) => NullableValues(values).Cast<int?>().Min();
    private static int? NullableMax(IEnumerable<int?> values) => NullableValues(values).Cast<int?>().Max();

    private static bool IsSemanticRepair(GenerationAttempt attempt) =>
        attempt.IsRepair && (HasRepairType(attempt, "Kind") || HasRepairType(attempt, "Structure") || HasRepairType(attempt, "Semantic"));

    private static bool HasRepairType(GenerationAttempt attempt, string repairType) =>
        attempt.RepairType?.Split('+').Contains(repairType, StringComparer.Ordinal) == true;

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
