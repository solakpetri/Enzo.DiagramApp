using System.Text;

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
    string? RepairType = null,
    string? RepairFailureCategory = null,
    bool? RepairSuccessful = null,
    bool IntroducedSyntaxFailure = false,
    IReadOnlyList<string>? FailureCategories = null);

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
    public IReadOnlyList<string> SequenceFailureCategories => FinalAttempt?.FailureCategories ?? [];
    public bool FinalValid => Attempts.LastOrDefault()?.Valid == true;
    public bool NormalizationApplied => Attempts.Any(attempt => attempt.NormalizationApplied);
    public string? RepairStoppedReason { get; init; }
    public int? ExpectedMinimumInteractionCount { get; init; }
    public int SourceCharacters => EquivalentAttempt?.NormalizedSource.Length ?? 0;
    public int SourceBytes => EquivalentAttempt is null ? 0 : Encoding.UTF8.GetByteCount(EquivalentAttempt.NormalizedSource);
    public int NonEmptyLines => EquivalentAttempt?.NormalizedSource.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Count(line => line.Trim().Length > 0) ?? 0;
    public int EquivalentOutputTokens => EquivalentAttempt?.OutputTokens ?? 0;
    public int Participants => EquivalentAttempt?.SemanticDiagnostics?.ActualParticipants ?? 0;
    public int Interactions => EquivalentAttempt?.SemanticDiagnostics?.ActualInteractions ?? 0;
    public double? OutputTokensPerParticipant => Participants == 0 || EquivalentAttempt is null ? null : EquivalentAttempt.OutputTokens / (double)Participants;
    public double? OutputTokensPerInteraction => Interactions == 0 || EquivalentAttempt is null ? null : EquivalentAttempt.OutputTokens / (double)Interactions;
    public double? TokensToValidEquivalentDiagramPerInteraction => Interactions == 0 || TokensToValidEquivalentDiagram is null ? null : TokensToValidEquivalentDiagram.Value / (double)Interactions;
    public string InteractionSizeBucket => ExpectedMinimumInteractionCount switch
    {
        <= 6 => "small",
        <= 12 => "medium",
        > 12 => "large",
        _ => "unknown"
    };
    private GenerationAttempt? InitialAttempt => Attempts.FirstOrDefault(attempt => !attempt.IsRepair);
    private GenerationAttempt? FinalAttempt => Attempts.LastOrDefault();
    private GenerationAttempt? EquivalentAttempt => Attempts.FirstOrDefault(attempt => attempt.EquivalentValid);
}

public sealed record PromptAudit(
    string EnzoSystemPrompt,
    string MermaidSystemPrompt,
    string SharedSemanticTaskTemplate,
    string EnzoLanguageSpecificAdditions,
    string MermaidLanguageSpecificAdditions,
    double CostComparisonEquivalentValidityThresholdPercent,
    string? EnzoRepairSystemPrompt = null,
    string? EnzoSequenceSystemPrompt = null);

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
    PromptAudit? PromptAudit = null,
    string Suite = "all");

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
