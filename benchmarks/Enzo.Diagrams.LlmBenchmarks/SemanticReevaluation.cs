using System.Text.Json;
using Enzo.Diagrams.Benchmarks;

namespace Enzo.Diagrams.LlmBenchmarks;

public static class SemanticReevaluation
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public static string Run(string persistedRunPath, string scenariosDirectory)
    {
        var run = JsonSerializer.Deserialize<LlmBenchmarkRun>(File.ReadAllText(persistedRunPath), JsonOptions)
            ?? throw new InvalidOperationException($"Could not read persisted run: {persistedRunPath}");
        var scenarios = ScenarioLoader.Load(scenariosDirectory).ToDictionary(scenario => scenario.Id, StringComparer.Ordinal);
        var enriched = run with { Results = run.Results.Select(result => ReevaluateResult(result, scenarios)).ToList() };
        var outputPath = Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(persistedRunPath)) ?? Environment.CurrentDirectory,
            $"{Path.GetFileNameWithoutExtension(persistedRunPath)}-semantic.json");
        BenchmarkOutputWriter.WriteToPath(enriched, outputPath);
        return outputPath;
    }

    private static LlmRunResult ReevaluateResult(LlmRunResult result, IReadOnlyDictionary<string, DiagramScenario> scenarios)
    {
        if (!scenarios.TryGetValue(result.ScenarioId, out var scenario))
        {
            throw new InvalidOperationException($"Persisted result references unknown scenario: {result.ScenarioId}");
        }

        return result with { Attempts = result.Attempts.Select(attempt => ReevaluateAttempt(attempt, scenario, result.Language)).ToList() };
    }

    private static GenerationAttempt ReevaluateAttempt(GenerationAttempt attempt, DiagramScenario scenario, string language)
    {
        var syntaxValid = attempt.SyntaxValid || attempt.Valid;
        var renderValid = attempt.RenderValid || attempt.RenderSuccess;
        var semantic = SemanticDiagramValidator.Validate(scenario, language, attempt.NormalizedSource, syntaxValid, renderValid);
        return attempt with
        {
            SyntaxValid = syntaxValid,
            RenderValid = renderValid,
            KindValid = semantic.KindValid,
            StructureValid = semantic.StructureValid,
            SemanticValid = semantic.SemanticValid,
            EquivalentValid = semantic.EquivalentValid,
            FailureReasons = semantic.FailureReasons,
            SemanticDiagnostics = semantic.Diagnostics
        };
    }
}
