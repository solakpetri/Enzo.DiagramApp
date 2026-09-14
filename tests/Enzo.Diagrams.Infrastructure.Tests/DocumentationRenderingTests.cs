using Enzo.Diagrams.Domain;
using Xunit;

namespace Enzo.Diagrams.Infrastructure.Tests;

public sealed class DocumentationRenderingTests
{
    [Theory]
    [InlineData("checkout-sequence.enzo", "Checkout")]
    [InlineData("order-approval-bpmn.enzo", "Review order")]
    [InlineData("order-fulfillment.enzo", "Order received")]
    public void DocsAssetExample_RendersSvgWithExpectedContent(string fileName, string expectedText)
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "docs", "assets", fileName));
        var result = DiagramParser.Parse(source);

        var svg = DiagramSvgRenderer.Render(result);

        Assert.StartsWith("<?xml", svg);
        Assert.Contains("<svg", svg);
        Assert.Contains(expectedText, svg);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Enzo.Diagrams.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root could not be found.");
    }
}
