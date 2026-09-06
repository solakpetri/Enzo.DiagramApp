using Enzo.Diagrams.Benchmarks;

namespace Enzo.Diagrams.LlmBenchmarks;

public sealed class LlmBenchmarkRunner(
    IDiagramModelClient modelClient,
    IReadOnlyDictionary<string, IDiagramValidator> validators,
    PromptStore prompts,
    BenchmarkOutputWriter writer)
{
    public async Task<LlmBenchmarkRun> RunAsync(string scenariosDirectory, BenchmarkOptions options, CancellationToken cancellationToken)
    {
        var run = writer.LoadOrCreate(options);
        var settings = new ModelSettings(options.Model, options.Temperature, options.TopP, options.MaxOutputTokens);
        var scenarios = ScenarioLoader.Load(scenariosDirectory)
            .Where(s => options.ScenarioFilter is null || s.Id.Equals(options.ScenarioFilter, StringComparison.OrdinalIgnoreCase))
            .Where(s => options.CategoryFilter is null || s.Category.Equals(options.CategoryFilter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var scenario in scenarios)
        foreach (var language in Languages(options.LanguageFilter))
        for (var runNumber = 1; runNumber <= options.Runs; runNumber++)
        {
            if (run.Results.Any(r => r.ScenarioId == scenario.Id && r.Language == language && r.RunNumber == runNumber && r.Model == options.Model))
            {
                continue;
            }

            run.Results.Add(await RunOneAsync(scenario, language, runNumber, settings, options.MaxRepairAttempts, cancellationToken));
            writer.Write(run);
        }

        writer.Write(run);
        return run;
    }

    private async Task<LlmRunResult> RunOneAsync(DiagramScenario scenario, string language, int runNumber, ModelSettings settings, int maxRepairAttempts, CancellationToken cancellationToken)
    {
        var attempts = new List<GenerationAttempt>();
        try
        {
            var systemPrompt = prompts.GetPrompt(language);
            attempts.Add(await GenerateAttemptAsync(language, systemPrompt, scenario.Prompt, settings, 0, false, cancellationToken));
            for (var repair = 1; attempts.Last().Valid is false && repair <= maxRepairAttempts; repair++)
            {
                var userPrompt = RepairPrompt(attempts.Last().NormalizedSource, attempts.Last().ValidationError);
                attempts.Add(await GenerateAttemptAsync(language, systemPrompt, userPrompt, settings, repair, true, cancellationToken));
            }

            return Result(scenario, language, settings.Model, runNumber, attempts, attempts.Last().Valid ? null : "validation");
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            return Result(scenario, language, settings.Model, runNumber, attempts, "api");
        }
    }

    private async Task<GenerationAttempt> GenerateAttemptAsync(string language, string systemPrompt, string userPrompt, ModelSettings settings, int attemptNumber, bool isRepair, CancellationToken cancellationToken)
    {
        var response = await modelClient.CompleteAsync(systemPrompt, userPrompt, settings, cancellationToken);
        var normalized = SourceNormalizer.RemoveMarkdownFence(response.Source);
        var validation = await validators[language].ValidateAsync(normalized.Source, cancellationToken);
        return new GenerationAttempt(
            attemptNumber,
            isRepair,
            response.Usage.InputTokens,
            response.Usage.OutputTokens,
            response.Usage.TotalTokens,
            response.Source,
            normalized.Source,
            normalized.Applied,
            validation.IsValid,
            validation.RenderSuccess,
            validation.Error,
            (long)response.Duration.TotalMilliseconds);
    }

    private static string RepairPrompt(string source, string? error) =>
        $"The diagram source below is invalid. Correct it and return only the corrected diagram source.\n\nValidation error:\n{error}\n\nInvalid source:\n{source}";

    private static LlmRunResult Result(DiagramScenario scenario, string language, string model, int runNumber, IReadOnlyList<GenerationAttempt> attempts, string? errorCategory) =>
        new(scenario.Id, scenario.Category, scenario.Complexity, language, model, runNumber, attempts, errorCategory);

    private static IReadOnlyList<string> Languages(string filter) => filter switch
    {
        DiagramLanguages.Enzo => [DiagramLanguages.Enzo],
        DiagramLanguages.Mermaid => [DiagramLanguages.Mermaid],
        "all" => [DiagramLanguages.Enzo, DiagramLanguages.Mermaid],
        _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, "Language must be all, enzo, or mermaid.")
    };
}
