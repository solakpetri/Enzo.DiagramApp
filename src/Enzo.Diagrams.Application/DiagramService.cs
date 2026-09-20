using Enzo.Diagrams.Domain;

namespace Enzo.Diagrams.Application;

public sealed class DiagramService(IDiagramRenderer renderer)
{
    public DiagramValidationResult Validate(string source, DiagramComplexityLimits? limits = null)
    {
        var parseResult = DiagramParser.Parse(source);
        var limitViolation = parseResult.IsSuccess && limits is not null
            ? DiagramComplexity.Check(parseResult, limits)
            : null;

        return new DiagramValidationResult(parseResult, limitViolation);
    }

    public IReadOnlyList<DiagramBatchValidationResult> ValidateBatch(
        IReadOnlyList<DiagramBatchValidationRequest> requests,
        DiagramComplexityLimits? limits = null)
    {
        var results = new List<DiagramBatchValidationResult>(requests.Count);
        foreach (var request in requests)
        {
            results.Add(new DiagramBatchValidationResult(request.Id, Validate(request.Source, limits)));
        }

        return results;
    }

    public DiagramRenderResult Render(
        string source,
        DiagramRenderFormat format,
        DiagramComplexityLimits? limits = null,
        PngRenderOptions? pngOptions = null)
    {
        var validation = Validate(source, limits);
        if (!validation.IsSuccess)
        {
            return DiagramRenderResult.Invalid(validation);
        }

        var svg = renderer.RenderSvg(validation.ParseResult);
        return format == DiagramRenderFormat.Svg
            ? DiagramRenderResult.FromSvg(validation, svg)
            : DiagramRenderResult.FromPng(validation, renderer.RenderPng(svg, pngOptions));
    }
}

public enum DiagramRenderFormat
{
    Svg,
    Png
}

public sealed record DiagramValidationResult(
    DiagramParseResult ParseResult,
    DiagramLimitViolation? LimitViolation)
{
    public bool IsSuccess => ParseResult.IsSuccess && LimitViolation is null;
}

public sealed record DiagramBatchValidationRequest(string Id, string Source);

public sealed record DiagramBatchValidationResult(string Id, DiagramValidationResult Validation);

public sealed record DiagramRenderResult(
    DiagramValidationResult Validation,
    string? Svg,
    byte[]? Png)
{
    public bool IsSuccess => Validation.IsSuccess;

    public static DiagramRenderResult Invalid(DiagramValidationResult validation)
    {
        return new DiagramRenderResult(validation, null, null);
    }

    public static DiagramRenderResult FromSvg(DiagramValidationResult validation, string svg)
    {
        return new DiagramRenderResult(validation, svg, null);
    }

    public static DiagramRenderResult FromPng(DiagramValidationResult validation, byte[] png)
    {
        return new DiagramRenderResult(validation, null, png);
    }
}

public sealed record DiagramComplexityLimits(int MaxDiagramElements, int MaxDiagramConnections);

public sealed record DiagramLimitViolation(string Message, string Code);

public sealed record PngRenderOptions(int MaxWidth, int MaxHeight, int MaxPixels);

internal static class DiagramComplexity
{
    public static DiagramLimitViolation? Check(DiagramParseResult result, DiagramComplexityLimits limits)
    {
        var elementCount = result.Flowchart?.Nodes.Count
            ?? result.SequenceDiagram?.Participants.Count
            ?? result.BpmnDiagram?.Elements.Count
            ?? 0;
        var connectionCount = result.Flowchart?.Edges.Count
            ?? result.SequenceDiagram?.Messages.Count
            ?? result.BpmnDiagram?.Flows.Count
            ?? 0;

        return elementCount > limits.MaxDiagramElements || connectionCount > limits.MaxDiagramConnections
            ? new DiagramLimitViolation("Diagram exceeds configured element or connection limits.", "diagram_too_complex")
            : null;
    }
}
