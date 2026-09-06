namespace Enzo.Diagrams.Mcp;

public sealed record EnzoStatus(string Service, string Version, IReadOnlyList<string> Capabilities);
