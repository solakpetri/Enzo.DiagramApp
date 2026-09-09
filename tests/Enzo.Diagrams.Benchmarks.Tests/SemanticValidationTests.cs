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
    public void Load_SequenceGenerationSuite_HasThirtyBalancedScenarios()
    {
        var scenarios = ScenarioLoader.Load(Path.Combine(RepositoryRoot(), "benchmarks", "scenarios", "sequence-generation"));

        Assert.Equal(30, scenarios.Count);
        Assert.Equal(10, scenarios.Count(scenario => scenario.Complexity == "simple"));
        Assert.Equal(10, scenarios.Count(scenario => scenario.Complexity == "medium"));
        Assert.Equal(10, scenarios.Count(scenario => scenario.Complexity == "complex"));
        Assert.All(scenarios, scenario => Assert.Equal("sequence", scenario.Expectations.ExpectedKind));
    }

    [Fact]
    public void Validate_SequenceGenerationFixtures_PassSemanticEquivalenceExpectations()
    {
        var scenarios = ScenarioLoader.Load(Path.Combine(RepositoryRoot(), "benchmarks", "scenarios", "sequence-generation"));

        foreach (var scenario in scenarios)
        {
            var enzo = SemanticDiagramValidator.Validate(scenario, DiagramLanguages.Enzo, scenario.Enzo, syntaxValid: true, renderValid: true);
            var mermaid = SemanticDiagramValidator.Validate(scenario, DiagramLanguages.Mermaid, scenario.Mermaid, syntaxValid: true, renderValid: true);
            Assert.True(enzo.EquivalentValid, $"{scenario.Id} Enzo: {string.Join("; ", enzo.FailureReasons)}");
            Assert.True(mermaid.EquivalentValid, $"{scenario.Id} Mermaid: {string.Join("; ", mermaid.FailureReasons)}");
        }
    }

    [Fact]
    public void Load_DefaultScenarios_ExcludesSequenceGenerationSuite()
    {
        var scenarios = ScenarioLoader.Load(Path.Combine(RepositoryRoot(), "benchmarks", "scenarios"));

        Assert.Equal(10, scenarios.Count(scenario => scenario.Category == "sequence"));
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
    public void Validate_SequenceSyntaxForExpectedSequence_PassesKind()
    {
        var enzo = SemanticDiagramValidator.Validate(SequenceScenario(), DiagramLanguages.Enzo, """
            sequence Login
            actor User
            participant Api
            User -> Api: Request
            """, syntaxValid: true, renderValid: true);
        var mermaid = SemanticDiagramValidator.Validate(SequenceScenario(), DiagramLanguages.Mermaid, """
            sequenceDiagram
            actor User
            participant Api
            User->>Api: Request
            """, syntaxValid: true, renderValid: true);

        Assert.True(enzo.KindValid);
        Assert.True(mermaid.KindValid);
        Assert.True(enzo.EquivalentValid);
        Assert.True(mermaid.EquivalentValid);
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
    public void Validate_ParticipantFormattingVariations_MatchRequiredParticipant()
    {
        var scenario = new DiagramScenario(
            "sequence-participant-formatting",
            "sequence",
            "simple",
            "Call an order API.",
            string.Empty,
            string.Empty,
            new DiagramExpectations("sequence", [], MinimumParticipantCount: 2, MinimumInteractionCount: 1, RequiredParticipants: ["Order API", "Client"]));

        var result = SemanticDiagramValidator.Validate(scenario, DiagramLanguages.Mermaid, """
            sequenceDiagram
            actor Client
            participant OrderApi
            Client->>OrderApi: Request
            """, syntaxValid: true, renderValid: true);

        Assert.True(result.SemanticValid);
        Assert.Empty(result.Diagnostics.MissingParticipants ?? []);
    }

    [Fact]
    public void Validate_MissingParticipant_FailsEquivalence()
    {
        var result = SemanticDiagramValidator.Validate(InteractionScenario(), DiagramLanguages.Mermaid, """
            sequenceDiagram
            actor Customer
            participant Web
            Customer->>Web: Submit order
            """, syntaxValid: true, renderValid: true);

        Assert.False(result.SemanticValid);
        Assert.False(result.EquivalentValid);
        Assert.Contains("Order API", result.Diagnostics.MissingParticipants ?? []);
    }

    [Fact]
    public void Validate_InsufficientInteractionCount_FailsEquivalence()
    {
        var result = SemanticDiagramValidator.Validate(InteractionScenario(), DiagramLanguages.Mermaid, """
            sequenceDiagram
            actor Customer
            participant Web
            participant OrderApi
            Customer->>Web: Submit order
            """, syntaxValid: true, renderValid: true);

        Assert.False(result.StructureValid);
        Assert.False(result.EquivalentValid);
        Assert.Contains(result.FailureReasons, reason => reason.Contains("Expected at least 2 interactions", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_MissingRequiredInteraction_FailsEquivalence()
    {
        var result = SemanticDiagramValidator.Validate(InteractionScenario(), DiagramLanguages.Mermaid, """
            sequenceDiagram
            actor Customer
            participant Web
            participant OrderApi
            Customer->>Web: Submit order
            Web-->>Customer: Accepted
            """, syntaxValid: true, renderValid: true);

        Assert.False(result.SemanticValid);
        Assert.False(result.EquivalentValid);
        Assert.Single(result.Diagnostics.MissingInteractions ?? []);
    }

    [Fact]
    public void Validate_RequiredInteraction_AllowsAlternativeEndpointFormatting()
    {
        var result = SemanticDiagramValidator.Validate(InteractionScenario(), DiagramLanguages.Mermaid, """
            sequenceDiagram
            actor Customer
            participant Web
            participant order-api
            Customer->>Web: Submit order
            Web->>order-api: Create order
            """, syntaxValid: true, renderValid: true);

        Assert.True(result.EquivalentValid);
        Assert.Empty(result.Diagnostics.MissingInteractions ?? []);
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

    private static DiagramScenario InteractionScenario() => new(
        "sequence-interaction-test",
        "sequence",
        "simple",
        "Show checkout order creation.",
        string.Empty,
        string.Empty,
        new DiagramExpectations(
            "sequence",
            ["customer", "web", "order"],
            MinimumParticipantCount: 3,
            MinimumInteractionCount: 2,
            ConceptAliases: new Dictionary<string, IReadOnlyList<string>> { ["Order API"] = ["OrderApi", "order-api"] },
            RequiredParticipants: ["Customer", "Web", "Order API"],
            RequiredInteractions: [new RequiredInteraction("Web", "Order API")]));

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
