using Enzo.Diagrams.Language;
using Enzo.Diagrams.Rendering;

namespace Enzo.Diagrams.Cli;

public static class Program
{
    public static Task<int> Main(string[] args)
    {
        return CliApplication.RunAsync(args, Console.Out, Console.Error);
    }
}

public static class CliApplication
{
    private const int SuccessExitCode = 0;
    private const int FailureExitCode = 1;

    public static async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        if (args.Length < 2)
        {
            WriteUsage(error);
            return FailureExitCode;
        }

        return args[0] switch
        {
            "validate" => await ValidateAsync(args, output, error, cancellationToken),
            "render" => await RenderAsync(args, output, error, cancellationToken),
            _ => InvalidArguments(error)
        };
    }

    private static async Task<int> ValidateAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (args.Length != 2)
        {
            return InvalidArguments(error);
        }

        var result = await ParseFileAsync(args[1], error, cancellationToken);
        if (result is null)
        {
            return FailureExitCode;
        }

        if (!result.IsSuccess)
        {
            WriteParseErrors(args[1], result, error);
            return FailureExitCode;
        }

        output.WriteLine($"Valid: {args[1]}");
        return SuccessExitCode;
    }

    private static async Task<int> RenderAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (!TryReadRenderArguments(args, out var filePath, out var outputPath, error))
        {
            return FailureExitCode;
        }

        var result = await ParseFileAsync(filePath, error, cancellationToken);
        if (result is null)
        {
            return FailureExitCode;
        }

        if (!result.IsSuccess || result.Flowchart is null)
        {
            WriteParseErrors(filePath, result, error);
            return FailureExitCode;
        }

        var layout = FlowchartLayoutEngine.Layout(result.Flowchart);
        var svg = FlowchartSvgRenderer.Render(layout);

        try
        {
            await File.WriteAllTextAsync(outputPath, svg, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            error.WriteLine($"Error: could not write '{outputPath}'. {exception.Message}");
            return FailureExitCode;
        }

        output.WriteLine($"Rendered: {outputPath}");
        return SuccessExitCode;
    }

    private static async Task<FlowchartParseResult?> ParseFileAsync(
        string filePath,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            error.WriteLine($"Error: file not found: {filePath}");
            return null;
        }

        try
        {
            var source = await File.ReadAllTextAsync(filePath, cancellationToken);
            return FlowchartParser.Parse(source);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            error.WriteLine($"Error: could not read '{filePath}'. {exception.Message}");
            return null;
        }
    }

    private static bool TryReadRenderArguments(
        string[] args,
        out string filePath,
        out string outputPath,
        TextWriter error)
    {
        filePath = args[1];
        outputPath = Path.ChangeExtension(filePath, ".svg");

        for (var index = 2; index < args.Length; index++)
        {
            if (args[index] != "--output" || index + 1 >= args.Length)
            {
                WriteUsage(error);
                return false;
            }

            outputPath = args[++index];
        }

        return true;
    }

    private static void WriteParseErrors(
        string filePath,
        FlowchartParseResult result,
        TextWriter error)
    {
        error.WriteLine($"Invalid: {filePath}");

        foreach (var syntaxError in result.Errors)
        {
            error.WriteLine($"line {syntaxError.Line}, column {syntaxError.Column}: {syntaxError.Message}");
        }

        foreach (var validationError in result.ValidationErrors)
        {
            error.WriteLine($"line {validationError.Line}, column {validationError.Column}: {validationError.Message}");
        }
    }

    private static int InvalidArguments(TextWriter error)
    {
        WriteUsage(error);
        return FailureExitCode;
    }

    private static void WriteUsage(TextWriter error)
    {
        error.WriteLine("Usage:");
        error.WriteLine("  enzo-diagram validate <file>");
        error.WriteLine("  enzo-diagram render <file> [--output <file>]");
    }
}
