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
    bool IsValid,
    bool RenderSuccess,
    string? Error);

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
    long DurationMs);

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
    public int RepairAttempts => Attempts.Count(attempt => attempt.IsRepair);
    public int RepairInputTokens => Attempts.Where(attempt => attempt.IsRepair).Sum(attempt => attempt.InputTokens);
    public int RepairOutputTokens => Attempts.Where(attempt => attempt.IsRepair).Sum(attempt => attempt.OutputTokens);
    public int TotalRepairTokens => Attempts.Where(attempt => attempt.IsRepair).Sum(attempt => attempt.TotalTokens);
    public int TokensToValidDiagram => Attempts.Sum(attempt => attempt.InputTokens + attempt.OutputTokens);
    public bool RenderSuccess => Attempts.LastOrDefault()?.RenderSuccess == true;
    public bool FinalValid => Attempts.LastOrDefault()?.Valid == true;
    public bool NormalizationApplied => Attempts.Any(attempt => attempt.NormalizationApplied);
    private GenerationAttempt? InitialAttempt => Attempts.FirstOrDefault(attempt => !attempt.IsRepair);
}

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
    string LanguageFilter);

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
