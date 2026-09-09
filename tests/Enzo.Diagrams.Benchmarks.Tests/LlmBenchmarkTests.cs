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
            Attempt(1, true, 8, 4, true, equivalent: true)], null);

        Assert.False(result.FirstPassValid);
        Assert.True(result.FinalValid);
        Assert.Equal(1, result.RepairAttempts);
        Assert.Equal(8, result.RepairInputTokens);
        Assert.Equal(4, result.RepairOutputTokens);
        Assert.Equal(12, result.TotalRepairTokens);
        Assert.Equal(27, result.TokensToValidDiagram);
        Assert.Equal(27, result.TokensToValidEquivalentDiagram);
        Assert.True(result.EquivalentValid);
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
    public void EquivalentDirection_RequiresHighEquivalentValidity()
    {
        Assert.Contains("below the 95%", LlmMetrics.EquivalentDirection(100, 80, 90, 100));
        Assert.Contains("fewer", LlmMetrics.EquivalentDirection(80, 100, 95, 95));
    }

    [Fact]
    public void GetDefaultExecutable_ReturnsWindowsNpmCommandShim()
    {
        Assert.Equal("mmdc.cmd", MermaidExecutableResolver.GetDefaultExecutable(isWindows: true));
    }

    [Fact]
    public void GetDefaultExecutable_ReturnsUnixCommandName()
    {
        Assert.Equal("mmdc", MermaidExecutableResolver.GetDefaultExecutable(isWindows: false));
    }

    [Fact]
    public async Task RunAsync_RepairsInvalidGenerationAndSerializesResults()
    {
        var output = TempDirectory();
        var client = new FakeClient(new Queue<ModelResponse>([
            new ModelResponse("bad", new TokenUsage(10, 5, 15), TimeSpan.FromMilliseconds(1)),
            new ModelResponse($"```enzo\n{ValidFlowSource()}\n```", new TokenUsage(8, 4, 12), TimeSpan.FromMilliseconds(1))]));
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
        var csvPath = Assert.Single(Directory.EnumerateFiles(output, "*.csv"));
        Assert.Contains("sequenceFailureCategories,repairTypes,repairFailureCategories,repairSuccesses", File.ReadAllText(csvPath));
        Assert.Single(Directory.EnumerateFiles(output, "*.md"));
    }

    [Fact]
    public async Task RunAsync_FirstPassEquivalentDoesNotRepair()
    {
        var output = TempDirectory();
        var client = new FakeClient(new Queue<ModelResponse>([new ModelResponse(ValidFlowSource(), new TokenUsage(10, 5, 15), TimeSpan.Zero)]));

        var result = Assert.Single((await Runner(client, AlwaysValid(1), output).RunAsync(ScenariosDirectory(), Options(output, language: DiagramLanguages.Enzo), CancellationToken.None)).Results);

        Assert.True(result.FirstPassEquivalentValid);
        Assert.True(result.EquivalentValid);
        Assert.Equal(0, result.RepairAttempts);
        Assert.Equal(15, result.TokensToValidEquivalentDiagram);
    }

    [Fact]
    public async Task RunAsync_BuildsSharedSemanticPromptWithLanguageKindInstruction()
    {
        var output = TempDirectory();
        var client = new FakeClient(new Queue<ModelResponse>([new ModelResponse(ValidFlowSource(), new TokenUsage(10, 5, 15), TimeSpan.Zero)]));

        await Runner(client, AlwaysValid(1), output).RunAsync(ScenariosDirectory(), Options(output, language: DiagramLanguages.Enzo), CancellationToken.None);

        var prompt = Assert.Single(client.UserPrompts);
        Assert.Contains("Authenticate a user by accepting credentials", prompt);
        Assert.Contains("- Diagram kind: flow", prompt);
        Assert.Contains("- At least 6 nodes", prompt);
        Assert.Contains("Use Enzo `flow` syntax", prompt);
        Assert.DoesNotContain("MinimumNodeCount", prompt);
    }

    [Fact]
    public void BuildUserPrompt_IncludesSequenceParticipantsAndInteractions()
    {
        var scenario = new DiagramScenario(
            "s",
            "sequence",
            "simple",
            "Show a user calling an order API.",
            string.Empty,
            string.Empty,
            new DiagramExpectations(
                "sequence",
                ["order"],
                MinimumParticipantCount: 2,
                MinimumInteractionCount: 1,
                RequiredParticipants: ["User", "Order API"],
                RequiredInteractions: [new RequiredInteraction("User", "Order API")]));

        var prompt = GenerationPromptBuilder.BuildUserPrompt(scenario, DiagramLanguages.Mermaid);

        Assert.Contains("Include these participants", prompt);
        Assert.Contains("- User", prompt);
        Assert.Contains("User -> Order API", prompt);
        Assert.Contains("Use Mermaid `sequenceDiagram` syntax", prompt);
    }

    [Fact]
    public async Task RunAsync_WrongKindCanBeRepairedToEquivalent()
    {
        var output = TempDirectory();
        var client = new FakeClient(new Queue<ModelResponse>([
            new ModelResponse(ValidSequenceSource(), new TokenUsage(10, 5, 15), TimeSpan.Zero),
            new ModelResponse(ValidFlowSource(), new TokenUsage(8, 4, 12), TimeSpan.Zero)]));

        var result = Assert.Single((await Runner(client, AlwaysValid(2), output).RunAsync(ScenariosDirectory(), Options(output, language: DiagramLanguages.Enzo), CancellationToken.None)).Results);

        Assert.True(result.FinalValid);
        Assert.True(result.EquivalentValid);
        Assert.Contains("Kind", result.Attempts[1].RepairType);
        Assert.Contains("Expected diagram kind: flow. Actual: sequence.", client.UserPrompts[1]);
        Assert.Equal(15, result.TokensToValidDiagram);
        Assert.Equal(27, result.TokensToValidEquivalentDiagram);
    }

    [Fact]
    public async Task RunAsync_MissingConceptCanBeRepairedToEquivalent()
    {
        var output = TempDirectory();
        var client = new FakeClient(new Queue<ModelResponse>([
            new ModelResponse(MissingDashboardFlowSource("1"), new TokenUsage(10, 5, 15), TimeSpan.Zero),
            new ModelResponse(ValidFlowSource(), new TokenUsage(8, 4, 12), TimeSpan.Zero)]));

        var result = Assert.Single((await Runner(client, AlwaysValid(2), output).RunAsync(ScenariosDirectory(), Options(output, language: DiagramLanguages.Enzo), CancellationToken.None)).Results);

        Assert.True(result.EquivalentValid);
        Assert.Equal("Semantic", result.Attempts[1].RepairType);
        Assert.Equal(EnzoRepairFailureCategories.MissingConcept, result.Attempts[1].RepairFailureCategory);
        Assert.True(result.Attempts[1].RepairSuccessful);
        Assert.Contains("Missing required concept: dashboard.", client.UserPrompts[1]);
        Assert.DoesNotContain("Expected at least 6 nodes", client.UserPrompts[1]);
        Assert.Equal(27, result.TokensToValidEquivalentDiagram);
    }

    [Fact]
    public async Task RunAsync_StructuralFailureCanBeRepairedToEquivalent()
    {
        var output = TempDirectory();
        var client = new FakeClient(new Queue<ModelResponse>([
            new ModelResponse(StructurallyShortFlowSource(), new TokenUsage(10, 5, 15), TimeSpan.Zero),
            new ModelResponse(ValidFlowSource(), new TokenUsage(8, 4, 12), TimeSpan.Zero)]));

        var result = Assert.Single((await Runner(client, AlwaysValid(2), output).RunAsync(ScenariosDirectory(), Options(output, language: DiagramLanguages.Enzo), CancellationToken.None)).Results);

        Assert.True(result.EquivalentValid);
        Assert.Equal("Structure", result.Attempts[1].RepairType);
        Assert.Contains("Expected at least 6 nodes. Found 4.", client.UserPrompts[1]);
    }

    [Fact]
    public async Task RunAsync_SemanticRepairFailsAfterMaximumAttempts()
    {
        var output = TempDirectory();
        var client = new FakeClient(new Queue<ModelResponse>([
            new ModelResponse(MissingDashboardFlowSource("1"), new TokenUsage(10, 5, 15), TimeSpan.Zero),
            new ModelResponse(MissingDashboardFlowSource("2"), new TokenUsage(8, 4, 12), TimeSpan.Zero),
            new ModelResponse(MissingDashboardFlowSource("3"), new TokenUsage(7, 4, 11), TimeSpan.Zero),
            new ModelResponse(MissingDashboardFlowSource("4"), new TokenUsage(6, 4, 10), TimeSpan.Zero)]));

        var result = Assert.Single((await Runner(client, AlwaysValid(4), output).RunAsync(ScenariosDirectory(), Options(output, language: DiagramLanguages.Enzo), CancellationToken.None)).Results);

        Assert.False(result.EquivalentValid);
        Assert.Null(result.TokensToValidEquivalentDiagram);
        Assert.Equal(3, result.RepairAttempts);
        Assert.Equal("validation", result.ErrorCategory);
    }

    [Fact]
    public async Task RunAsync_IdenticalRepairGetsOneEscalatedChance()
    {
        var output = TempDirectory();
        var repeated = MissingDashboardFlowSource("1");
        var client = new FakeClient(new Queue<ModelResponse>([
            new ModelResponse(repeated, new TokenUsage(10, 5, 15), TimeSpan.Zero),
            new ModelResponse(repeated, new TokenUsage(8, 4, 12), TimeSpan.Zero),
            new ModelResponse(ValidFlowSource(), new TokenUsage(7, 3, 10), TimeSpan.Zero)]));

        var result = Assert.Single((await Runner(client, AlwaysValid(3), output).RunAsync(ScenariosDirectory(), Options(output, language: DiagramLanguages.Enzo), CancellationToken.None)).Results);

        Assert.True(result.EquivalentValid);
        Assert.Equal(3, client.Calls);
        Assert.Null(result.RepairStoppedReason);
        Assert.Contains("previous repair did not change", client.UserPrompts[2]);
        Assert.Contains("You must modify the source", client.UserPrompts[2]);
        Assert.Contains("Flowchart:", client.SystemPrompts[0]);
        Assert.DoesNotContain("Flowchart:", client.SystemPrompts[1]);
    }

    [Fact]
    public async Task RunAsync_RepeatedIdenticalRepairStopsAfterEscalation()
    {
        var output = TempDirectory();
        var repeated = MissingDashboardFlowSource("1");
        var client = new FakeClient(new Queue<ModelResponse>([
            new ModelResponse(repeated, new TokenUsage(10, 5, 15), TimeSpan.Zero),
            new ModelResponse(repeated, new TokenUsage(8, 4, 12), TimeSpan.Zero),
            new ModelResponse(repeated, new TokenUsage(7, 3, 10), TimeSpan.Zero)]));

        var result = Assert.Single((await Runner(client, AlwaysValid(3), output).RunAsync(ScenariosDirectory(), Options(output, language: DiagramLanguages.Enzo), CancellationToken.None)).Results);

        Assert.False(result.EquivalentValid);
        Assert.Equal(3, client.Calls);
        Assert.Equal("identical-output", result.RepairStoppedReason);
        Assert.Equal(15, result.RepairInputTokens);
        Assert.Equal(7, result.RepairOutputTokens);
        Assert.Equal(22, result.TotalRepairTokens);
        Assert.Equal(37, result.Attempts.Sum(attempt => attempt.InputTokens + attempt.OutputTokens));
        Assert.Equal(15, result.TokensToValidDiagram);
        Assert.Equal(2, LlmMetrics.Aggregate(DiagramLanguages.Enzo, [result]).IdenticalOutputRepairs);
    }

    [Fact]
    public async Task RunAsync_OscillationUsesOneFocusedFinalAttempt()
    {
        var output = TempDirectory();
        var sourceA = MissingDashboardFlowSource("1");
        var sourceB = MissingDenyFlowSource();
        var client = new FakeClient(new Queue<ModelResponse>([
            new ModelResponse(sourceA, new TokenUsage(10, 5, 15), TimeSpan.Zero),
            new ModelResponse(sourceB, new TokenUsage(8, 4, 12), TimeSpan.Zero),
            new ModelResponse(sourceA, new TokenUsage(7, 4, 11), TimeSpan.Zero),
            new ModelResponse(MissingDashboardFlowSource("3"), new TokenUsage(6, 3, 9), TimeSpan.Zero)]));

        var result = Assert.Single((await Runner(client, AlwaysValid(4), output).RunAsync(ScenariosDirectory(), Options(output, language: DiagramLanguages.Enzo), CancellationToken.None)).Results);

        Assert.False(result.EquivalentValid);
        Assert.Equal(4, client.Calls);
        Assert.Equal("repair-oscillation", result.RepairStoppedReason);
        Assert.Contains("alternate between invalid forms", client.UserPrompts[3]);
        Assert.Contains("Required condition:", client.UserPrompts[3]);
        Assert.Equal(1, LlmMetrics.Aggregate(DiagramLanguages.Enzo, [result]).OscillationRepairs);
    }

    [Fact]
    public async Task RunAsync_MermaidIdenticalRepairStillStopsImmediately()
    {
        var output = TempDirectory();
        const string source = "flowchart TD\nA[Credentials]\nB[Done]\nA --> B";
        var client = new FakeClient(new Queue<ModelResponse>([
            new ModelResponse(source, new TokenUsage(10, 5, 15), TimeSpan.Zero),
            new ModelResponse(source, new TokenUsage(8, 4, 12), TimeSpan.Zero)]));

        var result = Assert.Single((await Runner(client, AlwaysValid(2), output).RunAsync(ScenariosDirectory(), Options(output, language: DiagramLanguages.Mermaid), CancellationToken.None)).Results);

        Assert.Equal(2, client.Calls);
        Assert.Equal("identical-output", result.RepairStoppedReason);
        Assert.Contains("The diagram source does not satisfy the request", client.UserPrompts[1]);
    }

    [Fact]
    public async Task RunAsync_RecordsSyntaxRegressionAndRepairOutcome()
    {
        var output = TempDirectory();
        var client = new FakeClient(new Queue<ModelResponse>([
            new ModelResponse(StructurallyShortFlowSource(), new TokenUsage(10, 5, 15), TimeSpan.Zero),
            new ModelResponse("bad", new TokenUsage(8, 4, 12), TimeSpan.Zero),
            new ModelResponse(ValidFlowSource(), new TokenUsage(7, 3, 10), TimeSpan.Zero)]));

        var result = Assert.Single((await Runner(client, new SequenceValidator(new Queue<bool>([true, false, true])), output).RunAsync(ScenariosDirectory(), Options(output, language: DiagramLanguages.Enzo), CancellationToken.None)).Results);

        Assert.True(result.EquivalentValid);
        Assert.True(result.Attempts[1].IntroducedSyntaxFailure);
        Assert.False(result.Attempts[1].RepairSuccessful);
        Assert.Equal(EnzoRepairFailureCategories.Syntax, result.Attempts[2].RepairFailureCategory);
        Assert.True(result.Attempts[2].RepairSuccessful);
        var aggregate = LlmMetrics.Aggregate(DiagramLanguages.Enzo, [result]);
        Assert.Equal(2, aggregate.RepairAttemptsStarted);
        Assert.Equal(15, aggregate.AverageRepairInputTokens);
        Assert.Equal(50, aggregate.RepairSuccessRatePercent);
        Assert.Equal(1, aggregate.RepairsResolvedSyntax);
        Assert.Equal(1, aggregate.RepairsIntroducedSyntaxFailure);
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
        Assert.Contains("Avg successful tokens to valid equivalent diagram", report);
        Assert.Contains("Avg repair input tokens", report);
        Assert.Contains("Enzo Repair Convergence", report);
        Assert.Contains("Repairs that introduced new syntax failure", report);
        Assert.Contains("Equivalent By Category", report);
        Assert.Contains("Cold start includes", report);
        Assert.Contains("Source Efficiency", report);
        Assert.Contains("By Interaction Size", report);
        Assert.Contains("| flow |", report);
    }

    [Fact]
    public void Parse_SequenceSuite_UsesIsolatedDefaultResultsDirectory()
    {
        var root = TempDirectory();
        var broad = Directory.CreateDirectory(Path.Combine(root, "benchmarks", "results", "llm-generation-cost")).FullName;
        var existing = Path.Combine(broad, "existing.json");
        File.WriteAllText(existing, "broad");

        var options = BenchmarkOptions.Parse(["--suite", "sequence", "--runs", "1"], root);
        var writer = new BenchmarkOutputWriter();
        var run = writer.LoadOrCreate(options);
        writer.Write(run);

        Assert.Equal("sequence", options.Suite);
        Assert.EndsWith(Path.Combine("benchmarks", "results", "sequence-generation"), options.OutputDirectory);
        Assert.Equal("broad", File.ReadAllText(existing));
        Assert.Contains("sequence-generation", Directory.GetFiles(options.OutputDirectory, "*.json").Single());
        Assert.Equal("sequence", run.Metadata.Suite);
    }

    [Fact]
    public void Reevaluate_WritesSeparateSemanticReportWithoutChangingOriginal()
    {
        var output = TempDirectory();
        var input = Path.Combine(output, "historical.json");
        var run = new LlmBenchmarkRun(new LlmBenchmarkMetadata("r", DateTimeOffset.UtcNow, "m", 1, 0, 0.2, 1, 100, "flow-login-basic", "all", DiagramLanguages.Mermaid), [
            new LlmRunResult("sequence-login-basic", "sequence", "simple", DiagramLanguages.Mermaid, "m", 1, [
                new GenerationAttempt(0, false, 1, 2, 3, "flowchart TD\nA[User]\nB[Api]\nA --> B", "flowchart TD\nA[User]\nB[Api]\nA --> B", false, true, true, null, 1)], null)]);
        var jsonOptions = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web) { WriteIndented = true };
        File.WriteAllText(input, System.Text.Json.JsonSerializer.Serialize(run, jsonOptions));
        var original = File.ReadAllText(input);

        var enrichedPath = SemanticReevaluation.Run(input, ScenariosDirectory());

        Assert.NotEqual(input, enrichedPath);
        Assert.Equal(original, File.ReadAllText(input));
        var enriched = System.Text.Json.JsonSerializer.Deserialize<LlmBenchmarkRun>(File.ReadAllText(enrichedPath), jsonOptions)!;
        Assert.False(Assert.Single(enriched.Results).EquivalentValid);
        Assert.Contains("Expected sequence", Assert.Single(enriched.Results).FailureReasons[0]);
    }

    private static GenerationAttempt Attempt(int number, bool repair, int input, int output, bool valid, bool equivalent = false) =>
        new(number, repair, input, output, input + output, valid ? "valid" : "bad", valid ? "valid" : "bad", false, valid, valid, valid ? null : "invalid", 1, valid, valid, equivalent, equivalent, equivalent, equivalent, equivalent ? [] : ["not equivalent"], null);

    private static LlmBenchmarkRunner Runner(IDiagramModelClient client, IDiagramValidator validator, string output) =>
        new(client, new Dictionary<string, IDiagramValidator> { [DiagramLanguages.Enzo] = validator, [DiagramLanguages.Mermaid] = validator }, Prompts(), new BenchmarkOutputWriter());

    private static BenchmarkOptions Options(string output, string language, string? resume = null) =>
        new(1, 3, "fake-model", 0.2, 1, 100, "flow-login-basic", null, language, output, resume, null, "mmdc", "all");
    private static SequenceValidator AlwaysValid(int count) => new(new Queue<bool>(Enumerable.Repeat(true, count)));

    private static PromptStore Prompts() => new(Path.Combine(RepositoryRoot(), "benchmarks", "Enzo.Diagrams.LlmBenchmarks", "prompts"));
    private static string ScenariosDirectory() => Path.Combine(RepositoryRoot(), "benchmarks", "scenarios");
    private static string TempDirectory() => Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"enzo-llm-tests-{Guid.NewGuid():N}")).FullName;

    private static string ValidFlowSource() => """
        flow LoginBasic
        start Begin "Credentials entered"
        task Validate "Validate credentials"
        decision Valid "Credentials valid?"
        task Dashboard "Show dashboard"
        end Denied "Deny access"
        end Done "Login complete"
        Begin -> Validate
        Validate -> Valid
        Valid -> Dashboard : yes
        Valid -> Denied : no
        Dashboard -> Done
        """;

    private static string MissingDashboardFlowSource(string suffix) => $$"""
        flow LoginBasic
        start Begin "Credentials entered"
        task Validate "Validate credentials"
        decision Valid "Credentials valid?"
        task Home "Show home {{suffix}}"
        end Denied "Deny access"
        end Done "Login complete"
        Begin -> Validate
        Validate -> Valid
        Valid -> Home : yes
        Valid -> Denied : no
        Home -> Done
        """;

    private static string MissingDenyFlowSource() => """
        flow LoginBasic
        start Begin "Credentials entered"
        task Validate "Validate credentials"
        decision Valid "Credentials valid?"
        task Dashboard "Show dashboard"
        end Blocked "Block login"
        end Done "Login complete"
        Begin -> Validate
        Validate -> Valid
        Valid -> Dashboard : yes
        Valid -> Blocked : no
        Dashboard -> Done
        """;

    private static string StructurallyShortFlowSource() => """
        flow LoginBasic
        start Begin "credentials"
        task Validate "validate credentials"
        decision Valid "dashboard deny access?"
        end Done "done"
        Begin -> Validate
        Validate -> Valid
        Valid -> Done : yes
        """;

    private static string ValidSequenceSource() => """
        sequence LoginBasic
        actor User
        participant Api
        User -> Api: credentials validate credentials dashboard deny access
        Api --> User: done
        """;

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
        public List<string> SystemPrompts { get; } = [];
        public List<string> UserPrompts { get; } = [];

        public Task<ModelResponse> CompleteAsync(string systemPrompt, string userPrompt, ModelSettings settings, CancellationToken cancellationToken)
        {
            Calls++;
            SystemPrompts.Add(systemPrompt);
            UserPrompts.Add(userPrompt);
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
