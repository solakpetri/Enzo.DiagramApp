using System.Text;
using Enzo.Diagrams.Benchmarks;

namespace Enzo.Diagrams.LlmBenchmarks;

public sealed record GenerationContract(
    string ExpectedKind,
    IReadOnlyList<string> RequiredConcepts,
    int? MinimumNodeCount,
    int? MinimumEdgeCount,
    int? MinimumDecisionCount,
    int? MinimumParticipantCount,
    int? MinimumInteractionCount,
    IReadOnlyList<string> RequiredParticipants,
    IReadOnlyList<RequiredInteraction> RequiredInteractions)
{
    public static GenerationContract From(DiagramScenario scenario) => new(
        scenario.Expectations.ExpectedKind,
        scenario.Expectations.RequiredConcepts,
        scenario.Expectations.MinimumNodeCount,
        scenario.Expectations.MinimumEdgeCount,
        scenario.Expectations.MinimumDecisionCount,
        scenario.Expectations.MinimumParticipantCount,
        scenario.Expectations.MinimumInteractionCount,
        scenario.Expectations.RequiredParticipants ?? [],
        scenario.Expectations.RequiredInteractions ?? []);
}

public static class GenerationPromptBuilder
{
    public const string SharedSemanticTaskTemplate = "Create a {kind} diagram for this request, preserve the original request text, then satisfy only the listed requirements.";
    public const string EnzoLanguageSpecificAdditions = "Use the requested Enzo diagram keyword for the benchmark kind.";
    public const string MermaidLanguageSpecificAdditions = "Use the requested Mermaid diagram declaration for the benchmark kind.";

    public static string BuildUserPrompt(DiagramScenario scenario, string language)
    {
        var contract = GenerationContract.From(scenario);
        var isEnzoSequence = language == DiagramLanguages.Enzo && contract.ExpectedKind == "sequence";
        var builder = new StringBuilder();
        builder.AppendLine($"Create a {contract.ExpectedKind} diagram for this request:");
        builder.AppendLine();
        builder.AppendLine(scenario.Prompt);
        builder.AppendLine();
        builder.AppendLine("Requirements:");
        builder.AppendLine($"- Diagram kind: {contract.ExpectedKind}");
        if (contract.RequiredConcepts.Count > 0)
        {
            builder.AppendLine(isEnzoSequence ? "- Represent these concepts:" : "- Include these concepts:");
            foreach (var concept in contract.RequiredConcepts)
            {
                builder.AppendLine($"  - {concept}");
            }
        }

        if (contract.RequiredParticipants.Count > 0)
        {
            builder.AppendLine(isEnzoSequence ? "- Mandatory participants:" : "- Include these participants:");
            foreach (var participant in contract.RequiredParticipants)
            {
                builder.AppendLine($"  - {participant}");
            }
        }

        if (contract.RequiredInteractions.Count > 0)
        {
            builder.AppendLine(isEnzoSequence ? "- Required interactions:" : "- Include these participant interactions:");
            foreach (var interaction in contract.RequiredInteractions)
            {
                builder.AppendLine($"  - {interaction.From} -> {interaction.To}{(interaction.Label is null ? string.Empty : $": {interaction.Label}")}");
            }
        }

        foreach (var (value, label) in Minimums(contract))
        {
            if (value is not null)
            {
                builder.AppendLine(isEnzoSequence && label == "interactions" ? $"- Use at least {value} interactions" : $"- At least {value} {label}");
            }
        }
        builder.AppendLine();
        builder.AppendLine(SourceOnlyInstruction(language, contract.ExpectedKind));
        if (!isEnzoSequence)
        {
            builder.AppendLine(LanguageKindInstruction(language, contract.ExpectedKind));
        }

        return builder.ToString().TrimEnd();
    }

    public static string BuildRepairPrompt(string language, GenerationAttempt failedAttempt)
    {
        var problems = RepairProblems(failedAttempt).ToList();
        var builder = new StringBuilder();
        builder.AppendLine("The diagram source does not satisfy the request.");
        builder.AppendLine();
        builder.AppendLine("Problems:");
        foreach (var problem in problems.Count == 0 ? failedAttempt.FailureReasons ?? [] : problems)
        {
            builder.AppendLine($"- {problem}");
        }

        builder.AppendLine();
        builder.AppendLine(LanguageKindInstruction(language, failedAttempt.SemanticDiagnostics?.ExpectedKind));
        builder.AppendLine("Return the complete corrected source only.");
        builder.AppendLine();
        builder.AppendLine("Current source:");
        builder.AppendLine(failedAttempt.NormalizedSource);
        return builder.ToString().TrimEnd();
    }

    public static string? RepairTypeFor(GenerationAttempt failedAttempt)
    {
        if (!failedAttempt.SyntaxValid)
        {
            return "Syntax";
        }

        if (!failedAttempt.RenderValid)
        {
            return "Render";
        }

        var types = new List<string>();
        if (!failedAttempt.KindValid)
        {
            types.Add("Kind");
        }

        if (!failedAttempt.StructureValid)
        {
            types.Add("Structure");
        }

        if (!failedAttempt.SemanticValid)
        {
            types.Add("Semantic");
        }

        return types.Count == 0 ? null : string.Join('+', types);
    }

    public static string LanguageKindInstruction(string language, string? expectedKind) => (language, expectedKind) switch
    {
        (DiagramLanguages.Enzo, "sequence") => "Use Enzo `sequence` syntax, not `flow` or `bpmn`.",
        (DiagramLanguages.Enzo, "flow") => "Use Enzo `flow` syntax, not `sequence` or `bpmn`.",
        (DiagramLanguages.Enzo, "process") => "Use Enzo `bpmn` syntax, not `flow` or `sequence`.",
        (DiagramLanguages.Mermaid, "sequence") => "Use Mermaid `sequenceDiagram` syntax, not `flowchart`.",
        (DiagramLanguages.Mermaid, "flow") => "Use Mermaid `flowchart TD` syntax, not `sequenceDiagram`.",
        (DiagramLanguages.Mermaid, "process") => "Use Mermaid `flowchart TD` syntax to represent a process, not `sequenceDiagram`.",
        _ => language == DiagramLanguages.Enzo ? "Use valid Enzo syntax." : "Use valid Mermaid syntax."
    };

    private static string SourceOnlyInstruction(string language, string expectedKind) => language == DiagramLanguages.Enzo && expectedKind == "sequence"
        ? "Before output, silently verify required participants, concepts, interactions, and counts; return source only."
        : "Return the complete diagram source only. Do not use Markdown fences or explanations.";

    internal static IEnumerable<string> RepairProblems(GenerationAttempt attempt)
    {
        if (!attempt.SyntaxValid)
        {
            yield return $"Syntax/parser validation failed{Suffix(attempt.ValidationError)}.";
            yield break;
        }

        if (!attempt.RenderValid)
        {
            yield return $"Render validation failed{Suffix(attempt.ValidationError)}.";
            yield break;
        }

        var diagnostics = attempt.SemanticDiagnostics;
        if (diagnostics is null)
        {
            yield break;
        }

        if (!attempt.KindValid)
        {
            yield return $"Expected diagram kind: {diagnostics.ExpectedKind}. Actual: {diagnostics.ActualKind ?? "unknown"}.";
        }

        foreach (var concept in diagnostics.MissingConcepts)
        {
            yield return $"Missing required concept: {concept}.";
        }

        foreach (var participant in diagnostics.MissingParticipants ?? [])
        {
            yield return $"Missing required participant: {participant}.";
        }

        foreach (var interaction in diagnostics.MissingInteractions ?? [])
        {
            yield return $"Missing required interaction: {interaction.From} -> {interaction.To}.";
        }

        foreach (var problem in MinimumProblems(diagnostics))
        {
            yield return problem;
        }
    }

    private static IEnumerable<string> MinimumProblems(SemanticValidationDiagnostics diagnostics) =>
        Minimums(diagnostics).Where(minimum => Below(minimum.Expected, minimum.Actual))
            .Select(minimum => $"Expected at least {minimum.Expected} {minimum.Label}. Found {Found(minimum.Actual)}.");

    private static IEnumerable<(int? Expected, int? Actual, string Label)> Minimums(SemanticValidationDiagnostics diagnostics) =>
    [
        (diagnostics.ExpectedMinimumNodes, diagnostics.ActualNodes, "nodes"),
        (diagnostics.ExpectedMinimumEdges, diagnostics.ActualEdges, "edges"),
        (diagnostics.ExpectedMinimumDecisions, diagnostics.ActualDecisions, "decisions"),
        (diagnostics.ExpectedMinimumParticipants, diagnostics.ActualParticipants, "participants"),
        (diagnostics.ExpectedMinimumInteractions, diagnostics.ActualInteractions, "interactions")
    ];

    private static IEnumerable<(int? Value, string Label)> Minimums(GenerationContract contract) =>
    [
        (contract.MinimumNodeCount, "nodes"),
        (contract.MinimumEdgeCount, "edges"),
        (contract.MinimumDecisionCount, "decisions"),
        (contract.MinimumParticipantCount, "participants"),
        (contract.MinimumInteractionCount, "interactions")
    ];

    private static bool Below(int? expected, int? actual) => expected is not null && (actual is null || actual < expected);
    private static string Found(int? actual) => actual?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unknown";
    private static string Suffix(string? detail) => string.IsNullOrWhiteSpace(detail) ? string.Empty : $": {detail.Trim()}";
}
