namespace Enzo.Diagrams.LlmBenchmarks;

public sealed class PromptStore(string promptsDirectory)
{
    public string GetPrompt(string language, string? expectedKind = null)
    {
        var fileName = language switch
        {
            DiagramLanguages.Enzo when expectedKind == "sequence" => "enzo-sequence-system.txt",
            DiagramLanguages.Enzo => "enzo-system.txt",
            DiagramLanguages.Mermaid => "mermaid-system.txt",
            _ => throw new ArgumentOutOfRangeException(nameof(language), language, "Unknown diagram language.")
        };

        return File.ReadAllText(Path.Combine(promptsDirectory, fileName));
    }

    public string GetRepairPrompt(string language) => language == DiagramLanguages.Enzo
        ? File.ReadAllText(Path.Combine(promptsDirectory, "enzo-repair-system.txt"))
        : GetPrompt(language);

    public PromptAudit CreateAudit() => new(
        GetPrompt(DiagramLanguages.Enzo),
        GetPrompt(DiagramLanguages.Mermaid),
        GenerationPromptBuilder.SharedSemanticTaskTemplate,
        GenerationPromptBuilder.EnzoLanguageSpecificAdditions,
        GenerationPromptBuilder.MermaidLanguageSpecificAdditions,
        LlmMetrics.EquivalentCostComparisonThresholdPercent,
        GetRepairPrompt(DiagramLanguages.Enzo),
        GetPrompt(DiagramLanguages.Enzo, "sequence"));
}
