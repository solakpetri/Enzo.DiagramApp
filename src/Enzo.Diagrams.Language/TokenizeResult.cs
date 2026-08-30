namespace Enzo.Diagrams.Language;

public sealed record TokenizeResult(
    IReadOnlyList<SyntaxToken> Tokens,
    IReadOnlyList<DiagramSyntaxError> Errors)
{
    public bool IsSuccess => Errors.Count == 0;
}
