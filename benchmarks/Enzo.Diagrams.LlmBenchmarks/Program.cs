namespace Enzo.Diagrams.LlmBenchmarks;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var root = FindRepositoryRoot();
        var options = BenchmarkOptions.Parse(args, root);
        var scenariosDirectory = Path.Combine(root, "benchmarks", "scenarios");
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.Error.WriteLine("OPENAI_API_KEY is required to run the LLM generation benchmark.");
            return 1;
        }

        try
        {
            using var httpClient = new HttpClient();
            var prompts = new PromptStore(Path.Combine(root, "benchmarks", "Enzo.Diagrams.LlmBenchmarks", "prompts"));
            var mermaid = new MermaidCliValidator(options.MermaidCommand);
            if (options.LanguageFilter is "all" or DiagramLanguages.Mermaid)
            {
                var availability = await mermaid.EnsureAvailableAsync(CancellationToken.None);
                if (!availability.IsValid)
                {
                    Console.Error.WriteLine(availability.Error);
                    return 1;
                }
            }

            var validators = new Dictionary<string, IDiagramValidator>
            {
                [DiagramLanguages.Enzo] = new EnzoDiagramValidator(),
                [DiagramLanguages.Mermaid] = mermaid
            };
            var runner = new LlmBenchmarkRunner(new OpenAiChatClient(httpClient, apiKey), validators, prompts, new BenchmarkOutputWriter());
            var run = await runner.RunAsync(scenariosDirectory, options, CancellationToken.None);
            Console.WriteLine(LlmMarkdownReport.Generate(run));
            return 0;
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Environment.CurrentDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Enzo.Diagrams.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Environment.CurrentDirectory;
    }
}
