using Xunit;

namespace Enzo.Diagrams.Domain.Tests;

public sealed class DocumentationExampleTests
{
    [Theory]
    [InlineData("architecture.enzo")]
    [InlineData("checkout-sequence.enzo")]
    [InlineData("order-approval-bpmn.enzo")]
    [InlineData("order-fulfillment.enzo")]
    public void DocsAssetExample_ParsesAndValidates(string fileName)
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "docs", "assets", fileName));

        var result = DiagramParser.Parse(source);

        Assert.True(result.IsSuccess, FailureMessage(fileName, result));
    }

    [Fact]
    public void ReadmeSequenceExample_ParsesSemanticStructure()
    {
        const string source = """
            sequence Checkout

            actor Customer "Customer"
            participant Web "Web App"
            participant Api "Order API"
            participant Payment "Payment Service"
            participant Db "Database"

            Customer -> Web: Checkout
            Web -> Api: POST /orders
            Api -> Payment: Charge payment
            Payment --> Api: Payment accepted
            Api -> Db: Save order
            Db --> Api: Saved
            Api --> Web: 201 Created
            Web --> Customer: Confirmation
            """;

        var result = SequenceParser.Parse(source);

        Assert.True(result.IsSuccess, FailureMessage("README sequence", result));
        Assert.Equal(5, result.SequenceDiagram!.Participants.Count);
        Assert.Equal(8, result.SequenceDiagram.Messages.Count);
        Assert.Contains(result.SequenceDiagram.Messages, message =>
            message.Kind == SequenceMessageKind.Response
            && message.FromId == "Api"
            && message.ToId == "Web"
            && message.Label == "201 Created");
    }

    private static string FailureMessage(string name, DiagramParseResult result)
    {
        return name + " failed: " + string.Join("; ", result.Errors.Select(error => error.Message)
            .Concat(result.ValidationErrors.Select(error => error.Message)));
    }

    private static string FailureMessage(string name, SequenceParseResult result)
    {
        return name + " failed: " + string.Join("; ", result.Errors.Select(error => error.Message)
            .Concat(result.ValidationErrors.Select(error => error.Message)));
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
