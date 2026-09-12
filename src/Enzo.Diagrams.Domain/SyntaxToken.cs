namespace Enzo.Diagrams.Domain;

public sealed record SyntaxToken(
    TokenKind Kind,
    string Text,
    int Line,
    int Column);
