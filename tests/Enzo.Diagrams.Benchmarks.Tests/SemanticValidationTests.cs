using Enzo.Diagrams.LlmBenchmarks;

namespace Enzo.Diagrams.Benchmarks.Tests;

public sealed class SemanticValidationTests
{
    [Fact]
    public void Validate_CommittedFixtures_PassSemanticEquivalenceExpectations()
    {
        var scenarios = ScenarioLoader.Load(Path.Combine(RepositoryRoot(), "benchmarks", "scenarios"));

        foreach (var scenario in scenarios)
        {
            var enzo = SemanticDiagramValidator.Validate(scenario, DiagramLanguages.Enzo, scenario.Enzo, syntaxValid: true, renderValid: true);
            var mermaid = SemanticDiagramValidator.Validate(scenario, DiagramLanguages.Mermaid, scenario.Mermaid, syntaxValid: true, renderValid: true);
            Assert.True(enzo.EquivalentValid, $"{scenario.Id} Enzo: {string.Join("; ", enzo.FailureReasons)}");
            Assert.True(mermaid.EquivalentValid, $"{scenario.Id} Mermaid: {string.Join("; ", mermaid.FailureReasons)}");
        }
    }

    [Fact]
    public void Validate_MermaidFlowchartForSequence_FailsKindAndEquivalence()
    {
        var result = SemanticDiagramValidator.Validate(SequenceScenario(), DiagramLanguages.Mermaid, """
            flowchart TD
            A[User]
            B[API]
            A --> B
            """, syntaxValid: true, renderValid: true);

        Assert.False(result.KindValid);
        Assert.False(result.EquivalentValid);
        Assert.Contains(result.FailureReasons, reason => reason.Contains("Expected sequence", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_EnzoFlowForSequence_FailsKindAndEquivalence()
    {
        var result = SemanticDiagramValidator.Validate(SequenceScenario(), DiagramLanguages.Enzo, """
            flow WrongKind
            start A "User"
            task B "API"
            A -> B
            """, syntaxValid: true, renderValid: true);

        Assert.False(result.KindValid);
        Assert.False(result.EquivalentValid);
    }

    [Fact]
    public void Validate_MissingRequiredConcept_FailsSemanticEquivalence()
    {
        var result = SemanticDiagramValidator.Validate(FlowScenario(), DiagramLanguages.Mermaid, """
            flowchart TD
            A[Receive order]
            B[Validate order]
            A --> B
            """, syntaxValid: true, renderValid: true);

        Assert.False(result.SemanticValid);
        Assert.False(result.EquivalentValid);
        Assert.Contains("payment", result.Diagnostics.MissingConcepts);
    }

    [Fact]
    public void Validate_UnderSpecifiedStructure_FailsStructureEquivalence()
    {
        var result = SemanticDiagramValidator.Validate(FlowScenario(), DiagramLanguages.Mermaid, """
            flowchart TD
            A[Receive order]
            B[Validate order]
            A --> B
            """, syntaxValid: true, renderValid: true);

        Assert.False(result.StructureValid);
        Assert.False(result.EquivalentValid);
        Assert.Contains(result.FailureReasons, reason => reason.Contains("Expected at least 4 nodes", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_EquivalentWordingFormatting_MatchesRequiredConcept()
    {
        var result = SemanticDiagramValidator.Validate(SimpleFlowScenario("validate order"), DiagramLanguages.Mermaid, """
            flowchart TD
            ValidateOrder[Validate-Order]
            Done[Done]
            ValidateOrder --> Done
            """, syntaxValid: true, renderValid: true);

        Assert.True(result.SemanticValid);
        Assert.Empty(result.Diagnostics.MissingConcepts);
    }

    [Fact]
    public void Validate_AlternativeIdentifiers_DoesNotRequireReferenceIds()
    {
        var result = SemanticDiagramValidator.Validate(SimpleFlowScenario("validate order"), DiagramLanguages.Mermaid, """
            flowchart TD
            X[Validate order]
            Y[Done]
            X --> Y
            """, syntaxValid: true, renderValid: true);

        Assert.True(result.EquivalentValid);
    }

    [Fact]
    public void Validate_FullyValidProcessFlowchartRepresentation_PassesEquivalence()
    {
        var scenario = new DiagramScenario(
            "process-test",
            "process",
            "simple",
            "Submit, review, approve, complete.",
            string.Empty,
            string.Empty,
            new DiagramExpectations("process", ["submit request", "review", "approval", "complete"], MinimumNodeCount: 4, MinimumEdgeCount: 3, MinimumDecisionCount: 1));

        var result = SemanticDiagramValidator.Validate(scenario, DiagramLanguages.Mermaid, """
            flowchart TD
            A[Submit request]
            B[Review]
            C{Approval?}
            D[Complete]
            A --> B
            B --> C
            C -- yes --> D
            """, syntaxValid: true, renderValid: true);

        Assert.True(result.KindValid);
        Assert.True(result.StructureValid);
        Assert.True(result.SemanticValid);
        Assert.True(result.EquivalentValid);
    }

    private static DiagramScenario SequenceScenario() => new(
        "sequence-test",
        "sequence",
        "simple",
        "Show a user calling an API.",
        string.Empty,
        string.Empty,
        new DiagramExpectations("sequence", ["user", "api"], MinimumParticipantCount: 2, MinimumInteractionCount: 1));

    private static DiagramScenario FlowScenario() => new(
        "flow-test",
        "flow",
        "medium",
        "Receive an order, validate it, process payment, and ship it.",
        string.Empty,
        string.Empty,
        new DiagramExpectations("flow", ["receive order", "validate order", "payment", "ship order"], MinimumNodeCount: 4, MinimumEdgeCount: 3));

    private static DiagramScenario SimpleFlowScenario(string concept) => new(
        "flow-simple",
        "flow",
        "simple",
        "Validate an order.",
        string.Empty,
        string.Empty,
        new DiagramExpectations("flow", [concept], MinimumNodeCount: 2, MinimumEdgeCount: 1));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Enzo.Diagrams.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
