namespace Enzo.Diagrams.LlmBenchmarks;

public sealed class PromptStore(string promptsDirectory)
{
    public string GetPrompt(string language)
    {
        var fileName = language switch
        {
            DiagramLanguages.Enzo => "enzo-system.txt",
            DiagramLanguages.Mermaid => "mermaid-system.txt",
            _ => throw new ArgumentOutOfRangeException(nameof(language), language, "Unknown diagram language.")
        };

        return File.ReadAllText(Path.Combine(promptsDirectory, fileName));
    }
}
