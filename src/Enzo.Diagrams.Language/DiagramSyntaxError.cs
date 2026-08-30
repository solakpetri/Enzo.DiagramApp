namespace Enzo.Diagrams.Language;

public sealed record DiagramSyntaxError(
    int Line,
    int Column,
    string Message);
