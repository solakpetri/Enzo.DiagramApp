namespace Enzo.Diagrams.Language;

public sealed record DiagramParseResult(
    Flowchart? Flowchart,
    SequenceDiagram? SequenceDiagram,
    IReadOnlyList<DiagramSyntaxError> Errors,
    IReadOnlyList<DiagramValidationError> ValidationErrors)
{
    public bool IsSuccess => (Flowchart is not null || SequenceDiagram is not null)
        && Errors.Count == 0
        && ValidationErrors.Count == 0;
}

public sealed record DiagramValidationError(
    string Kind,
    int Line,
    int Column,
    string Message);
