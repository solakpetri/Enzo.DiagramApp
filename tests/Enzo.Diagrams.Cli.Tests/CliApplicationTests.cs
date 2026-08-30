using Enzo.Diagrams.Cli;
using Xunit;

namespace Enzo.Diagrams.Cli.Tests;

public sealed class CliApplicationTests
{
    private const string ValidSource = """
        flow Checkout

        start Begin "Order received"
        task Validate "Validate order"
        end Complete "Complete order"

        Begin -> Validate
        Validate -> Complete
        """;

    [Fact]
    public async Task Validate_ValidDsl_ReturnsSuccess()
    {
        using var workspace = TestWorkspace.Create();
        var filePath = workspace.WriteFile("checkout.enzo", ValidSource);
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await CliApplication.RunAsync(["validate", filePath], output, error);

        Assert.Equal(0, exitCode);
        Assert.Contains($"Valid: {filePath}", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task Validate_InvalidDsl_ReturnsFailure()
    {
        using var workspace = TestWorkspace.Create();
        var filePath = workspace.WriteFile("checkout.enzo", "flow Checkout\nstart Begin \"Start\"");
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await CliApplication.RunAsync(["validate", filePath], output, error);

        Assert.NotEqual(0, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("Invalid:", error.ToString());
        Assert.Contains("must contain an end node", error.ToString());
    }

    [Fact]
    public async Task Validate_CancelledToken_ReturnsFailure()
    {
        using var workspace = TestWorkspace.Create();
        using var cancellationTokenSource = new CancellationTokenSource();
        var filePath = workspace.WriteFile("checkout.enzo", ValidSource);
        var output = new StringWriter();
        var error = new StringWriter();
        cancellationTokenSource.Cancel();

        var exitCode = await CliApplication.RunAsync(["validate", filePath], output, error, cancellationTokenSource.Token);

        Assert.NotEqual(0, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("Cancelled.", error.ToString());
    }

    [Fact]
    public async Task Render_ValidDsl_WritesDefaultSvg()
    {
        using var workspace = TestWorkspace.Create();
        var filePath = workspace.WriteFile("checkout.enzo", ValidSource);
        var outputPath = Path.Combine(workspace.Path, "checkout.svg");
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await CliApplication.RunAsync(["render", filePath], output, error);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(outputPath));
        Assert.Contains($"Rendered: {outputPath}", output.ToString());
        Assert.Contains("<svg", await File.ReadAllTextAsync(outputPath));
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task Render_OutputArgument_WritesRequestedSvg()
    {
        using var workspace = TestWorkspace.Create();
        var filePath = workspace.WriteFile("checkout.enzo", ValidSource);
        var outputPath = Path.Combine(workspace.Path, "diagram.svg");
        var defaultOutputPath = Path.Combine(workspace.Path, "checkout.svg");
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await CliApplication.RunAsync(["render", filePath, "--output", outputPath], output, error);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(outputPath));
        Assert.False(File.Exists(defaultOutputPath));
        Assert.Contains("Validate order", await File.ReadAllTextAsync(outputPath));
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task Render_InvalidDsl_DoesNotCreateOutput()
    {
        using var workspace = TestWorkspace.Create();
        var filePath = workspace.WriteFile("checkout.enzo", "flow Checkout\nstart Begin \"Start\"");
        var outputPath = Path.Combine(workspace.Path, "diagram.svg");
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await CliApplication.RunAsync(["render", filePath, "--output", outputPath], output, error);

        Assert.NotEqual(0, exitCode);
        Assert.False(File.Exists(outputPath));
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("must contain an end node", error.ToString());
    }

    [Fact]
    public async Task Render_OutputOutsideSourceDirectory_ReturnsFailure()
    {
        using var workspace = TestWorkspace.Create();
        var filePath = workspace.WriteFile("checkout.enzo", ValidSource);
        var outputPath = System.IO.Path.Combine(workspace.Path, "..", $"{Guid.NewGuid():N}.svg");
        var normalizedOutputPath = System.IO.Path.GetFullPath(outputPath);
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await CliApplication.RunAsync(["render", filePath, "--output", outputPath], output, error);

        Assert.NotEqual(0, exitCode);
        Assert.False(File.Exists(normalizedOutputPath));
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("output path must be within", error.ToString());
    }

    private sealed class TestWorkspace : IDisposable
    {
        private TestWorkspace(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TestWorkspace Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Enzo.Diagrams.Cli.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);

            return new TestWorkspace(path);
        }

        public string WriteFile(string fileName, string contents)
        {
            var filePath = System.IO.Path.Combine(Path, fileName);
            File.WriteAllText(filePath, contents);

            return filePath;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
