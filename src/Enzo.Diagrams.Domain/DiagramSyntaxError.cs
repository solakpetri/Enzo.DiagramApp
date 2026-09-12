namespace Enzo.Diagrams.Domain;

public sealed record DiagramSyntaxError(
    int Line,
    int Column,
    string Message);
