namespace Enzo.Diagrams.Language;

public sealed record SyntaxToken(
    TokenKind Kind,
    string Text,
    int Line,
    int Column);
