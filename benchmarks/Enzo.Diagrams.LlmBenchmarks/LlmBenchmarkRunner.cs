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
        if (run.Metadata.PromptAudit is null)
        {
            run = run with { Metadata = run.Metadata with { PromptAudit = prompts.CreateAudit() } };
        }

        var settings = new ModelSettings(options.Model, options.Temperature, options.TopP, options.MaxOutputTokens);
        var scenarios = ScenarioLoader.Load(SuiteDirectory(scenariosDirectory, options.Suite))
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
        string? repairStoppedReason = null;
        try
        {
            var systemPrompt = prompts.GetPrompt(language);
            var repairSystemPrompt = prompts.GetRepairPrompt(language);
            string? pendingEscalation = null;
            attempts.Add(await GenerateAttemptAsync(scenario, language, systemPrompt, GenerationPromptBuilder.BuildUserPrompt(scenario, language), settings, 0, false, null, null, null, cancellationToken));
            for (var repair = 1; attempts.Last().EquivalentValid is false && repair <= maxRepairAttempts; repair++)
            {
                var failedAttempt = attempts.Last();
                var repairType = GenerationPromptBuilder.RepairTypeFor(failedAttempt);
                var failureCategory = language == DiagramLanguages.Enzo ? EnzoRepairPromptBuilder.FailureCategoryFor(failedAttempt) : null;
                var escalation = pendingEscalation;
                pendingEscalation = null;
                var userPrompt = language == DiagramLanguages.Enzo
                    ? EnzoRepairPromptBuilder.BuildRepairPrompt(attempts, escalation)
                    : GenerationPromptBuilder.BuildRepairPrompt(language, failedAttempt);
                attempts.Add(await GenerateAttemptAsync(scenario, language, repairSystemPrompt, userPrompt, settings, repair, true, repairType, failureCategory, failedAttempt, cancellationToken));

                var stagnation = DetectRepairStagnation(attempts);
                if (language != DiagramLanguages.Enzo)
                {
                    repairStoppedReason = stagnation;
                    if (repairStoppedReason is not null)
                    {
                        break;
                    }

                    continue;
                }

                if (escalation == EnzoRepairPromptBuilder.Oscillation && !attempts.Last().EquivalentValid)
                {
                    repairStoppedReason = EnzoRepairPromptBuilder.Oscillation;
                    break;
                }

                if (stagnation is null)
                {
                    continue;
                }

                if (escalation is not null || repair == maxRepairAttempts)
                {
                    repairStoppedReason = stagnation;
                    break;
                }

                pendingEscalation = stagnation;
            }

            return Result(scenario, language, settings.Model, runNumber, attempts, attempts.Last().EquivalentValid ? null : repairStoppedReason ?? "validation", repairStoppedReason);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            return Result(scenario, language, settings.Model, runNumber, attempts, "api", repairStoppedReason);
        }
    }

    private async Task<GenerationAttempt> GenerateAttemptAsync(
        DiagramScenario scenario,
        string language,
        string systemPrompt,
        string userPrompt,
        ModelSettings settings,
        int attemptNumber,
        bool isRepair,
        string? repairType,
        string? repairFailureCategory,
        GenerationAttempt? failedAttempt,
        CancellationToken cancellationToken)
    {
        var response = await modelClient.CompleteAsync(systemPrompt, userPrompt, settings, cancellationToken);
        var normalized = SourceNormalizer.RemoveMarkdownFence(response.Source);
        var validation = await validators[language].ValidateAsync(normalized.Source, cancellationToken);
        var semantic = SemanticDiagramValidator.Validate(scenario, language, normalized.Source, validation.SyntaxValid, validation.RenderSuccess);
        var failureCategories = SequenceFailureClassifier.Classify(scenario, normalized.Source, validation, semantic);
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
            (long)response.Duration.TotalMilliseconds,
            validation.SyntaxValid,
            validation.RenderSuccess,
            semantic.KindValid,
            semantic.StructureValid,
            semantic.SemanticValid,
            semantic.EquivalentValid,
            semantic.FailureReasons,
            semantic.Diagnostics,
            repairType,
            repairFailureCategory,
            failedAttempt is null ? null : RepairResolved(failedAttempt, validation, semantic),
            failedAttempt?.SyntaxValid == true && !validation.SyntaxValid,
            failureCategories);
    }

    private static bool RepairResolved(GenerationAttempt failedAttempt, ValidationOutcome validation, SemanticValidationResult semantic)
    {
        if (!failedAttempt.SyntaxValid)
        {
            return validation.SyntaxValid;
        }

        if (!failedAttempt.RenderValid)
        {
            return validation.SyntaxValid && validation.RenderSuccess;
        }

        return validation.SyntaxValid && validation.RenderSuccess && semantic.EquivalentValid;
    }

    private static string? DetectRepairStagnation(IReadOnlyList<GenerationAttempt> attempts)
    {
        if (attempts.Count < 2)
        {
            return null;
        }

        if (attempts[^1].NormalizedSource == attempts[^2].NormalizedSource)
        {
            return EnzoRepairPromptBuilder.IdenticalOutput;
        }

        return attempts.Count >= 3 && attempts[^1].NormalizedSource == attempts[^3].NormalizedSource ? EnzoRepairPromptBuilder.Oscillation : null;
    }

    private static LlmRunResult Result(DiagramScenario scenario, string language, string model, int runNumber, IReadOnlyList<GenerationAttempt> attempts, string? errorCategory, string? repairStoppedReason) =>
        new(scenario.Id, scenario.Category, scenario.Complexity, language, model, runNumber, attempts, errorCategory)
        {
            RepairStoppedReason = repairStoppedReason,
            ExpectedMinimumInteractionCount = scenario.Expectations.MinimumInteractionCount
        };

    private static string SuiteDirectory(string scenariosDirectory, string suite) => suite switch
    {
        "all" => scenariosDirectory,
        "sequence" => Path.Combine(scenariosDirectory, "sequence-generation"),
        _ => Path.Combine(scenariosDirectory, suite)
    };

    private static IReadOnlyList<string> Languages(string filter) => filter switch
    {
        DiagramLanguages.Enzo => [DiagramLanguages.Enzo],
        DiagramLanguages.Mermaid => [DiagramLanguages.Mermaid],
        "all" => [DiagramLanguages.Enzo, DiagramLanguages.Mermaid],
        _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, "Language must be all, enzo, or mermaid.")
    };
}
