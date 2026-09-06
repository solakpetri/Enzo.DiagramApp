using Enzo.Diagrams.Benchmarks;

namespace Enzo.Diagrams.LlmBenchmarks;

public sealed record DiagramFacts(
    string Kind,
    IReadOnlyList<string> Labels,
    int NodeCount,
    int EdgeCount,
    int DecisionCount,
    int ParticipantCount,
    int InteractionCount);

public sealed record SemanticValidationDiagnostics(
    string ExpectedKind,
    string? ActualKind,
    IReadOnlyList<string> RequiredConcepts,
    IReadOnlyList<string> MatchedConcepts,
    IReadOnlyList<string> MissingConcepts,
    int? ExpectedMinimumNodes,
    int? ActualNodes,
    int? ExpectedMinimumEdges,
    int? ActualEdges,
    int? ExpectedMinimumDecisions,
    int? ActualDecisions,
    int? ExpectedMinimumParticipants,
    int? ActualParticipants,
    int? ExpectedMinimumInteractions,
    int? ActualInteractions);

public sealed record SemanticValidationResult(
    bool KindValid,
    bool StructureValid,
    bool SemanticValid,
    bool EquivalentValid,
    IReadOnlyList<string> FailureReasons,
    SemanticValidationDiagnostics Diagnostics);

public static class SemanticDiagramValidator
{
    public static SemanticValidationResult Validate(DiagramScenario scenario, string language, string source, bool syntaxValid, bool renderValid)
    {
        var failures = new List<string>();
        if (!syntaxValid)
        {
            failures.Add("Syntax/parser validation failed.");
        }

        if (!renderValid)
        {
            failures.Add("Render validation failed.");
        }

        var expected = scenario.Expectations;
        var facts = TryExtractFacts(language, source, expected.ExpectedKind, failures);
        var actualKind = facts?.Kind;
        var kindValid = string.Equals(expected.ExpectedKind, actualKind, StringComparison.Ordinal);
        if (!kindValid)
        {
            failures.Add($"Expected {expected.ExpectedKind} diagram but generated {actualKind ?? "unknown"}.");
        }

        var labels = facts?.Labels.Select(ConceptNormalizer.Normalize).Where(label => label.Length > 0).ToList() ?? [];
        var matched = new List<string>();
        var missing = new List<string>();
        foreach (var concept in expected.RequiredConcepts)
        {
            var aliases = new[] { concept }.Concat(Aliases(expected, concept)).Select(ConceptNormalizer.Normalize).Where(alias => alias.Length > 0).ToList();
            if (aliases.Any(alias => labels.Any(label => label == alias || ContainsTokenSequence(label, alias))))
            {
                matched.Add(concept);
                continue;
            }

            missing.Add(concept);
            failures.Add($"Missing required concept: {concept}.");
        }

        var structureValid = true;
        CheckMinimum(failures, ref structureValid, "nodes", expected.MinimumNodeCount, facts?.NodeCount);
        CheckMinimum(failures, ref structureValid, "edges", expected.MinimumEdgeCount, facts?.EdgeCount);
        CheckMinimum(failures, ref structureValid, "decisions", expected.MinimumDecisionCount, facts?.DecisionCount);
        CheckMinimum(failures, ref structureValid, "participants", expected.MinimumParticipantCount, facts?.ParticipantCount);
        CheckMinimum(failures, ref structureValid, "interactions", expected.MinimumInteractionCount, facts?.InteractionCount);

        var semanticValid = missing.Count == 0;
        var equivalentValid = syntaxValid && renderValid && kindValid && structureValid && semanticValid;
        return new SemanticValidationResult(
            kindValid,
            structureValid,
            semanticValid,
            equivalentValid,
            failures,
            new SemanticValidationDiagnostics(
                expected.ExpectedKind,
                actualKind,
                expected.RequiredConcepts,
                matched,
                missing,
                expected.MinimumNodeCount,
                facts?.NodeCount,
                expected.MinimumEdgeCount,
                facts?.EdgeCount,
                expected.MinimumDecisionCount,
                facts?.DecisionCount,
                expected.MinimumParticipantCount,
                facts?.ParticipantCount,
                expected.MinimumInteractionCount,
                facts?.InteractionCount));
    }

    private static DiagramFacts? TryExtractFacts(string language, string source, string expectedKind, List<string> failures)
    {
        try
        {
            return language switch
            {
                DiagramLanguages.Enzo => EnzoDiagramFacts.Extract(source),
                DiagramLanguages.Mermaid => MermaidDiagramFacts.Extract(source, expectedKind),
                _ => throw new ArgumentOutOfRangeException(nameof(language), language, "Unsupported benchmark language.")
            };
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or BenchmarkValidationException)
        {
            failures.Add($"Could not extract semantic facts: {ex.Message}");
            return null;
        }
    }

    private static IEnumerable<string> Aliases(DiagramExpectations expected, string concept) =>
        expected.ConceptAliases is not null && expected.ConceptAliases.TryGetValue(concept, out var aliases) ? aliases : [];

    private static void CheckMinimum(List<string> failures, ref bool valid, string label, int? expected, int? actual)
    {
        if (expected is null)
        {
            return;
        }

        if (actual is null || actual.Value < expected.Value)
        {
            valid = false;
            failures.Add($"Expected at least {expected.Value} {label}; found {actual?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unknown"}.");
        }
    }

    private static bool ContainsTokenSequence(string label, string concept) =>
        $" {label} ".Contains($" {concept} ", StringComparison.Ordinal);
}
