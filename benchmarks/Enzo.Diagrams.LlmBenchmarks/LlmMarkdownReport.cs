using System.Globalization;

namespace Enzo.Diagrams.LlmBenchmarks;

public static class LlmMarkdownReport
{
    public static string Generate(LlmBenchmarkRun run)
    {
        var enzo = LlmMetrics.Aggregate(DiagramLanguages.Enzo, run.Results);
        var mermaid = LlmMetrics.Aggregate(DiagramLanguages.Mermaid, run.Results);
        return $"""
            # Enzo vs Mermaid - LLM Generation Benchmark

            Model: {run.Metadata.Model}
            Scenarios: {run.Results.Select(r => r.ScenarioId).Distinct().Count()}
            Runs per scenario: {run.Metadata.RunsPerScenario}
            Total generations: {run.Results.Count}
            Temperature: {run.Metadata.Temperature.ToString("0.###", CultureInfo.InvariantCulture)}
            Max repair attempts: {run.Metadata.MaxRepairAttempts}

            {SummaryTable(run, enzo, mermaid)}

            Overall TTV: {LlmMetrics.Direction(LlmMetrics.Difference(enzo.AverageTokensToValidDiagram, mermaid.AverageTokensToValidDiagram))}
            Overall successful-run TTVED: {LlmMetrics.EquivalentDirection(enzo.AverageTokensToValidEquivalentDiagram, mermaid.AverageTokensToValidEquivalentDiagram, enzo.EquivalentSuccessPercent, mermaid.EquivalentSuccessPercent)}

            ## Tokens To Valid Diagram

            | Metric | Enzo | Mermaid |
            | --- | ---: | ---: |
            | Mean | {Number(enzo.AverageTokensToValidDiagram)} | {Number(mermaid.AverageTokensToValidDiagram)} |
            | Median | {Number(enzo.MedianTokensToValidDiagram)} | {Number(mermaid.MedianTokensToValidDiagram)} |
            | Min | {enzo.MinimumTokensToValidDiagram} | {mermaid.MinimumTokensToValidDiagram} |
            | Max | {enzo.MaximumTokensToValidDiagram} | {mermaid.MaximumTokensToValidDiagram} |
            | Std dev | {Number(enzo.StandardDeviationTokensToValidDiagram)} | {Number(mermaid.StandardDeviationTokensToValidDiagram)} |

            ## Tokens To Valid Equivalent Diagram

            | Metric | Enzo | Mermaid |
            | --- | ---: | ---: |
            | Mean successful TTVED | {NullableNumber(enzo.AverageTokensToValidEquivalentDiagram)} | {NullableNumber(mermaid.AverageTokensToValidEquivalentDiagram)} |
            | Median successful TTVED | {NullableNumber(enzo.MedianTokensToValidEquivalentDiagram)} | {NullableNumber(mermaid.MedianTokensToValidEquivalentDiagram)} |
            | Min successful TTVED | {NullableNumber(enzo.MinimumTokensToValidEquivalentDiagram)} | {NullableNumber(mermaid.MinimumTokensToValidEquivalentDiagram)} |
            | Max successful TTVED | {NullableNumber(enzo.MaximumTokensToValidEquivalentDiagram)} | {NullableNumber(mermaid.MaximumTokensToValidEquivalentDiagram)} |
            | Equivalent unresolved count | {enzo.EquivalentUnresolvedCount} | {mermaid.EquivalentUnresolvedCount} |
            | Equivalent failure rate | {Number(enzo.EquivalentFailureRatePercent)}% | {Number(mermaid.EquivalentFailureRatePercent)}% |

            ## Repair Diagnostics

            | Metric | Enzo | Mermaid |
            | --- | ---: | ---: |
            | Avg repair attempts | {Number(enzo.AverageRepairAttempts)} | {Number(mermaid.AverageRepairAttempts)} |
            | Avg repair input tokens | {Number(enzo.AverageRepairInputTokens)} | {Number(mermaid.AverageRepairInputTokens)} |
            | Avg repair output tokens | {Number(enzo.AverageRepairOutputTokens)} | {Number(mermaid.AverageRepairOutputTokens)} |
            | Repair success rate | {Number(enzo.RepairSuccessRatePercent)}% | {Number(mermaid.RepairSuccessRatePercent)}% |
            | Avg semantic repair attempts | {Number(enzo.AverageSemanticRepairAttempts)} | {Number(mermaid.AverageSemanticRepairAttempts)} |
            | Avg semantic repair input tokens | {Number(enzo.AverageSemanticRepairInputTokens)} | {Number(mermaid.AverageSemanticRepairInputTokens)} |
            | Avg semantic repair output tokens | {Number(enzo.AverageSemanticRepairOutputTokens)} | {Number(mermaid.AverageSemanticRepairOutputTokens)} |
            | Runs repaired from kind failure | {enzo.RunsRepairedFromKindFailure} | {mermaid.RunsRepairedFromKindFailure} |
            | Runs repaired from structural failure | {enzo.RunsRepairedFromStructuralFailure} | {mermaid.RunsRepairedFromStructuralFailure} |
            | Runs repaired from semantic concept failure | {enzo.RunsRepairedFromSemanticConceptFailure} | {mermaid.RunsRepairedFromSemanticConceptFailure} |
            | Runs unresolved after repair | {enzo.RunsUnresolvedAfterRepair} | {mermaid.RunsUnresolvedAfterRepair} |
            | Runs stopped due to stagnation | {enzo.RunsStoppedDueToStagnation} | {mermaid.RunsStoppedDueToStagnation} |

            ## Enzo Repair Convergence

            | Metric | Count |
            | --- | ---: |
            | Repair attempts started | {enzo.RepairAttemptsStarted} |
            | Repairs that resolved syntax | {enzo.RepairsResolvedSyntax} |
            | Repairs that resolved semantic failure | {enzo.RepairsResolvedSemanticFailure} |
            | Repairs that introduced new syntax failure | {enzo.RepairsIntroducedSyntaxFailure} |
            | Identical-output repairs | {enzo.IdenticalOutputRepairs} |
            | Oscillation repairs | {enzo.OscillationRepairs} |
            | Unresolved after max attempts | {enzo.RunsUnresolvedAfterMaxAttempts} |

            {EnzoFailureCategories(run)}

            ## By Category

            {Breakdown(run, result => result.Category)}

            ## Equivalent By Category

            
            {EquivalentBreakdown(run)}

            ## By Complexity

            {Breakdown(run, result => result.Complexity)}

            ## Cold Start vs Repeated Use

            Cold start includes the complete request input tokens: system instructions, DSL guidance, and the natural-language scenario.

            | Metric | Enzo | Mermaid |
            | --- | ---: | ---: |
            | Avg initial input tokens | {Number(enzo.AverageInitialInputTokens)} | {Number(mermaid.AverageInitialInputTokens)} |
            | Initial input-token difference | {Number(enzo.AverageInitialInputTokens - mermaid.AverageInitialInputTokens)} | {Number(mermaid.AverageInitialInputTokens - enzo.AverageInitialInputTokens)} |
            | Avg generation-only tokens to valid | {Number(enzo.AverageGenerationOnlyTokensToValidDiagram)} | {Number(mermaid.AverageGenerationOnlyTokensToValidDiagram)} |
            | Avg repair output tokens | {Number(enzo.AverageRepairOutputTokens)} | {Number(mermaid.AverageRepairOutputTokens)} |

            Generation-only excludes instruction input tokens and is not total API cost.
            """;
    }

    private static string SummaryTable(LlmBenchmarkRun run, LanguageAggregate enzo, LanguageAggregate mermaid) => string.Join(Environment.NewLine, [
        "| Metric | Enzo | Mermaid |",
        "| --- | ---: | ---: |",
        Row("First-pass valid", $"{Number(enzo.FirstPassValidityPercent)}%", $"{Number(mermaid.FirstPassValidityPercent)}%"),
        Row("Eventual valid", $"{Number(enzo.EventualSuccessPercent)}%", $"{Number(mermaid.EventualSuccessPercent)}%"),
        Row("First-pass equivalent-valid", $"{Number(enzo.FirstPassEquivalentSuccessPercent)}%", $"{Number(mermaid.FirstPassEquivalentSuccessPercent)}%"),
        Row("Eventual equivalent-valid", $"{Number(enzo.EquivalentSuccessPercent)}%", $"{Number(mermaid.EquivalentSuccessPercent)}%"),
        Row("Syntax-valid", $"{Number(Percent(run, DiagramLanguages.Enzo, r => r.SyntaxValid, enzo.Runs))}%", $"{Number(Percent(run, DiagramLanguages.Mermaid, r => r.SyntaxValid, mermaid.Runs))}%"),
        Row("Render-valid", $"{Number(Percent(run, DiagramLanguages.Enzo, r => r.RenderValid, enzo.Runs))}%", $"{Number(Percent(run, DiagramLanguages.Mermaid, r => r.RenderValid, mermaid.Runs))}%"),
        Row("Kind-valid", $"{Number(Percent(run, DiagramLanguages.Enzo, r => r.KindValid, enzo.Runs))}%", $"{Number(Percent(run, DiagramLanguages.Mermaid, r => r.KindValid, mermaid.Runs))}%"),
        Row("Structurally complete", $"{Number(Percent(run, DiagramLanguages.Enzo, r => r.StructureValid, enzo.Runs))}%", $"{Number(Percent(run, DiagramLanguages.Mermaid, r => r.StructureValid, mermaid.Runs))}%"),
        Row("Semantically complete", $"{Number(Percent(run, DiagramLanguages.Enzo, r => r.SemanticValid, enzo.Runs))}%", $"{Number(Percent(run, DiagramLanguages.Mermaid, r => r.SemanticValid, mermaid.Runs))}%"),
        Row("Equivalent unresolved count", enzo.EquivalentUnresolvedCount.ToString(CultureInfo.InvariantCulture), mermaid.EquivalentUnresolvedCount.ToString(CultureInfo.InvariantCulture)),
        Row("Equivalent failure rate", $"{Number(enzo.EquivalentFailureRatePercent)}%", $"{Number(mermaid.EquivalentFailureRatePercent)}%"),
        Row("Avg output tokens", Number(enzo.AverageOutputTokens), Number(mermaid.AverageOutputTokens)),
        Row("Avg repair tokens", Number(enzo.TotalRepairTokens / Math.Max(enzo.Runs, 1.0)), Number(mermaid.TotalRepairTokens / Math.Max(mermaid.Runs, 1.0))),
        Row("Avg tokens to valid diagram", Number(enzo.AverageTokensToValidDiagram), Number(mermaid.AverageTokensToValidDiagram)),
        Row("Median tokens to valid diagram", Number(enzo.MedianTokensToValidDiagram), Number(mermaid.MedianTokensToValidDiagram)),
        Row("Avg successful tokens to valid equivalent diagram", NullableNumber(enzo.AverageTokensToValidEquivalentDiagram), NullableNumber(mermaid.AverageTokensToValidEquivalentDiagram))]);

    private static string Breakdown(LlmBenchmarkRun run, Func<LlmRunResult, string> groupBy)
    {
        var rows = run.Results.GroupBy(groupBy).OrderBy(group => group.Key).Select(group =>
        {
            var enzo = LlmMetrics.Aggregate(DiagramLanguages.Enzo, group);
            var mermaid = LlmMetrics.Aggregate(DiagramLanguages.Mermaid, group);
            return $"| {group.Key} | {enzo.Runs} | {mermaid.Runs} | {Number(enzo.EquivalentSuccessPercent)}% | {Number(mermaid.EquivalentSuccessPercent)}% | {Number(enzo.AverageTokensToValidDiagram)} | {Number(mermaid.AverageTokensToValidDiagram)} | {NullableNumber(enzo.AverageTokensToValidEquivalentDiagram)} | {NullableNumber(mermaid.AverageTokensToValidEquivalentDiagram)} |";
        });
        return string.Join(Environment.NewLine, ["| Group | Enzo runs | Mermaid runs | Enzo eventual equivalent | Mermaid eventual equivalent | Enzo avg TTV | Mermaid avg TTV | Enzo avg successful TTVED | Mermaid avg successful TTVED |", "| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |", .. rows]);
    }

    private static string EquivalentBreakdown(LlmBenchmarkRun run)
    {
        var rows = run.Results.GroupBy(result => result.Category).OrderBy(group => group.Key).Select(group =>
            $"| {group.Key} | {Count(group, DiagramLanguages.Enzo, r => r.EquivalentValid)} / {Count(group, DiagramLanguages.Enzo, _ => true)} | {Count(group, DiagramLanguages.Mermaid, r => r.EquivalentValid)} / {Count(group, DiagramLanguages.Mermaid, _ => true)} |");
        return string.Join(Environment.NewLine, ["| Category | Enzo equivalent | Mermaid equivalent |", "| --- | ---: | ---: |", .. rows]);
    }

    private static string EnzoFailureCategories(LlmBenchmarkRun run)
    {
        var rows = run.Results.Where(result => result.Language == DiagramLanguages.Enzo)
            .SelectMany(result => result.Attempts)
            .Where(attempt => attempt.IsRepair && attempt.RepairFailureCategory is not null)
            .GroupBy(attempt => attempt.RepairFailureCategory, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"| {group.Key} | {group.Count()} |");
        return string.Join(Environment.NewLine, ["### Enzo Repair Failure Categories", "", "| Category | Attempts |", "| --- | ---: |", .. rows]);
    }

    private static double Percent(LlmBenchmarkRun run, string language, Func<LlmRunResult, bool> predicate, int total) =>
        total == 0 ? 0 : Count(run.Results, language, predicate) / (double)total * 100;

    private static int Count(IEnumerable<LlmRunResult> results, string language, Func<LlmRunResult, bool> predicate) =>
        results.Count(result => result.Language == language && predicate(result));

    private static string Row(string name, string enzo, string mermaid) => $"| {name} | {enzo} | {mermaid} |";
    private static string Number(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);
    private static string NullableNumber(double? value) => value?.ToString("0.#", CultureInfo.InvariantCulture) ?? "unresolved";
    private static string NullableNumber(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "unresolved";
}
