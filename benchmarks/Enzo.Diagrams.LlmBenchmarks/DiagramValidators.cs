using System.ComponentModel;
using System.Diagnostics;
using Enzo.Diagrams.Language;
using Enzo.Diagrams.Rendering;

namespace Enzo.Diagrams.LlmBenchmarks;

public sealed class EnzoDiagramValidator : IDiagramValidator
{
    public Task<ValidationOutcome> ValidateAsync(string source, CancellationToken cancellationToken)
    {
        var result = DiagramParser.Parse(source);
        if (!result.IsSuccess)
        {
            return Task.FromResult(new ValidationOutcome(false, false, string.Join("; ", result.Errors.Select(e => e.Message).Concat(result.ValidationErrors.Select(e => e.Message)))));
        }

        try
        {
            _ = DiagramSvgRenderer.Render(result);
            return Task.FromResult(new ValidationOutcome(true, true, null));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Task.FromResult(new ValidationOutcome(false, false, ex.Message));
        }
    }
}

public sealed class MermaidCliValidator(string command) : IDiagramValidator
{
    public async Task<ValidationOutcome> ValidateAsync(string source, CancellationToken cancellationToken)
    {
        var input = TempInput(source);
        var output = TempOutput();
        try
        {
            return await RunAsync(["-i", input, "-o", output], cancellationToken);
        }
        finally
        {
            DeleteIfExists(input);
            DeleteIfExists(output);
        }
    }

    public Task<ValidationOutcome> EnsureAvailableAsync(CancellationToken cancellationToken) => RunAsync(["--version"], cancellationToken);

    private async Task<ValidationOutcome> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        try
        {
            using var process = Start(arguments);
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(60));
                var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
                var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
                await process.WaitForExitAsync(timeout.Token);
                var output = await outputTask;
                var error = await errorTask;
                var ok = process.ExitCode == 0;
                return new ValidationOutcome(ok, ok, ok ? null : FirstText(error, output));
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                return new ValidationOutcome(false, false, "Mermaid CLI timed out.");
            }
        }
        catch (Win32Exception ex)
        {
            return new ValidationOutcome(false, false, $"Mermaid CLI was not found. Install it with npm install -g @mermaid-js/mermaid-cli and ensure {command} is available on PATH. {ex.Message}");
        }
    }

    private Process Start(IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo(command) { RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start Mermaid CLI.");
    }

    private static string TempInput(string source)
    {
        var path = Path.Combine(Path.GetTempPath(), $"enzo-llm-mermaid-{Guid.NewGuid():N}.mmd");
        File.WriteAllText(path, source);
        return path;
    }

    private static string TempOutput() => Path.Combine(Path.GetTempPath(), $"enzo-llm-mermaid-{Guid.NewGuid():N}.svg");

    private static string FirstText(string first, string second) => string.IsNullOrWhiteSpace(first) ? second.Trim() : first.Trim();

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

public static class MermaidExecutableResolver
{
    public static string GetDefaultExecutable() => GetDefaultExecutable(OperatingSystem.IsWindows());

    public static string GetDefaultExecutable(bool isWindows) => isWindows ? "mmdc.cmd" : "mmdc";
}
