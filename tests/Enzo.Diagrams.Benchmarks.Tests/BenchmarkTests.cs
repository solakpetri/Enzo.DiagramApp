using Enzo.Diagrams.Benchmarks;

namespace Enzo.Diagrams.Benchmarks.Tests;

public sealed class BenchmarkTests
{
    [Fact]
    public void Load_CommittedScenarios_ReturnsThirtyScenarios()
    {
        var scenarios = ScenarioLoader.Load(Path.Combine(RepositoryRoot(), "benchmarks", "scenarios"));

        Assert.Equal(30, scenarios.Count);
        Assert.Equal(10, scenarios.Count(scenario => scenario.Category == "flow"));
        Assert.Equal(10, scenarios.Count(scenario => scenario.Category == "sequence"));
        Assert.Equal(10, scenarios.Count(scenario => scenario.Category == "process"));
    }

    [Fact]
    public void Calculate_ReturnsTokenizerAndRepresentationMetrics()
    {
        var metrics = new MetricCalculator("cl100k_base").Calculate("flow A\n\ntask B \"Build\"", 2, 1);

        Assert.Equal(22, metrics.Utf8Bytes);
        Assert.Equal(22, metrics.Characters);
        Assert.Equal(2, metrics.NonEmptyLines);
        Assert.True(metrics.Tokens > 0);
        Assert.Equal(metrics.Tokens / 2.0, metrics.TokensPerElement);
        Assert.Equal(metrics.Tokens, metrics.TokensPerConnection);
    }

    [Fact]
    public void Difference_FormatsFewerMoreAndEqualDirections()
    {
        Assert.Equal(25, Percentage.Difference(75, 100));
        Assert.Equal("Enzo uses 25.0% fewer tokens than Mermaid.", Percentage.Direction(25));
        Assert.Equal("Enzo uses 10.0% more tokens than Mermaid.", Percentage.Direction(-10));
        Assert.Equal("Enzo and Mermaid use the same number of tokens.", Percentage.Direction(0));
    }

    [Fact]
    public void Compare_FlagsStructuralDifferences()
    {
        var enzo = new DiagramStructure("flow", ["Start"], ["Start->End:"], []);
        var mermaid = new DiagramStructure("flow", ["Start", "End"], ["Start->End:"], []);

        var errors = DiagramEquivalence.Compare(enzo, mermaid);

        Assert.Contains(errors, error => error.Contains("element count"));
    }

    [Fact]
    public void Run_CommittedScenarios_ValidatesFixtures()
    {
        var result = BenchmarkRunner.Run(Path.Combine(RepositoryRoot(), "benchmarks", "scenarios"));

        Assert.Equal(30, result.TotalScenarios);
        Assert.All(result.Scenarios, scenario => Assert.True(scenario.Enzo.Tokens > 0 && scenario.Mermaid.Tokens > 0));
    }

    [Fact]
    public void RunScenario_InvalidEnzoFixture_Throws()
    {
        var scenario = ValidScenario() with { Enzo = "flow Broken\nstart Begin \"Begin\"" };

        Assert.Throws<BenchmarkValidationException>(() => BenchmarkRunner.RunScenario(scenario, new MetricCalculator("cl100k_base")));
    }

    [Fact]
    public void RunScenario_InvalidMermaidFixture_Throws()
    {
        var scenario = ValidScenario() with { Mermaid = "flowchart TD\nBegin ==> Done" };

        Assert.Throws<BenchmarkValidationException>(() => BenchmarkRunner.RunScenario(scenario, new MetricCalculator("cl100k_base")));
    }

    [Fact]
    public void Generate_IncludesDirectionAndBreakdowns()
    {
        var result = new BenchmarkRunResult("cl100k_base", [new ScenarioBenchmarkResult(
            "s1",
            "flow",
            "simple",
            new RepresentationMetrics(1, 1, 1, 50, 10, 10),
            new RepresentationMetrics(1, 1, 1, 100, 20, 20),
            50,
            new DiagramStructure("flow", ["A"], ["A->B:"], []),
            new DiagramStructure("flow", ["A"], ["A->B:"], []))]);

        var markdown = MarkdownReport.Generate(result);

        Assert.Contains("Scenarios: 1", markdown);
        Assert.Contains("Enzo uses 50.0% fewer tokens than Mermaid.", markdown);
        Assert.Contains("| flow | 1 | 50 | 100 |", markdown);
    }

    private static DiagramScenario ValidScenario()
    {
        return new DiagramScenario(
            "valid",
            "flow",
            "simple",
            "Connect a start node to an end node.",
            "flow Valid\nstart Begin \"Begin\"\nend Done \"Done\"\nBegin -> Done",
            "flowchart TD\nBegin([Begin])\nDone([Done])\nBegin --> Done",
            new DiagramExpectations("flow", ["begin", "done"], MinimumNodeCount: 2, MinimumEdgeCount: 1));
    }

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
