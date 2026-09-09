using System.Text.RegularExpressions;
using Enzo.Diagrams.Benchmarks;

namespace Enzo.Diagrams.LlmBenchmarks;

public static partial class SequenceFailureCategories
{
    public const string MissingParticipant = "MissingParticipant";
    public const string UndefinedParticipant = "UndefinedParticipant";
    public const string InvalidMessage = "InvalidMessage";
    public const string InvalidArrow = "InvalidArrow";
    public const string WrongKind = "WrongKind";
    public const string MissingInteraction = "MissingInteraction";
    public const string MissingConcept = "MissingConcept";
    public const string Syntax = "Syntax";
    public const string Other = "Other";
}

public static partial class SequenceFailureClassifier
{
    public static IReadOnlyList<string> Classify(DiagramScenario scenario, string source, ValidationOutcome validation, SemanticValidationResult semantic)
    {
        if (scenario.Expectations.ExpectedKind != "sequence" || semantic.EquivalentValid)
        {
            return [];
        }

        var categories = new List<string>();
        if (!validation.SyntaxValid)
        {
            categories.Add(SequenceFailureCategories.Syntax);
        }

        if (!semantic.KindValid)
        {
            categories.Add(SequenceFailureCategories.WrongKind);
        }

        if (semantic.Diagnostics.MissingParticipants?.Count > 0)
        {
            categories.Add(SequenceFailureCategories.MissingParticipant);
        }

        if (semantic.Diagnostics.MissingInteractions?.Count > 0)
        {
            categories.Add(SequenceFailureCategories.MissingInteraction);
        }

        if (semantic.Diagnostics.MissingConcepts.Count > 0)
        {
            categories.Add(SequenceFailureCategories.MissingConcept);
        }

        var error = validation.Error ?? string.Empty;
        if (error.Contains("Unknown participant", StringComparison.OrdinalIgnoreCase))
        {
            categories.Add(SequenceFailureCategories.UndefinedParticipant);
        }

        if (error.Contains("arrow", StringComparison.OrdinalIgnoreCase) || InvalidArrowRegex().IsMatch(source))
        {
            categories.Add(SequenceFailureCategories.InvalidArrow);
        }

        if (error.Contains("message", StringComparison.OrdinalIgnoreCase))
        {
            categories.Add(SequenceFailureCategories.InvalidMessage);
        }

        return categories.Count == 0 ? [SequenceFailureCategories.Other] : categories.Distinct(StringComparer.Ordinal).ToList();
    }

    [GeneratedRegex("(?m)^\\s*[A-Za-z_][A-Za-z0-9_]*\\s*(?:==>|-.->|--\\|)")]
    private static partial Regex InvalidArrowRegex();
}
