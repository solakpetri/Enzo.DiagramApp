using System.Globalization;
using System.Text.Json;

namespace Enzo.Diagrams.LlmBenchmarks;

public sealed class BenchmarkOutputWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private string? _jsonPath;

    public LlmBenchmarkRun LoadOrCreate(BenchmarkOptions runOptions)
    {
        Directory.CreateDirectory(runOptions.OutputDirectory);
        if (runOptions.ResumeFile is not null)
        {
            _jsonPath = Path.GetFullPath(runOptions.ResumeFile);
            return JsonSerializer.Deserialize<LlmBenchmarkRun>(File.ReadAllText(_jsonPath), JsonOptions)
                ?? throw new InvalidOperationException($"Could not read resume file: {_jsonPath}");
        }

        var runId = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        _jsonPath = Path.Combine(runOptions.OutputDirectory, $"{OutputPrefix(runOptions.Suite)}-{runId}-{Safe(runOptions.Model)}.json");
        return new LlmBenchmarkRun(new LlmBenchmarkMetadata(
            runId,
            DateTimeOffset.UtcNow,
            runOptions.Model,
            runOptions.Runs,
            runOptions.MaxRepairAttempts,
            runOptions.Temperature,
            runOptions.TopP,
            runOptions.MaxOutputTokens,
            runOptions.ScenarioFilter ?? "all",
            runOptions.CategoryFilter ?? "all",
            runOptions.LanguageFilter,
            Suite: runOptions.Suite), []);
    }

    public void Write(LlmBenchmarkRun run)
    {
        var jsonPath = _jsonPath ?? throw new InvalidOperationException("Output file has not been initialized.");
        WriteToPath(run, jsonPath);
    }

    public static void WriteToPath(LlmBenchmarkRun run, string jsonPath)
    {
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(run, JsonOptions));
        File.WriteAllText(Path.ChangeExtension(jsonPath, ".csv"), ToCsv(run));
        File.WriteAllText(Path.ChangeExtension(jsonPath, ".md"), LlmMarkdownReport.Generate(run));
    }

    private static string ToCsv(LlmBenchmarkRun run)
    {
        var rows = run.Results.OrderBy(r => r.ScenarioId).ThenBy(r => r.Language).ThenBy(r => r.RunNumber).Select(r => string.Join(',', [
            Csv(r.ScenarioId), r.Category, r.Complexity, r.Language, Csv(r.Model), r.RunNumber.ToString(CultureInfo.InvariantCulture),
            r.InputTokens.ToString(CultureInfo.InvariantCulture), r.OutputTokens.ToString(CultureInfo.InvariantCulture), r.TotalTokens.ToString(CultureInfo.InvariantCulture),
            r.FirstPassValid.ToString(), r.FirstPassEquivalentValid.ToString(), r.RepairAttempts.ToString(CultureInfo.InvariantCulture), r.RepairInputTokens.ToString(CultureInfo.InvariantCulture),
            r.RepairOutputTokens.ToString(CultureInfo.InvariantCulture), r.TotalRepairTokens.ToString(CultureInfo.InvariantCulture),
            r.TokensToValidDiagram.ToString(CultureInfo.InvariantCulture), Csv(r.TokensToValidEquivalentDiagram?.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
            r.SyntaxValid.ToString(), r.RenderSuccess.ToString(), r.RenderValid.ToString(), r.KindValid.ToString(), r.StructureValid.ToString(), r.SemanticValid.ToString(), r.EquivalentValid.ToString(),
            r.FinalValid.ToString(), r.NormalizationApplied.ToString(), r.SourceCharacters.ToString(CultureInfo.InvariantCulture), r.SourceBytes.ToString(CultureInfo.InvariantCulture),
            r.NonEmptyLines.ToString(CultureInfo.InvariantCulture), r.EquivalentOutputTokens.ToString(CultureInfo.InvariantCulture), r.Participants.ToString(CultureInfo.InvariantCulture),
            r.Interactions.ToString(CultureInfo.InvariantCulture), Csv(r.OutputTokensPerParticipant?.ToString("0.###", CultureInfo.InvariantCulture) ?? string.Empty),
            Csv(r.OutputTokensPerInteraction?.ToString("0.###", CultureInfo.InvariantCulture) ?? string.Empty), Csv(r.TokensToValidEquivalentDiagramPerInteraction?.ToString("0.###", CultureInfo.InvariantCulture) ?? string.Empty),
            Csv(r.InteractionSizeBucket), Csv(string.Join("; ", r.SequenceFailureCategories)), Csv(string.Join("; ", r.Attempts.Where(a => a.IsRepair).Select(a => a.RepairType).Where(t => t is not null))),
            Csv(string.Join("; ", r.Attempts.Where(a => a.IsRepair).Select(a => a.RepairFailureCategory).Where(c => c is not null))),
            Csv(string.Join("; ", r.Attempts.Where(a => a.IsRepair).Select(a => a.RepairSuccessful?.ToString() ?? string.Empty))),
            r.Attempts.Count(a => a.IntroducedSyntaxFailure).ToString(CultureInfo.InvariantCulture), Csv(r.RepairStoppedReason ?? string.Empty), Csv(string.Join("; ", r.FailureReasons)), Csv(r.ErrorCategory ?? string.Empty)]));

        return string.Join(Environment.NewLine, ["scenarioId,category,complexity,language,model,runNumber,inputTokens,outputTokens,totalTokens,firstPassValid,firstPassEquivalentValid,repairAttempts,repairInputTokens,repairOutputTokens,totalRepairTokens,tokensToValidDiagram,tokensToValidEquivalentDiagram,syntaxValid,renderSuccess,renderValid,kindValid,structureValid,semanticValid,equivalentValid,finalValid,normalizationApplied,sourceCharacters,sourceBytes,nonEmptyLines,equivalentOutputTokens,participants,interactions,outputTokensPerParticipant,outputTokensPerInteraction,ttvedPerInteraction,interactionSizeBucket,sequenceFailureCategories,repairTypes,repairFailureCategories,repairSuccesses,repairsIntroducedSyntaxFailure,repairStoppedReason,failureReasons,errorCategory", .. rows]);
    }

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    private static string Safe(string value) => string.Concat(value.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-'));
    private static string OutputPrefix(string suite) => suite == "sequence" ? "sequence-generation" : "llm-generation-cost";
}
