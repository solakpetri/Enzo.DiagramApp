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

            {SummaryTable(enzo, mermaid)}

            Overall: {LlmMetrics.Direction(LlmMetrics.Difference(enzo.AverageTokensToValidDiagram, mermaid.AverageTokensToValidDiagram))}

            ## Tokens To Valid Diagram

            | Metric | Enzo | Mermaid |
            | --- | ---: | ---: |
            | Mean | {Number(enzo.AverageTokensToValidDiagram)} | {Number(mermaid.AverageTokensToValidDiagram)} |
            | Median | {Number(enzo.MedianTokensToValidDiagram)} | {Number(mermaid.MedianTokensToValidDiagram)} |
            | Min | {enzo.MinimumTokensToValidDiagram} | {mermaid.MinimumTokensToValidDiagram} |
            | Max | {enzo.MaximumTokensToValidDiagram} | {mermaid.MaximumTokensToValidDiagram} |
            | Std dev | {Number(enzo.StandardDeviationTokensToValidDiagram)} | {Number(mermaid.StandardDeviationTokensToValidDiagram)} |

            ## By Category

            {Breakdown(run, result => result.Category)}

            ## By Complexity

            {Breakdown(run, result => result.Complexity)}

            ## Cold Start vs Repeated Use

            Cold start includes the complete request input tokens: system instructions, DSL guidance, and the natural-language scenario.

            | Metric | Enzo | Mermaid |
            | --- | ---: | ---: |
            | Avg initial input tokens | {Number(enzo.AverageInitialInputTokens)} | {Number(mermaid.AverageInitialInputTokens)} |
            | Avg generation-only tokens to valid | {Number(enzo.AverageGenerationOnlyTokensToValidDiagram)} | {Number(mermaid.AverageGenerationOnlyTokensToValidDiagram)} |
            | Avg repair output tokens | {Number(enzo.AverageRepairOutputTokens)} | {Number(mermaid.AverageRepairOutputTokens)} |

            Generation-only excludes instruction input tokens and is not total API cost.
            """;
    }

    private static string SummaryTable(LanguageAggregate enzo, LanguageAggregate mermaid) => string.Join(Environment.NewLine, [
        "| Metric | Enzo | Mermaid |",
        "| --- | ---: | ---: |",
        Row("First-pass valid", $"{Number(enzo.FirstPassValidityPercent)}%", $"{Number(mermaid.FirstPassValidityPercent)}%"),
        Row("Eventual valid", $"{Number(enzo.EventualSuccessPercent)}%", $"{Number(mermaid.EventualSuccessPercent)}%"),
        Row("Avg output tokens", Number(enzo.AverageOutputTokens), Number(mermaid.AverageOutputTokens)),
        Row("Avg repair tokens", Number(enzo.TotalRepairTokens / Math.Max(enzo.Runs, 1.0)), Number(mermaid.TotalRepairTokens / Math.Max(mermaid.Runs, 1.0))),
        Row("Avg tokens to valid diagram", Number(enzo.AverageTokensToValidDiagram), Number(mermaid.AverageTokensToValidDiagram)),
        Row("Median tokens to valid diagram", Number(enzo.MedianTokensToValidDiagram), Number(mermaid.MedianTokensToValidDiagram))]);

    private static string Breakdown(LlmBenchmarkRun run, Func<LlmRunResult, string> groupBy)
    {
        var rows = run.Results.GroupBy(groupBy).OrderBy(group => group.Key).Select(group =>
        {
            var enzo = LlmMetrics.Aggregate(DiagramLanguages.Enzo, group);
            var mermaid = LlmMetrics.Aggregate(DiagramLanguages.Mermaid, group);
            return $"| {group.Key} | {enzo.Runs} | {mermaid.Runs} | {Number(enzo.AverageTokensToValidDiagram)} | {Number(mermaid.AverageTokensToValidDiagram)} | {LlmMetrics.Direction(LlmMetrics.Difference(enzo.AverageTokensToValidDiagram, mermaid.AverageTokensToValidDiagram))} |";
        });
        return string.Join(Environment.NewLine, ["| Group | Enzo runs | Mermaid runs | Enzo avg tokens to valid | Mermaid avg tokens to valid | Difference |", "| --- | ---: | ---: | ---: | ---: | --- |", .. rows]);
    }

    private static string Row(string name, string enzo, string mermaid) => $"| {name} | {enzo} | {mermaid} |";
    private static string Number(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);
}
