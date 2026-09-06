using System.Globalization;

namespace Enzo.Diagrams.Benchmarks;

public static class MarkdownReport
{
    public static string Generate(BenchmarkRunResult result)
    {
        return $"""
            # Enzo vs Mermaid Token Benchmark

            Scenarios: {result.TotalScenarios}
            Encoding: `{result.Encoding}`

            {SummaryTable(result)}

            Overall token difference: {Percentage.Direction(result.TokenDifferencePercent)}

            ## By Category

            {Breakdown(result, scenario => scenario.Category)}

            ## By Complexity

            {Breakdown(result, scenario => scenario.Complexity)}

            ## Scaling Samples

            {Scaling(result)}
            """;
    }

    public static string SummaryTable(BenchmarkRunResult result)
    {
        return Table("Metric", [
            Row("Total tokens", result.Scenarios.Sum(s => s.Enzo.Tokens), result.Scenarios.Sum(s => s.Mermaid.Tokens)),
            Row("Average tokens", result.Scenarios.Average(s => s.Enzo.Tokens), result.Scenarios.Average(s => s.Mermaid.Tokens)),
            Row("Median tokens", Median(result.Scenarios.Select(s => s.Enzo.Tokens)), Median(result.Scenarios.Select(s => s.Mermaid.Tokens))),
            Row("Characters", result.Scenarios.Sum(s => s.Enzo.Characters), result.Scenarios.Sum(s => s.Mermaid.Characters)),
            Row("UTF-8 bytes", result.Scenarios.Sum(s => s.Enzo.Utf8Bytes), result.Scenarios.Sum(s => s.Mermaid.Utf8Bytes)),
            Row("Non-empty lines", result.Scenarios.Sum(s => s.Enzo.NonEmptyLines), result.Scenarios.Sum(s => s.Mermaid.NonEmptyLines))]);
    }

    private static string Breakdown(BenchmarkRunResult result, Func<ScenarioBenchmarkResult, string> groupBy)
    {
        var rows = result.Scenarios.GroupBy(groupBy).OrderBy(group => group.Key).Select(group =>
            $"| {group.Key} | {group.Count()} | {group.Sum(s => s.Enzo.Tokens)} | {group.Sum(s => s.Mermaid.Tokens)} | {Percentage.Direction(Percentage.Difference(group.Sum(s => s.Enzo.Tokens), group.Sum(s => s.Mermaid.Tokens)))} |");

        return string.Join(Environment.NewLine, ["| Group | Scenarios | Enzo tokens | Mermaid tokens | Difference |", "| --- | ---: | ---: | ---: | --- |", .. rows]);
    }

    private static string Scaling(BenchmarkRunResult result)
    {
        var rows = result.Scenarios
            .OrderBy(scenario => scenario.EnzoStructure.ElementCount)
            .ThenBy(scenario => scenario.Id, StringComparer.Ordinal)
            .Select(scenario => $"| {scenario.Id} | {scenario.EnzoStructure.ElementCount} | {scenario.EnzoStructure.ConnectionCount} | {scenario.Enzo.Tokens} | {scenario.Mermaid.Tokens} |");

        return string.Join(Environment.NewLine, ["| Scenario | Elements | Connections | Enzo tokens | Mermaid tokens |", "| --- | ---: | ---: | ---: | ---: |", .. rows]);
    }

    private static string Table(string firstHeader, IEnumerable<(string Name, string Enzo, string Mermaid)> rows)
    {
        return string.Join(Environment.NewLine, [$"| {firstHeader} | Enzo | Mermaid |", "| --- | ---: | ---: |", .. rows.Select(row => $"| {row.Name} | {row.Enzo} | {row.Mermaid} |")]);
    }

    private static (string Name, string Enzo, string Mermaid) Row(string name, double enzo, double mermaid)
    {
        return (name, enzo.ToString("0.#", CultureInfo.InvariantCulture), mermaid.ToString("0.#", CultureInfo.InvariantCulture));
    }

    private static double Median(IEnumerable<int> values)
    {
        var ordered = values.Order().ToArray();
        var middle = ordered.Length / 2;
        return ordered.Length % 2 == 0 ? (ordered[middle - 1] + ordered[middle]) / 2.0 : ordered[middle];
    }
}
