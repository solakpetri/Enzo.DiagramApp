namespace Enzo.Diagrams.Benchmarks;

public static class Program
{
    public static int Main(string[] args)
    {
        var encoding = args.FirstOrDefault(arg => arg.StartsWith("--encoding=", StringComparison.OrdinalIgnoreCase))?.Split('=', 2)[1]
            ?? "cl100k_base";
        var root = FindRepositoryRoot();
        var scenariosDirectory = Path.Combine(root, "benchmarks", "scenarios");
        var resultsDirectory = Path.Combine(root, "benchmarks", "results");

        try
        {
            var result = BenchmarkRunner.Run(scenariosDirectory, encoding);
            Directory.CreateDirectory(resultsDirectory);
            BenchmarkWriters.Write(result, resultsDirectory);
            Console.WriteLine(MarkdownReport.SummaryTable(result));
            Console.WriteLine(Percentage.Direction(result.TokenDifferencePercent));
            return 0;
        }
        catch (Exception ex) when (ex is BenchmarkValidationException or DirectoryNotFoundException)
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
            if (Directory.Exists(Path.Combine(directory.FullName, "benchmarks", "scenarios")) || File.Exists(Path.Combine(directory.FullName, "Enzo.Diagrams.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Environment.CurrentDirectory;
    }
}
