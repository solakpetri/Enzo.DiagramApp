using System.Text.Json;

namespace Enzo.Diagrams.Benchmarks;

public static class ScenarioLoader
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static IReadOnlyList<DiagramScenario> Load(string scenariosDirectory)
    {
        if (!Directory.Exists(scenariosDirectory))
        {
            throw new DirectoryNotFoundException($"Scenario directory not found: {scenariosDirectory}");
        }

        var scenarios = Directory.EnumerateFiles(scenariosDirectory, "*.json", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .SelectMany(ReadFile)
            .ToList();

        var duplicateId = scenarios.GroupBy(scenario => scenario.Id).FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateId is not null)
        {
            throw new BenchmarkValidationException($"Duplicate scenario id: {duplicateId}");
        }

        foreach (var scenario in scenarios)
        {
            if (scenario.Expectations is null)
            {
                throw new BenchmarkValidationException($"Scenario '{scenario.Id}' is missing semantic expectations.");
            }

            if (!string.Equals(scenario.Category, scenario.Expectations.ExpectedKind, StringComparison.Ordinal))
            {
                throw new BenchmarkValidationException($"Scenario '{scenario.Id}' category '{scenario.Category}' must match expected kind '{scenario.Expectations.ExpectedKind}'.");
            }
        }

        return scenarios;
    }

    private static IReadOnlyList<DiagramScenario> ReadFile(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<List<DiagramScenario>>(stream, Options)
            ?? throw new BenchmarkValidationException($"Scenario file is empty: {path}");
    }
}
