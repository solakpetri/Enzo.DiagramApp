using Enzo.Diagrams.Domain;
using Enzo.Diagrams.Infrastructure;

namespace Enzo.Diagrams.Benchmarks;

public static class BenchmarkRunner
{
    public static BenchmarkRunResult Run(string scenariosDirectory, string encodingName = "cl100k_base")
    {
        var calculator = new MetricCalculator(encodingName);
        var results = ScenarioLoader.Load(scenariosDirectory).Select(scenario => RunScenario(scenario, calculator)).ToList();
        return new BenchmarkRunResult(encodingName, results);
    }

    public static ScenarioBenchmarkResult RunScenario(DiagramScenario scenario, MetricCalculator calculator)
    {
        var parsed = DiagramParser.Parse(scenario.Enzo);
        if (!parsed.IsSuccess)
        {
            throw new BenchmarkValidationException($"Invalid Enzo fixture '{scenario.Id}': {Errors(parsed)}");
        }

        _ = DiagramSvgRenderer.Render(parsed);

        var enzoStructure = EnzoStructureReader.Read(parsed);
        var mermaidStructure = MermaidStructureReader.Read(scenario.Mermaid, scenario.Category);
        var equivalenceErrors = DiagramEquivalence.Compare(enzoStructure, mermaidStructure);
        if (equivalenceErrors.Count > 0)
        {
            throw new BenchmarkValidationException($"Scenario '{scenario.Id}' is not structurally equivalent: {string.Join("; ", equivalenceErrors)}");
        }

        var enzoMetrics = calculator.Calculate(scenario.Enzo, enzoStructure.ElementCount, enzoStructure.ConnectionCount);
        var mermaidMetrics = calculator.Calculate(scenario.Mermaid, mermaidStructure.ElementCount, mermaidStructure.ConnectionCount);

        return new ScenarioBenchmarkResult(
            scenario.Id,
            scenario.Category,
            scenario.Complexity,
            enzoMetrics,
            mermaidMetrics,
            Percentage.Difference(enzoMetrics.Tokens, mermaidMetrics.Tokens),
            enzoStructure,
            mermaidStructure);
    }

    private static string Errors(DiagramParseResult result)
    {
        return string.Join("; ", result.Errors.Select(error => error.Message).Concat(result.ValidationErrors.Select(error => error.Message)));
    }
}
