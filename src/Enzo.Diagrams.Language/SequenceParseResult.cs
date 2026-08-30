namespace Enzo.Diagrams.Language;

public sealed record SequenceParseResult(
    SequenceDiagram? SequenceDiagram,
    IReadOnlyList<DiagramSyntaxError> Errors,
    IReadOnlyList<SequenceValidationError> ValidationErrors)
{
    public bool IsSuccess => SequenceDiagram is not null && Errors.Count == 0 && ValidationErrors.Count == 0;
}
