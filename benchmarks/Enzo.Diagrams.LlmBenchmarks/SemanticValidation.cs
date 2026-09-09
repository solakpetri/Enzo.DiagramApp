using Enzo.Diagrams.Benchmarks;

namespace Enzo.Diagrams.LlmBenchmarks;

public sealed record DiagramFacts(
    string Kind,
    IReadOnlyList<string> Labels,
    int NodeCount,
    int EdgeCount,
    int DecisionCount,
    int ParticipantCount,
    int InteractionCount)
{
    public IReadOnlyList<string> Participants { get; init; } = [];
    public IReadOnlyList<DiagramInteraction> Interactions { get; init; } = [];
}

public sealed record DiagramInteraction(string From, string To, string? Label);

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
    int? ActualInteractions,
    IReadOnlyList<string>? RequiredParticipants = null,
    IReadOnlyList<string>? MatchedParticipants = null,
    IReadOnlyList<string>? MissingParticipants = null,
    IReadOnlyList<RequiredInteraction>? RequiredInteractions = null,
    IReadOnlyList<RequiredInteraction>? MatchedInteractions = null,
    IReadOnlyList<RequiredInteraction>? MissingInteractions = null);

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
        var (matched, missing) = MatchRequiredLabels(expected.RequiredConcepts, labels, expected);
        foreach (var concept in missing)
        {
            failures.Add($"Missing required concept: {concept}.");
        }

        var participants = facts?.Participants.Select(ConceptNormalizer.Normalize).Where(participant => participant.Length > 0).ToList() ?? [];
        var requiredParticipants = expected.RequiredParticipants ?? [];
        var (matchedParticipants, missingParticipants) = MatchRequiredLabels(requiredParticipants, participants, expected);
        foreach (var participant in missingParticipants)
        {
            failures.Add($"Missing required participant: {participant}.");
        }

        var requiredInteractions = expected.RequiredInteractions ?? [];
        var matchedInteractions = new List<RequiredInteraction>();
        var missingInteractions = new List<RequiredInteraction>();
        foreach (var interaction in requiredInteractions)
        {
            if (facts?.Interactions.Any(actual => MatchesInteraction(actual, interaction, expected)) == true)
            {
                matchedInteractions.Add(interaction);
                continue;
            }

            missingInteractions.Add(interaction);
            failures.Add($"Missing required interaction: {interaction.From} -> {interaction.To}.");
        }

        var structureValid = true;
        CheckMinimum(failures, ref structureValid, "nodes", expected.MinimumNodeCount, facts?.NodeCount);
        CheckMinimum(failures, ref structureValid, "edges", expected.MinimumEdgeCount, facts?.EdgeCount);
        CheckMinimum(failures, ref structureValid, "decisions", expected.MinimumDecisionCount, facts?.DecisionCount);
        CheckMinimum(failures, ref structureValid, "participants", expected.MinimumParticipantCount, facts?.ParticipantCount);
        CheckMinimum(failures, ref structureValid, "interactions", expected.MinimumInteractionCount, facts?.InteractionCount);

        var semanticValid = missing.Count == 0 && missingParticipants.Count == 0 && missingInteractions.Count == 0;
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
                facts?.InteractionCount,
                requiredParticipants,
                matchedParticipants,
                missingParticipants,
                requiredInteractions,
                matchedInteractions,
                missingInteractions));
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

    private static (List<string> Matched, List<string> Missing) MatchRequiredLabels(IReadOnlyList<string> required, IReadOnlyList<string> actual, DiagramExpectations expected)
    {
        var matched = new List<string>();
        var missing = new List<string>();
        foreach (var item in required)
        {
            if (AliasesIncludingSelf(expected, item).Any(alias => actual.Any(label => LabelMatches(label, alias))))
            {
                matched.Add(item);
                continue;
            }

            missing.Add(item);
        }

        return (matched, missing);
    }

    private static bool MatchesInteraction(DiagramInteraction actual, RequiredInteraction expected, DiagramExpectations expectations) =>
        AliasesIncludingSelf(expectations, expected.From).Any(alias => LabelMatches(ConceptNormalizer.Normalize(actual.From), alias)) &&
        AliasesIncludingSelf(expectations, expected.To).Any(alias => LabelMatches(ConceptNormalizer.Normalize(actual.To), alias)) &&
        (expected.Label is null || LabelMatches(ConceptNormalizer.Normalize(actual.Label ?? string.Empty), ConceptNormalizer.Normalize(expected.Label)));

    private static IReadOnlyList<string> AliasesIncludingSelf(DiagramExpectations expected, string value) =>
        new[] { value }.Concat(Aliases(expected, value)).Select(ConceptNormalizer.Normalize).Where(alias => alias.Length > 0).ToList();

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

    private static bool LabelMatches(string label, string concept) => label == concept || ContainsTokenSequence(label, concept);

    private static bool ContainsTokenSequence(string label, string concept) =>
        $" {label} ".Contains($" {concept} ", StringComparison.Ordinal);
}
