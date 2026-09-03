using Xunit;

namespace Enzo.Diagrams.Language.Tests;

public sealed class SequenceParserTests
{
    [Fact]
    public void Parse_ValidSequence_ReturnsAst()
    {
        const string source = """
            sequence Checkout

            actor Customer
            participant API
            participant Payment

            Customer -> API: Checkout
            API -> Payment: Charge
            Payment --> API: Success
            API --> Customer: Confirmed
            """;

        var result = SequenceParser.Parse(source);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.SequenceDiagram);
        Assert.Equal("Checkout", result.SequenceDiagram.Name);
        Assert.Equal(3, result.SequenceDiagram.Participants.Count);
        Assert.Equal(4, result.SequenceDiagram.Messages.Count);
        Assert.Equal(new SequenceParticipant(SequenceParticipantKind.Actor, "Customer", 3, 7), result.SequenceDiagram.Participants[0]);
        Assert.Equal(new SequenceMessage("Payment", "API", SequenceMessageKind.Response, "Success", 9, 1), result.SequenceDiagram.Messages[2]);
    }

    [Fact]
    public void Parse_ParticipantDisplayNames_ReturnsAliasesAndDisplayNames()
    {
        const string source = """
            sequence Checkout
            actor Client "Web Client"
            participant Api "Order API"
            Client -> Api: Submit order
            """;

        var result = SequenceParser.Parse(source);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.SequenceDiagram);
        Assert.Equal(new SequenceParticipant(SequenceParticipantKind.Actor, "Client", 2, 7, "Web Client"), result.SequenceDiagram.Participants[0]);
        Assert.Equal(new SequenceParticipant(SequenceParticipantKind.Participant, "Api", 3, 13, "Order API"), result.SequenceDiagram.Participants[1]);
        Assert.Equal(new SequenceMessage("Client", "Api", SequenceMessageKind.Synchronous, "Submit order", 4, 1), result.SequenceDiagram.Messages[0]);
    }

    [Theory]
    [InlineData("POST /orders")]
    [InlineData("200 OK")]
    [InlineData("Order #42")]
    [InlineData("SELECT order by id 42")]
    public void Parse_TechnicalMessageLabel_ReturnsRemainingLineText(string label)
    {
        var result = SequenceParser.Parse($"sequence Checkout\nparticipant Api\nparticipant Db\nApi -> Db: {label}");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.SequenceDiagram);
        Assert.Equal(label, result.SequenceDiagram.Messages[0].Label);
    }

    [Fact]
    public void Parse_MessageMissingArrow_ReturnsStructuralSyntaxError()
    {
        var result = SequenceParser.Parse("sequence Checkout\nparticipant Client\nparticipant Api\nClient Api: POST /orders");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Line == 4 && error.Message.Contains("'->'"));
    }

    [Fact]
    public void Parse_MessageWithoutLabel_ReturnsLineError()
    {
        var result = SequenceParser.Parse("sequence Checkout\nactor Customer\nparticipant API\nCustomer -> API:");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Line == 4 && error.Message.Contains("message label"));
    }

    [Fact]
    public void Parse_UnknownMessageTarget_ReturnsValidationError()
    {
        var result = SequenceParser.Parse("sequence Checkout\nactor Customer\nCustomer -> API: Checkout");

        var error = Assert.Single(result.ValidationErrors);
        Assert.False(result.IsSuccess);
        Assert.Equal(SequenceValidationErrorKind.UnknownMessageTarget, error.Kind);
        Assert.Equal("API", error.ParticipantId);
        Assert.Equal("Customer", error.MessageFromId);
        Assert.Equal("API", error.MessageToId);
    }
}
