using Enzo.Diagrams.Benchmarks;
using Enzo.Diagrams.LlmBenchmarks;

namespace Enzo.Diagrams.Benchmarks.Tests;

public sealed class EnzoGenerationPromptTests
{
    [Fact]
    public void FlowPrompt_IncludesCycleLabelStructureAndIdentifierGuidance()
    {
        var prompt = GenerationPromptBuilder.BuildUserPrompt(Scenario("flow"), DiagramLanguages.Enzo);

        Assert.Contains("Use Enzo `flow`", prompt);
        Assert.Contains("not sequence/bpmn", prompt);
        Assert.Contains("Acyclic", prompt);
        Assert.Contains("no back-edges", prompt);
        Assert.Contains("retry/rework forward", prompt);
        Assert.Contains("Rework -> Reinspect", prompt);
        Assert.Contains("A -> B : yes", prompt);
        Assert.Contains("never `A ->|yes| B`", prompt);
        Assert.Contains("prefer short labels", prompt);
        Assert.Contains("Short safe identifiers", prompt);
        Assert.Contains("declare first", prompt);
        Assert.Contains("silent check: kind, refs, labels, counts, concepts", prompt);
        Assert.Contains(">=8 nodes, >=9 edges, >=2 decisions", prompt);
        Assert.DoesNotContain("Markdown fences", prompt);
    }

    [Fact]
    public void ProcessPrompt_IncludesBpmnSpecificCycleAndLabelGuidance()
    {
        var prompt = GenerationPromptBuilder.BuildUserPrompt(Scenario("process"), DiagramLanguages.Enzo);

        Assert.Contains("Use Enzo `bpmn`", prompt);
        Assert.Contains("not flow/sequence", prompt);
        Assert.Contains("Acyclic", prompt);
        Assert.Contains("no back-edges", prompt);
        Assert.Contains("A -> B : yes", prompt);
        Assert.Contains("never `A ->|yes| B`", prompt);
        Assert.Contains(">=8 nodes, >=9 edges, >=2 decisions", prompt);
    }

    [Fact]
    public void SequencePrompt_DoesNotReceiveFlowOrBpmnCycleGuidance()
    {
        var prompt = GenerationPromptBuilder.BuildUserPrompt(Scenario("sequence"), DiagramLanguages.Enzo);

        Assert.Contains("Use Enzo `sequence` syntax, not `flow` or `bpmn`.", prompt);
        Assert.DoesNotContain("Acyclic", prompt);
        Assert.DoesNotContain("retry/rework", prompt);
        Assert.DoesNotContain("A -> B : yes", prompt);
        Assert.DoesNotContain("A ->|yes| B", prompt);
    }

    [Fact]
    public void SequencePrompt_PreservesSourceOnlyOutputContract()
    {
        var prompt = GenerationPromptBuilder.BuildUserPrompt(Scenario("sequence"), DiagramLanguages.Enzo);

        Assert.Contains("Return the complete diagram source only", prompt);
        Assert.Contains("Do not use Markdown fences or explanations", prompt);
    }

    private static DiagramScenario Scenario(string kind) => new(
        $"sample-{kind}",
        kind,
        "complex",
        "Route a request with approval, rejection, and possible rework.",
        "",
        "",
        new DiagramExpectations(kind, ["approval", "rejection", "rework"], 8, 9, 2));
}
