using Enzo.Diagrams.LlmBenchmarks;

namespace Enzo.Diagrams.Benchmarks.Tests;

public sealed class EnzoRepairPromptTests
{
    [Fact]
    public void MermaidEdgeLeakage_GivesOnlyEnzoEdgeForm()
    {
        var error = string.Join("; ", Enumerable.Repeat("Unexpected character '|'.", 12));
        var attempt = SyntaxFailure("flow Checkout\ndecision A \"Approved?\"\nend B \"Done\"\nA ->|yes| B", error);

        var prompt = EnzoRepairPromptBuilder.BuildRepairPrompt([attempt]);

        Assert.Equal(EnzoRepairFailureCategories.InvalidEdge, EnzoRepairPromptBuilder.FailureCategoryFor(attempt));
        Assert.Contains("A ->|yes| B", prompt);
        Assert.Contains("A -> B : label", prompt);
        Assert.DoesNotContain("Sequence diagram:", prompt);
        Assert.True(prompt.Length < GenerationPromptBuilder.BuildRepairPrompt(DiagramLanguages.Enzo, attempt).Length);
    }

    [Fact]
    public void UndefinedReference_IdentifiesMissingIdentifier()
    {
        var attempt = SyntaxFailure("flow Checkout\nstart A \"Start\"\nA -> PaymentResult", "Unknown node 'PaymentResult' referenced by edge on line 3.");

        var prompt = EnzoRepairPromptBuilder.BuildRepairPrompt([attempt]);

        Assert.Equal(EnzoRepairFailureCategories.UndefinedReference, EnzoRepairPromptBuilder.FailureCategoryFor(attempt));
        Assert.Contains("Undefined reference: `PaymentResult`", prompt);
        Assert.Contains("Prefer the smallest semantic change", prompt);
    }

    [Fact]
    public void ReservedIdentifier_RequiresRenameAndReferenceUpdate()
    {
        var attempt = SyntaxFailure("flow Checkout\ntask decision \"Choose\"", "Expected node identifier.");

        var prompt = EnzoRepairPromptBuilder.BuildRepairPrompt([attempt]);

        Assert.Equal(EnzoRepairFailureCategories.InvalidIdentifier, EnzoRepairPromptBuilder.FailureCategoryFor(attempt));
        Assert.Contains("`decision` cannot be used as an identifier", prompt);
        Assert.Contains("Rename that identifier and update all references", prompt);
    }

    [Fact]
    public void MultiwordBranchLabel_ShowsExactQuotedForm()
    {
        var attempt = SyntaxFailure("bpmn Restart\nend A\nstart B\nA -> B : Restart Process", "Unexpected token 'Process' at end of line.", "process");

        var prompt = EnzoRepairPromptBuilder.BuildRepairPrompt([attempt]);

        Assert.Equal(EnzoRepairFailureCategories.InvalidLabel, EnzoRepairPromptBuilder.FailureCategoryFor(attempt));
        Assert.Contains("A -> B : \"multiword label\"", prompt);
        Assert.Contains("A -> B : Restart Process", prompt);
    }

    [Fact]
    public void UnsupportedSequenceBlock_GivesOnlySequenceSyntax()
    {
        var attempt = SyntaxFailure("sequence Cache\nactor Client\nparticipant Api\nalt cache miss\nClient -> Api: Get\nend", "Expected '->' or '-->' in message declaration.", "sequence");

        var prompt = EnzoRepairPromptBuilder.BuildRepairPrompt([attempt]);

        Assert.Contains("do not support `alt`, `else`, or `end` blocks", prompt);
        Assert.Contains("A -> B: message", prompt);
        Assert.DoesNotContain("bpmn", prompt);
    }

    [Fact]
    public void Cycle_RequiresAcyclicForwardRewrite()
    {
        var attempt = SyntaxFailure("flow Review\nstart A \"Review\"\ntask B \"Revise\"\nA -> B\nB -> A", "Diagram 'Review' contains a cycle involving node 'A'.");

        var prompt = EnzoRepairPromptBuilder.BuildRepairPrompt([attempt]);

        Assert.Equal(EnzoRepairFailureCategories.Cycle, EnzoRepairPromptBuilder.FailureCategoryFor(attempt));
        Assert.Contains("Cycle involving `A`", prompt);
        Assert.Contains("must be acyclic", prompt);
        Assert.Contains("do not redirect it to another earlier node", prompt);
    }

    [Fact]
    public void MissingConcept_PreservesDiagramAndAddsOnlyMissingConcept()
    {
        var attempt = SemanticFailure(Diagnostics("flow", missing: ["Payment Service"]), structureValid: true, semanticValid: false);

        var prompt = EnzoRepairPromptBuilder.BuildRepairPrompt([attempt]);

        Assert.Equal(EnzoRepairFailureCategories.MissingConcept, EnzoRepairPromptBuilder.FailureCategoryFor(attempt));
        Assert.Contains("Missing required concept: Payment Service", prompt);
        Assert.Contains("Preserve all valid declarations, structure, labels, and semantics", prompt);
        Assert.Contains("Add only the missing required concepts or structure", prompt);
        Assert.Contains("Keep all edges acyclic", prompt);
    }

    [Fact]
    public void StructuralShortfall_ReportsActualAndRequiredCount()
    {
        var attempt = SemanticFailure(Diagnostics("sequence", expectedInteractions: 8, actualInteractions: 6), structureValid: false, semanticValid: true);

        var prompt = EnzoRepairPromptBuilder.BuildRepairPrompt([attempt]);

        Assert.Equal(EnzoRepairFailureCategories.MissingStructure, EnzoRepairPromptBuilder.FailureCategoryFor(attempt));
        Assert.Contains("Expected at least 8 interactions. Found 6.", prompt);
        Assert.Contains("Declare each added participant before using it", prompt);
    }

    private static GenerationAttempt SyntaxFailure(string source, string error, string expectedKind = "flow") =>
        new(0, false, 1, 1, 2, source, source, false, false, false, error, 0,
            SemanticDiagnostics: Diagnostics(expectedKind));

    private static GenerationAttempt SemanticFailure(SemanticValidationDiagnostics diagnostics, bool structureValid, bool semanticValid) =>
        new(0, false, 1, 1, 2, "flow Current", "flow Current", false, true, true, null, 0,
            SyntaxValid: true,
            RenderValid: true,
            KindValid: true,
            StructureValid: structureValid,
            SemanticValid: semanticValid,
            EquivalentValid: false,
            SemanticDiagnostics: diagnostics);

    private static SemanticValidationDiagnostics Diagnostics(
        string kind,
        IReadOnlyList<string>? missing = null,
        int? expectedInteractions = null,
        int? actualInteractions = null) =>
        new(kind, kind, missing ?? [], [], missing ?? [], null, null, null, null, null, null, null, null, expectedInteractions, actualInteractions);
}
