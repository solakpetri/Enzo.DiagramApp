using Enzo.Diagrams.LlmBenchmarks;

namespace Enzo.Diagrams.Benchmarks.Tests;

public sealed class LlmBenchmarkTests
{
    [Fact]
    public void Extract_ReturnsOpenAiUsageTokens()
    {
        var usage = OpenAiUsageParser.Extract("""
            {"choices":[{"message":{"content":"flow A"}}],"usage":{"prompt_tokens":10,"completion_tokens":3,"total_tokens":13}}
            """);

        Assert.Equal(new TokenUsage(10, 3, 13), usage);
    }

    [Fact]
    public void RunResult_CalculatesValidityAndRepairTokens()
    {
        var result = new LlmRunResult("s1", "flow", "simple", DiagramLanguages.Enzo, "m", 1, [
            Attempt(0, false, 10, 5, false),
            Attempt(1, true, 8, 4, true)], null);

        Assert.False(result.FirstPassValid);
        Assert.True(result.FinalValid);
        Assert.Equal(1, result.RepairAttempts);
        Assert.Equal(8, result.RepairInputTokens);
        Assert.Equal(4, result.RepairOutputTokens);
        Assert.Equal(12, result.TotalRepairTokens);
        Assert.Equal(27, result.TokensToValidDiagram);
    }

    [Fact]
    public void Difference_ReportsFewerMoreAndEqualDirections()
    {
        Assert.Equal(25, LlmMetrics.Difference(75, 100));
        Assert.Equal(-10, LlmMetrics.Difference(110, 100));
        Assert.Contains("fewer", LlmMetrics.Direction(25));
        Assert.Contains("more", LlmMetrics.Direction(-10));
        Assert.Contains("same", LlmMetrics.Direction(0));
    }

    [Fact]
    public async Task RunAsync_RepairsInvalidGenerationAndSerializesResults()
    {
        var output = TempDirectory();
        var client = new FakeClient(new Queue<ModelResponse>([
            new ModelResponse("bad", new TokenUsage(10, 5, 15), TimeSpan.FromMilliseconds(1)),
            new ModelResponse("```enzo\nvalid\n```", new TokenUsage(8, 4, 12), TimeSpan.FromMilliseconds(1))]));
        var runner = Runner(client, new SequenceValidator(new Queue<bool>([false, true])), output);

        var run = await runner.RunAsync(ScenariosDirectory(), Options(output, language: DiagramLanguages.Enzo), CancellationToken.None);

        var result = Assert.Single(run.Results);
        Assert.True(result.FinalValid);
        Assert.False(result.FirstPassValid);
        Assert.True(result.NormalizationApplied);
        Assert.Equal(27, result.TokensToValidDiagram);
        var jsonPath = Directory.EnumerateFiles(output, "*.json").Single();
        var serialized = System.Text.Json.JsonSerializer.Deserialize<LlmBenchmarkRun>(File.ReadAllText(jsonPath), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.Equal(result.TokensToValidDiagram, Assert.Single(serialized!.Results).TokensToValidDiagram);
        Assert.Single(Directory.EnumerateFiles(output, "*.csv"));
        Assert.Single(Directory.EnumerateFiles(output, "*.md"));
    }

    [Fact]
    public async Task RunAsync_ResumeSkipsCompletedScenarioRun()
    {
        var output = TempDirectory();
        var firstClient = new FakeClient(new Queue<ModelResponse>([new ModelResponse("valid", new TokenUsage(1, 2, 3), TimeSpan.Zero)]));
        await Runner(firstClient, new SequenceValidator(new Queue<bool>([true])), output).RunAsync(ScenariosDirectory(), Options(output, language: DiagramLanguages.Enzo), CancellationToken.None);
        var resumeFile = Directory.EnumerateFiles(output, "*.json").Single();
        var secondClient = new FakeClient(new Queue<ModelResponse>([new ModelResponse("should not run", new TokenUsage(9, 9, 18), TimeSpan.Zero)]));

        var resumed = await Runner(secondClient, new SequenceValidator(new Queue<bool>([true])), output).RunAsync(ScenariosDirectory(), Options(output, language: DiagramLanguages.Enzo, resume: resumeFile), CancellationToken.None);

        Assert.Single(resumed.Results);
        Assert.Equal(0, secondClient.Calls);
    }

    [Fact]
    public async Task RunAsync_PersistentApiFailureRecordsFailedRun()
    {
        var output = TempDirectory();
        var runner = Runner(new ThrowingClient(), new SequenceValidator(new Queue<bool>([true])), output);

        var run = await runner.RunAsync(ScenariosDirectory(), Options(output, language: DiagramLanguages.Enzo), CancellationToken.None);

        var result = Assert.Single(run.Results);
        Assert.Equal("api", result.ErrorCategory);
        Assert.False(result.FinalValid);
        Assert.Equal(0, result.TokensToValidDiagram);
    }

    [Fact]
    public void Generate_IncludesSummaryAndBreakdowns()
    {
        var run = new LlmBenchmarkRun(new LlmBenchmarkMetadata("r", DateTimeOffset.UtcNow, "m", 1, 3, 0.2, 1, 100, "all", "all", "all"), [
            new LlmRunResult("s1", "flow", "simple", DiagramLanguages.Enzo, "m", 1, [Attempt(0, false, 10, 5, true)], null),
            new LlmRunResult("s1", "flow", "simple", DiagramLanguages.Mermaid, "m", 1, [Attempt(0, false, 20, 10, true)], null)]);

        var report = LlmMarkdownReport.Generate(run);

        Assert.Contains("# Enzo vs Mermaid - LLM Generation Benchmark", report);
        Assert.Contains("Avg tokens to valid diagram", report);
        Assert.Contains("Cold start includes", report);
        Assert.Contains("| flow |", report);
    }

    private static GenerationAttempt Attempt(int number, bool repair, int input, int output, bool valid) =>
        new(number, repair, input, output, input + output, valid ? "valid" : "bad", valid ? "valid" : "bad", false, valid, valid, valid ? null : "invalid", 1);

    private static LlmBenchmarkRunner Runner(IDiagramModelClient client, IDiagramValidator validator, string output) =>
        new(client, new Dictionary<string, IDiagramValidator> { [DiagramLanguages.Enzo] = validator, [DiagramLanguages.Mermaid] = validator }, Prompts(), new BenchmarkOutputWriter());

    private static BenchmarkOptions Options(string output, string language, string? resume = null) =>
        new(1, 3, "fake-model", 0.2, 1, 100, "flow-login-basic", null, language, output, resume, "mmdc");

    private static PromptStore Prompts() => new(Path.Combine(RepositoryRoot(), "benchmarks", "Enzo.Diagrams.LlmBenchmarks", "prompts"));
    private static string ScenariosDirectory() => Path.Combine(RepositoryRoot(), "benchmarks", "scenarios");
    private static string TempDirectory() => Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"enzo-llm-tests-{Guid.NewGuid():N}")).FullName;

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Enzo.Diagrams.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find repository root.");
    }

    private sealed class FakeClient(Queue<ModelResponse> responses) : IDiagramModelClient
    {
        public int Calls { get; private set; }

        public Task<ModelResponse> CompleteAsync(string systemPrompt, string userPrompt, ModelSettings settings, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(responses.Dequeue());
        }
    }

    private sealed class ThrowingClient : IDiagramModelClient
    {
        public Task<ModelResponse> CompleteAsync(string systemPrompt, string userPrompt, ModelSettings settings, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("api failed");
    }

    private sealed class SequenceValidator(Queue<bool> results) : IDiagramValidator
    {
        public Task<ValidationOutcome> ValidateAsync(string source, CancellationToken cancellationToken)
        {
            var valid = results.Dequeue();
            return Task.FromResult(new ValidationOutcome(valid, valid, valid ? null : "invalid"));
        }
    }
}
