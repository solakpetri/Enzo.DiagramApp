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
    int? MinimumInteractionCount)
{
    public static GenerationContract From(DiagramScenario scenario) => new(
        scenario.Expectations.ExpectedKind,
        scenario.Expectations.RequiredConcepts,
        scenario.Expectations.MinimumNodeCount,
        scenario.Expectations.MinimumEdgeCount,
        scenario.Expectations.MinimumDecisionCount,
        scenario.Expectations.MinimumParticipantCount,
        scenario.Expectations.MinimumInteractionCount);
}

public static class GenerationPromptBuilder
{
    public const string SharedSemanticTaskTemplate = "Create a {kind} diagram for this request, preserve the original request text, then satisfy only the listed requirements.";
    public const string EnzoLanguageSpecificAdditions = "Use the requested Enzo diagram keyword for the benchmark kind.";
    public const string MermaidLanguageSpecificAdditions = "Use the requested Mermaid diagram declaration for the benchmark kind.";

    public static string BuildUserPrompt(DiagramScenario scenario, string language)
    {
        var contract = GenerationContract.From(scenario);
        if (language == DiagramLanguages.Enzo && (contract.ExpectedKind == "flow" || contract.ExpectedKind == "process"))
        {
            return BuildEnzoFlowProcessUserPrompt(scenario, contract);
        }

        var builder = new StringBuilder();
        builder.AppendLine($"Create a {contract.ExpectedKind} diagram for this request:");
        builder.AppendLine();
        builder.AppendLine(scenario.Prompt);
        builder.AppendLine();
        builder.AppendLine("Requirements:");
        builder.AppendLine($"- Diagram kind: {contract.ExpectedKind}");
        if (contract.RequiredConcepts.Count > 0)
        {
            builder.AppendLine("- Include these concepts:");
            foreach (var concept in contract.RequiredConcepts)
            {
                builder.AppendLine($"  - {concept}");
            }
        }

        foreach (var (value, label) in Minimums(contract))
        {
            if (value is not null)
            {
                builder.AppendLine($"- At least {value} {label}");
            }
        }
        builder.AppendLine();
        builder.AppendLine("Return the complete diagram source only. Do not use Markdown fences or explanations.");
        builder.AppendLine(LanguageKindInstruction(language, contract.ExpectedKind));
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
        (DiagramLanguages.Enzo, "flow") => EnzoFlowGuidance,
        (DiagramLanguages.Enzo, "process") => EnzoProcessGuidance,
        (DiagramLanguages.Mermaid, "sequence") => "Use Mermaid `sequenceDiagram` syntax, not `flowchart`.",
        (DiagramLanguages.Mermaid, "flow") => "Use Mermaid `flowchart TD` syntax, not `sequenceDiagram`.",
        (DiagramLanguages.Mermaid, "process") => "Use Mermaid `flowchart TD` syntax to represent a process, not `sequenceDiagram`.",
        _ => language == DiagramLanguages.Enzo ? "Use valid Enzo syntax." : "Use valid Mermaid syntax."
    };

    private const string FlowProcessReliabilityGuidance = "Acyclic: no back-edges; retry/rework forward as Rework -> Reinspect. Labels `A -> B : yes`, never `A ->|yes| B`; quote multiword, prefer short labels. Short safe identifiers; declare first; decisions only for branches; silent check: kind, refs, labels, counts, concepts.";
    private const string EnzoFlowGuidance = "Use Enzo `flow`, not sequence/bpmn. " + FlowProcessReliabilityGuidance;
    private const string EnzoProcessGuidance = "Use Enzo `bpmn`, not flow/sequence. " + FlowProcessReliabilityGuidance;

    private static string BuildEnzoFlowProcessUserPrompt(DiagramScenario scenario, GenerationContract contract)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Request:");
        builder.AppendLine(scenario.Prompt);
        builder.AppendLine();
        builder.Append($"Need: {contract.ExpectedKind}");
        if (contract.RequiredConcepts.Count > 0)
        {
            builder.Append($"; concepts {string.Join(", ", contract.RequiredConcepts)}");
        }

        var minimums = Minimums(contract)
            .Where(minimum => minimum.Value is not null)
            .Select(minimum => $">={minimum.Value} {minimum.Label}")
            .ToArray();
        if (minimums.Length > 0)
        {
            builder.Append($"; {string.Join(", ", minimums)}");
        }

        builder.AppendLine(".");
        builder.AppendLine(LanguageKindInstruction(DiagramLanguages.Enzo, contract.ExpectedKind));
        return builder.ToString().TrimEnd();
    }

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
