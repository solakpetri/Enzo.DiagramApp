using System.Security;
using Enzo.Diagrams.Application;
using Enzo.Diagrams.Domain;
using Enzo.Diagrams.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

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
            using var services = CliComposition.CreateServices();
            var diagrams = services.GetRequiredService<DiagramService>();

            return args[0] switch
            {
                "validate" => await ValidateAsync(args, diagrams, output, error, cancellationToken),
                "render" => await RenderAsync(args, diagrams, output, error, cancellationToken),
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
        DiagramService diagrams,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (args.Length != 2)
        {
            return InvalidArguments(error);
        }

        var source = await ReadFileAsync(args[1], error, cancellationToken);
        if (source is null)
        {
            return FailureExitCode;
        }

        var result = diagrams.Validate(source);
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
        DiagramService diagrams,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (!TryReadRenderArguments(args, out var filePath, out var outputPath, out var format, error))
        {
            return FailureExitCode;
        }

        var source = await ReadFileAsync(filePath, error, cancellationToken);
        if (source is null)
        {
            return FailureExitCode;
        }

        var renderFormat = string.Equals(format, "png", StringComparison.OrdinalIgnoreCase)
            ? DiagramRenderFormat.Png
            : DiagramRenderFormat.Svg;
        DiagramRenderResult result;
        try
        {
            result = diagrams.Render(source, renderFormat);
        }
        catch (DiagramPngRenderException exception)
        {
            error.WriteLine($"Error: {exception.Message}");
            return FailureExitCode;
        }

        if (!result.IsSuccess)
        {
            WriteParseErrors(filePath, result.Validation, error);
            return FailureExitCode;
        }

        try
        {
            if (string.Equals(format, "png", StringComparison.OrdinalIgnoreCase))
            {
                await File.WriteAllBytesAsync(outputPath, result.Png!, cancellationToken);
            }
            else
            {
                await File.WriteAllTextAsync(outputPath, result.Svg!, cancellationToken);
            }
        }
        catch (Exception exception) when (IsFileAccessException(exception))
        {
            error.WriteLine($"Error: could not write '{outputPath}'. {exception.Message}");
            return FailureExitCode;
        }

        output.WriteLine($"Rendered: {outputPath}");
        return SuccessExitCode;
    }

    private static async Task<string?> ReadFileAsync(
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
            return await File.ReadAllTextAsync(filePath, cancellationToken);
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
        DiagramValidationResult result,
        TextWriter error)
    {
        error.WriteLine($"Invalid: {filePath}");

        foreach (var syntaxError in result.ParseResult.Errors)
        {
            error.WriteLine($"line {syntaxError.Line}, column {syntaxError.Column}: {syntaxError.Message}");
        }

        foreach (var validationError in result.ParseResult.ValidationErrors)
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

internal static class CliComposition
{
    public static ServiceProvider CreateServices()
    {
        return new ServiceCollection()
            .AddSingleton<IDiagramRenderer, InfrastructureDiagramRenderer>()
            .AddSingleton<DiagramService>()
            .BuildServiceProvider(validateScopes: true);
    }
}
