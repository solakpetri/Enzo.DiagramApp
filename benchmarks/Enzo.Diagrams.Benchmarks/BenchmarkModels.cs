namespace Enzo.Diagrams.Benchmarks;

public sealed record DiagramScenario(
    string Id,
    string Category,
    string Complexity,
    string Prompt,
    string Enzo,
    string Mermaid,
    DiagramExpectations Expectations);

public sealed record DiagramExpectations(
    string ExpectedKind,
    IReadOnlyList<string> RequiredConcepts,
    int? MinimumNodeCount = null,
    int? MinimumEdgeCount = null,
    int? MinimumDecisionCount = null,
    int? MinimumParticipantCount = null,
    int? MinimumInteractionCount = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? ConceptAliases = null);

public sealed record RepresentationMetrics(
    int Utf8Bytes,
    int Characters,
    int NonEmptyLines,
    int Tokens,
    double? TokensPerElement,
    double? TokensPerConnection);

public sealed record DiagramStructure(
    string Kind,
    IReadOnlyList<string> Elements,
    IReadOnlyList<string> Connections,
    IReadOnlyList<string> BranchLabels)
{
    public int ElementCount => Elements.Count;
    public int ConnectionCount => Connections.Count;
}

public sealed record ScenarioBenchmarkResult(
    string Id,
    string Category,
    string Complexity,
    RepresentationMetrics Enzo,
    RepresentationMetrics Mermaid,
    double TokenDifferencePercent,
    DiagramStructure EnzoStructure,
    DiagramStructure MermaidStructure);

public sealed record BenchmarkRunResult(
    string Encoding,
    IReadOnlyList<ScenarioBenchmarkResult> Scenarios)
{
    public int TotalScenarios => Scenarios.Count;
    public int EnzoTokens => Scenarios.Sum(scenario => scenario.Enzo.Tokens);
    public int MermaidTokens => Scenarios.Sum(scenario => scenario.Mermaid.Tokens);
    public double TokenDifferencePercent => Percentage.Difference(EnzoTokens, MermaidTokens);
}

public sealed class BenchmarkValidationException(string message) : Exception(message);
