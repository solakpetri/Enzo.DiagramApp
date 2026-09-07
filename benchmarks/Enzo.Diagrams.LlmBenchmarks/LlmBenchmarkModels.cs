namespace Enzo.Diagrams.LlmBenchmarks;

public static class DiagramLanguages
{
    public const string Enzo = "enzo";
    public const string Mermaid = "mermaid";
}

public sealed record ModelSettings(
    string Model,
    double Temperature,
    double TopP,
    int MaxOutputTokens);

public sealed record TokenUsage(int InputTokens, int OutputTokens, int TotalTokens);

public sealed record ModelResponse(
    string Source,
    TokenUsage Usage,
    TimeSpan Duration);

public sealed record ValidationOutcome(
    bool SyntaxValid,
    bool RenderSuccess,
    string? Error)
{
    public bool IsValid => SyntaxValid && RenderSuccess;
}

public sealed record GenerationAttempt(
    int AttemptNumber,
    bool IsRepair,
    int InputTokens,
    int OutputTokens,
    int TotalTokens,
    string Source,
    string NormalizedSource,
    bool NormalizationApplied,
    bool Valid,
    bool RenderSuccess,
    string? ValidationError,
    long DurationMs,
    bool SyntaxValid = false,
    bool RenderValid = false,
    bool KindValid = false,
    bool StructureValid = false,
    bool SemanticValid = false,
    bool EquivalentValid = false,
    IReadOnlyList<string>? FailureReasons = null,
    SemanticValidationDiagnostics? SemanticDiagnostics = null,
    string? RepairType = null);

public sealed record LlmRunResult(
    string ScenarioId,
    string Category,
    string Complexity,
    string Language,
    string Model,
    int RunNumber,
    IReadOnlyList<GenerationAttempt> Attempts,
    string? ErrorCategory)
{
    public int InputTokens => InitialAttempt?.InputTokens ?? 0;
    public int OutputTokens => InitialAttempt?.OutputTokens ?? 0;
    public int TotalTokens => InitialAttempt?.TotalTokens ?? 0;
    public bool FirstPassValid => InitialAttempt?.Valid == true;
    public bool FirstPassEquivalentValid => InitialAttempt?.EquivalentValid == true;
    public int RepairAttempts => Attempts.Count(attempt => attempt.IsRepair);
    public int RepairInputTokens => Attempts.Where(attempt => attempt.IsRepair).Sum(attempt => attempt.InputTokens);
    public int RepairOutputTokens => Attempts.Where(attempt => attempt.IsRepair).Sum(attempt => attempt.OutputTokens);
    public int TotalRepairTokens => Attempts.Where(attempt => attempt.IsRepair).Sum(attempt => attempt.TotalTokens);
    public int TokensToValidDiagram
    {
        get
        {
            var index = Attempts.ToList().FindIndex(attempt => attempt.Valid);
            return index < 0 ? Attempts.Sum(attempt => attempt.InputTokens + attempt.OutputTokens) : Attempts.Take(index + 1).Sum(attempt => attempt.InputTokens + attempt.OutputTokens);
        }
    }
    public int? TokensToValidEquivalentDiagram
    {
        get
        {
            var index = Attempts.ToList().FindIndex(attempt => attempt.EquivalentValid);
            return index < 0 ? null : Attempts.Take(index + 1).Sum(attempt => attempt.InputTokens + attempt.OutputTokens);
        }
    }

    public bool SyntaxValid => FinalAttempt?.SyntaxValid == true;
    public bool RenderSuccess => Attempts.LastOrDefault()?.RenderSuccess == true;
    public bool RenderValid => FinalAttempt?.RenderValid == true;
    public bool KindValid => FinalAttempt?.KindValid == true;
    public bool StructureValid => FinalAttempt?.StructureValid == true;
    public bool SemanticValid => FinalAttempt?.SemanticValid == true;
    public bool EquivalentValid => FinalAttempt?.EquivalentValid == true;
    public IReadOnlyList<string> FailureReasons => FinalAttempt?.FailureReasons ?? [];
    public bool FinalValid => Attempts.LastOrDefault()?.Valid == true;
    public bool NormalizationApplied => Attempts.Any(attempt => attempt.NormalizationApplied);
    public string? RepairStoppedReason { get; init; }
    private GenerationAttempt? InitialAttempt => Attempts.FirstOrDefault(attempt => !attempt.IsRepair);
    private GenerationAttempt? FinalAttempt => Attempts.LastOrDefault();
}

public sealed record PromptAudit(
    string EnzoSystemPrompt,
    string MermaidSystemPrompt,
    string SharedSemanticTaskTemplate,
    string EnzoLanguageSpecificAdditions,
    string MermaidLanguageSpecificAdditions,
    double CostComparisonEquivalentValidityThresholdPercent);

public sealed record LlmBenchmarkMetadata(
    string RunId,
    DateTimeOffset StartedAt,
    string Model,
    int RunsPerScenario,
    int MaxRepairAttempts,
    double Temperature,
    double TopP,
    int MaxOutputTokens,
    string ScenarioFilter,
    string CategoryFilter,
    string LanguageFilter,
    PromptAudit? PromptAudit = null);

public sealed record LlmBenchmarkRun(
    LlmBenchmarkMetadata Metadata,
    List<LlmRunResult> Results);

public interface IDiagramModelClient
{
    Task<ModelResponse> CompleteAsync(string systemPrompt, string userPrompt, ModelSettings settings, CancellationToken cancellationToken);
}

public interface IDiagramValidator
{
    Task<ValidationOutcome> ValidateAsync(string source, CancellationToken cancellationToken);
}
