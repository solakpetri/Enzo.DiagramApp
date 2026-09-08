using System.Text;
using System.Text.RegularExpressions;

namespace Enzo.Diagrams.LlmBenchmarks;

public static class EnzoRepairFailureCategories
{
    public const string Syntax = "Syntax";
    public const string UndefinedReference = "UndefinedReference";
    public const string InvalidIdentifier = "InvalidIdentifier";
    public const string InvalidLabel = "InvalidLabel";
    public const string InvalidEdge = "InvalidEdge";
    public const string WrongDiagramKind = "WrongDiagramKind";
    public const string MissingStructure = "MissingStructure";
    public const string MissingConcept = "MissingConcept";
    public const string Cycle = "Cycle";
    public const string Other = "Other";
}

public static class EnzoRepairPromptBuilder
{
    public const string IdenticalOutput = "identical-output";
    public const string Oscillation = "repair-oscillation";

    public static string BuildRepairPrompt(IReadOnlyList<GenerationAttempt> attempts, string? escalation = null)
    {
        var failedAttempt = attempts.Last();
        var guidance = Analyze(failedAttempt);
        var builder = new StringBuilder();
        builder.AppendLine(failedAttempt.SyntaxValid && failedAttempt.RenderValid
            ? "The diagram is valid Enzo source but incomplete."
            : "The Enzo diagram is invalid.");

        if (escalation == IdenticalOutput)
        {
            builder.AppendLine();
            builder.AppendLine("Your previous repair did not change the failing source.");
        }
        else if (escalation == Oscillation)
        {
            builder.AppendLine();
            builder.AppendLine("The last two repairs alternate between invalid forms.");
        }

        builder.AppendLine();
        if (!failedAttempt.SyntaxValid || !failedAttempt.RenderValid)
        {
            builder.AppendLine("Problem:");
            builder.AppendLine(guidance.Problem);
            if (guidance.Hint is not null)
            {
                builder.AppendLine();
                builder.AppendLine(guidance.Hint);
            }
        }
        else
        {
            builder.AppendLine("Problems:");
            foreach (var problem in GenerationPromptBuilder.RepairProblems(failedAttempt))
            {
                builder.AppendLine($"- {problem}");
            }

            builder.AppendLine();
            builder.AppendLine(failedAttempt.SemanticDiagnostics?.ExpectedKind == "sequence"
                ? "Declare each added participant before using it in a message."
                : "Keep all edges acyclic and declare each added node before referencing it.");
        }

        if (escalation == IdenticalOutput)
        {
            builder.AppendLine();
            builder.AppendLine("You must modify the source to fix this problem.");
        }
        else if (escalation == Oscillation)
        {
            builder.AppendLine();
            builder.AppendLine("Required condition:");
            builder.AppendLine($"- {guidance.Invariant}");
        }

        builder.AppendLine();
        builder.AppendLine("Preserve all valid declarations, structure, labels, and semantics. Change only what is necessary to satisfy the listed failures.");
        if (failedAttempt.SyntaxValid && failedAttempt.RenderValid
            && (!failedAttempt.StructureValid || failedAttempt.SemanticDiagnostics?.MissingConcepts.Count > 0))
        {
            builder.AppendLine("Add only the missing required concepts or structure.");
        }

        builder.AppendLine("Return the complete corrected Enzo source only.");
        builder.AppendLine();
        builder.AppendLine("Current source:");
        builder.AppendLine(failedAttempt.NormalizedSource);
        return builder.ToString().TrimEnd();
    }

    public static string FailureCategoryFor(GenerationAttempt attempt) => Analyze(attempt).Category;

    private static RepairGuidance Analyze(GenerationAttempt attempt)
    {
        if (attempt.SyntaxValid && attempt.RenderValid)
        {
            if (!attempt.KindValid)
            {
                return new(EnzoRepairFailureCategories.WrongDiagramKind, "The diagram kind is wrong.", null, "use the requested Enzo diagram kind");
            }

            if (attempt.SemanticDiagnostics?.MissingConcepts.Count > 0)
            {
                return new(EnzoRepairFailureCategories.MissingConcept, "Required concepts are missing.", null, "include every listed required concept");
            }

            return !attempt.StructureValid
                ? new(EnzoRepairFailureCategories.MissingStructure, "Required structure is missing.", null, "meet every listed structural minimum")
                : new(EnzoRepairFailureCategories.Other, "The diagram is not equivalent.", null, "resolve every listed failure");
        }

        var source = attempt.NormalizedSource;
        var error = attempt.ValidationError ?? string.Empty;
        var expectedKind = attempt.SemanticDiagnostics?.ExpectedKind;
        var match = Regex.Match(source, @"(?m)^[ \t]*(?<line>[A-Za-z_][A-Za-z0-9_]*[ \t]*->\|[^\r\n]+\|[ \t]*[A-Za-z_][A-Za-z0-9_]*[^\r\n]*)$");
        if (match.Success && expectedKind != "sequence")
        {
            var line = match.Groups["line"].Value.Trim();
            return new(
                EnzoRepairFailureCategories.InvalidEdge,
                $"Mermaid-style edge label syntax is invalid in Enzo:\n{line}",
                "Rewrite every Mermaid-style branch as:\nA -> B : label\nQuote multiword labels:\nA -> B : \"multiword label\"",
                "every edge label uses `A -> B : label`, never `A ->|label| B`");
        }

        if (Regex.IsMatch(source, @"(?m)^[ \t]*(?:alt|else|end)(?:[ \t]|$)") && Regex.IsMatch(source, @"(?m)^[ \t]*sequence[ \t]"))
        {
            return new(
                EnzoRepairFailureCategories.Syntax,
                "Enzo sequence diagrams do not support `alt`, `else`, or `end` blocks.",
                "Remove the control-block lines and express each interaction directly as:\nA -> B: message\nA --> B: return message",
                "the sequence contains only participant declarations and direct message lines, with no `alt`, `else`, or `end` blocks");
        }

        match = Regex.Match(source, @"(?m)^[ \t]*(?<line>[A-Za-z_][A-Za-z0-9_]*[ \t]*->[ \t]*[A-Za-z_][A-Za-z0-9_]*[ \t]*:[ \t]*[A-Za-z_][A-Za-z0-9_]*(?:[ \t]+[A-Za-z_][A-Za-z0-9_]*)+)[ \t]*$");
        if (match.Success && expectedKind != "sequence")
        {
            return new(
                EnzoRepairFailureCategories.InvalidLabel,
                $"Multiword edge label is not quoted:\n{match.Groups["line"].Value.Trim()}",
                "Enzo multiword edge labels use:\nA -> B : \"multiword label\"",
                "every multiword edge label is quoted");
        }

        match = Regex.Match(source, @"(?m)^[ \t]*(?:flow|sequence|start|task|decision|end|actor|participant|gateway)[ \t]+(?<id>flow|sequence|start|task|decision|end|actor|participant)(?:[ \t]|$)");
        if (match.Success)
        {
            var identifier = match.Groups["id"].Value;
            return new(
                EnzoRepairFailureCategories.InvalidIdentifier,
                $"`{identifier}` cannot be used as an identifier.",
                "Rename that identifier and update all references.",
                $"`{identifier}` is no longer used as an identifier");
        }

        match = Regex.Match(error, @"Unknown (?:node|participant|BPMN element) '([^']+)'");
        if (match.Success)
        {
            var identifier = match.Groups[1].Value;
            return new(
                EnzoRepairFailureCategories.UndefinedReference,
                $"Undefined reference: `{identifier}`.",
                "Correct the mistaken reference to an existing declaration, or declare it if it is required. Prefer the smallest semantic change.",
                $"`{identifier}` is declared or no longer referenced");
        }

        match = Regex.Match(error, @"contains a cycle involving (?:node|element) '([^']+)'", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var identifier = match.Groups[1].Value;
            return new(
                EnzoRepairFailureCategories.Cycle,
                $"Cycle involving `{identifier}`.",
                $"Enzo {KindSyntax(expectedKind)} diagrams must be acyclic. Remove or unfold the backward edge; do not redirect it to another earlier node. Preserve the loop's business concept as a forward task or terminal outcome.",
                "the flow or bpmn graph is acyclic and neither prior back-edge is restored");
        }

        match = Regex.Match(source, "(?m)^[ \\t]*(?<line>[A-Za-z_][A-Za-z0-9_]*[ \\t]+\"[^\"\\r\\n]+\")[ \\t]*$");
        if (match.Success)
        {
            return new(
                EnzoRepairFailureCategories.Syntax,
                $"Missing declaration kind on:\n{match.Groups["line"].Value.Trim()}",
                "Add the applicable declaration kind, for example:\ntask A \"Label\"",
                "every declaration starts with its Enzo declaration kind");
        }

        if (error.Contains("identifier", StringComparison.OrdinalIgnoreCase))
        {
            return new(EnzoRepairFailureCategories.InvalidIdentifier, Concise(error), "Use one declared identifier made from letters, numbers, or underscores, and update all references.", "every identifier is valid and declared before use");
        }

        if (error.Contains("label", StringComparison.OrdinalIgnoreCase) || error.Contains("string literal", StringComparison.OrdinalIgnoreCase))
        {
            return new(EnzoRepairFailureCategories.InvalidLabel, Concise(error), "Quote node labels and quote edge labels that contain multiple words.", "every label follows Enzo quoting rules");
        }

        if (error.Contains("arrow", StringComparison.OrdinalIgnoreCase) || error.Contains("'->'", StringComparison.Ordinal) || error.Contains("target", StringComparison.OrdinalIgnoreCase))
        {
            var hint = expectedKind == "sequence"
                ? "Use `A -> B: message` or `A --> B: return message`."
                : "Use `A -> B` or `A -> B : label`.";
            return new(EnzoRepairFailureCategories.InvalidEdge, Concise(error), hint, "every edge uses the syntax for the current Enzo diagram kind");
        }

        return new(EnzoRepairFailureCategories.Syntax, Concise(error), null, "the exact parser failure is resolved");
    }

    private static string Concise(string error)
    {
        var parts = error.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.TrimEnd('.'))
            .Distinct(StringComparer.Ordinal)
            .Take(2);
        var text = string.Join("; ", parts);
        return string.IsNullOrWhiteSpace(text) ? "Invalid Enzo syntax." : $"{text}.";
    }

    private static string KindSyntax(string? expectedKind) => expectedKind == "process" ? "bpmn" : expectedKind ?? "flow or bpmn";

    private sealed record RepairGuidance(string Category, string Problem, string? Hint, string Invariant);
}
