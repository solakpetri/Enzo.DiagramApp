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
