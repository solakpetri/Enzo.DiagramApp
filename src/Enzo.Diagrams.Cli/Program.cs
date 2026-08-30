using System.Security;
using Enzo.Diagrams.Language;
using Enzo.Diagrams.Rendering;

namespace Enzo.Diagrams.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellationTokenSource.Cancel();
        };

        Console.CancelKeyPress += cancelHandler;

        try
        {
            return await CliApplication.RunAsync(args, Console.Out, Console.Error, cancellationTokenSource.Token);
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
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
        try
        {
            if (args.Length < 2)
            {
                WriteUsage(error);
                return FailureExitCode;
            }

            cancellationToken.ThrowIfCancellationRequested();

            return args[0] switch
            {
                "validate" => await ValidateAsync(args, output, error, cancellationToken),
                "render" => await RenderAsync(args, output, error, cancellationToken),
                _ => InvalidArguments(error)
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            error.WriteLine("Cancelled.");
            return FailureExitCode;
        }
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
        if (!TryReadRenderArguments(args, out var filePath, out var outputPath, out var format, error))
        {
            return FailureExitCode;
        }

        var result = await ParseFileAsync(filePath, error, cancellationToken);
        if (result is null)
        {
            return FailureExitCode;
        }

        if (!result.IsSuccess)
        {
            WriteParseErrors(filePath, result, error);
            return FailureExitCode;
        }

        var svg = DiagramSvgRenderer.Render(result);

        try
        {
            if (string.Equals(format, "png", StringComparison.OrdinalIgnoreCase))
            {
                await File.WriteAllBytesAsync(outputPath, FlowchartPngRenderer.Render(svg), cancellationToken);
            }
            else
            {
                await File.WriteAllTextAsync(outputPath, svg, cancellationToken);
            }
        }
        catch (FlowchartPngRenderException exception)
        {
            error.WriteLine($"Error: {exception.Message}");
            return FailureExitCode;
        }
        catch (Exception exception) when (IsFileAccessException(exception))
        {
            error.WriteLine($"Error: could not write '{outputPath}'. {exception.Message}");
            return FailureExitCode;
        }

        output.WriteLine($"Rendered: {outputPath}");
        return SuccessExitCode;
    }

    private static async Task<DiagramParseResult?> ParseFileAsync(
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
            return DiagramParser.Parse(source);
        }
        catch (Exception exception) when (IsFileAccessException(exception))
        {
            error.WriteLine($"Error: could not read '{filePath}'. {exception.Message}");
            return null;
        }
    }

    private static bool TryReadRenderArguments(
        string[] args,
        out string filePath,
        out string outputPath,
        out string format,
        TextWriter error)
    {
        filePath = args[1];
        format = "svg";
        outputPath = string.Empty;

        for (var index = 2; index < args.Length; index++)
        {
            if (index + 1 >= args.Length)
            {
                WriteUsage(error);
                return false;
            }

            switch (args[index])
            {
                case "--output":
                    outputPath = args[++index];
                    break;
                case "--format":
                    format = args[++index];
                    break;
                default:
                    WriteUsage(error);
                    return false;
            }
        }

        if (!IsSupportedRenderFormat(format))
        {
            error.WriteLine("Error: format must be 'svg' or 'png'.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            outputPath = Path.ChangeExtension(filePath, $".{format.ToLowerInvariant()}");
        }

        return TryNormalizeOutputPath(filePath, outputPath, out outputPath, error);
    }

    private static bool IsSupportedRenderFormat(string format)
    {
        return string.Equals(format, "svg", StringComparison.OrdinalIgnoreCase)
            || string.Equals(format, "png", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryNormalizeOutputPath(
        string sourceFilePath,
        string outputPath,
        out string normalizedOutputPath,
        TextWriter error)
    {
        normalizedOutputPath = string.Empty;

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            error.WriteLine("Error: output path is required.");
            return false;
        }

        try
        {
            var fullOutputPath = Path.GetFullPath(outputPath);
            var sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(sourceFilePath));

            if (string.IsNullOrEmpty(Path.GetFileName(fullOutputPath)))
            {
                error.WriteLine("Error: output path must include a file name.");
                return false;
            }

            if (sourceDirectory is not null && !IsPathInDirectory(fullOutputPath, sourceDirectory))
            {
                error.WriteLine($"Error: output path must be within '{sourceDirectory}'.");
                return false;
            }

            normalizedOutputPath = fullOutputPath;
            return true;
        }
        catch (Exception exception) when (IsFileAccessException(exception))
        {
            error.WriteLine($"Error: invalid output path '{outputPath}'. {exception.Message}");
            return false;
        }
    }

    private static bool IsPathInDirectory(string path, string directory)
    {
        var directoryPrefix = Path.TrimEndingDirectorySeparator(directory) + Path.DirectorySeparatorChar;

        return path.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteParseErrors(
        string filePath,
        DiagramParseResult result,
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
        error.WriteLine("  enzo-diagram render <file> [--format svg|png] [--output <file>]");
    }

    private static bool IsFileAccessException(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or SecurityException;
    }
}
