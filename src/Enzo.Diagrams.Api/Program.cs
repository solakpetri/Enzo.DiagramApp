using System.Text.Json;
using Enzo.Diagrams.Api;
using Enzo.Diagrams.Language;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();

var app = builder.Build();

app.MapPost("/v1/validate", async (HttpRequest httpRequest, CancellationToken cancellationToken) =>
{
    var (request, readError) = await ReadRequestAsync<ValidateDiagramRequest>(httpRequest, cancellationToken);
    if (readError is not null)
    {
        return readError;
    }

    if (!TryGetSource(request!.Source, out var source, out var inputError))
    {
        return inputError;
    }

    var result = FlowchartParser.Parse(source);
    return result.IsSuccess
        ? Results.Ok(new ValidateDiagramResponse(true))
        : DiagramProblem(result);
})
.Accepts<ValidateDiagramRequest>("application/json")
.Produces<ValidateDiagramResponse>()
.ProducesProblem(StatusCodes.Status400BadRequest);

app.Run();

static async Task<(T? Request, IResult? Error)> ReadRequestAsync<T>(
    HttpRequest httpRequest,
    CancellationToken cancellationToken)
{
    try
    {
        var request = await httpRequest.ReadFromJsonAsync<T>(cancellationToken);
        return request is null
            ? (default, InvalidRequestProblem("Request body is required.", [
                new DiagramProblemError("request", null, null, "Request body is required.", "required")
            ]))
            : (request, null);
    }
    catch (JsonException)
    {
        return (default, InvalidRequestProblem("Request body must be valid JSON.", [
            new DiagramProblemError("request", null, null, "Request body must be valid JSON.", "malformed_json")
        ]));
    }
    catch (BadHttpRequestException)
    {
        return (default, InvalidRequestProblem("Request body must be valid JSON.", [
            new DiagramProblemError("request", null, null, "Request body must be valid JSON.", "malformed_json")
        ]));
    }
}

static bool TryGetSource(string? value, out string source, out IResult error)
{
    source = value ?? string.Empty;
    if (!string.IsNullOrWhiteSpace(value))
    {
        error = Results.Empty;
        return true;
    }

    error = InvalidRequestProblem("Source is required.", [
        new DiagramProblemError("request", null, null, "Source is required.", "required")
    ]);
    return false;
}

static IResult DiagramProblem(FlowchartParseResult result)
{
    return Results.Problem(
        title: "Diagram DSL is invalid.",
        detail: "The request source contains syntax or validation errors.",
        statusCode: StatusCodes.Status400BadRequest,
        extensions: new Dictionary<string, object?>
        {
            ["errors"] = ToDiagramErrors(result)
        });
}

static IResult InvalidRequestProblem(string detail, IReadOnlyList<DiagramProblemError> errors)
{
    return Results.Problem(
        title: "Invalid request.",
        detail: detail,
        statusCode: StatusCodes.Status400BadRequest,
        extensions: new Dictionary<string, object?>
        {
            ["errors"] = errors
        });
}

static IReadOnlyList<DiagramProblemError> ToDiagramErrors(FlowchartParseResult result)
{
    var errors = new List<DiagramProblemError>();

    errors.AddRange(result.Errors.Select(error =>
        new DiagramProblemError("syntax", error.Line, error.Column, error.Message)));

    errors.AddRange(result.ValidationErrors.Select(error =>
        new DiagramProblemError("validation", error.Line, error.Column, error.Message, error.Kind.ToString())));

    return errors;
}

public partial class Program;
