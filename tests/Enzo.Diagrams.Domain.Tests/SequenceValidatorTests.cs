using Xunit;

namespace Enzo.Diagrams.Domain.Tests;

public sealed class SequenceValidatorTests
{
    [Fact]
    public void Validate_DuplicateParticipantIdentifier_ReturnsValidationError()
    {
        var diagram = new SequenceDiagram(
            "Checkout",
            [
                new SequenceParticipant(SequenceParticipantKind.Actor, "Customer", 2, 7),
                new SequenceParticipant(SequenceParticipantKind.Participant, "Customer", 3, 13)
            ],
            []);

        var error = Assert.Single(SequenceValidator.Validate(diagram));

        Assert.Equal(SequenceValidationErrorKind.DuplicateParticipantIdentifier, error.Kind);
        Assert.Equal("Customer", error.ParticipantId);
        Assert.Contains("already declared", error.Message);
    }

    [Fact]
    public void Validate_UnknownMessageSource_ReturnsValidationError()
    {
        var diagram = new SequenceDiagram(
            "Checkout",
            [new SequenceParticipant(SequenceParticipantKind.Participant, "Api", 2, 13)],
            [new SequenceMessage("Customer", "Api", SequenceMessageKind.Synchronous, "Checkout", 3, 1)]);

        var error = Assert.Single(SequenceValidator.Validate(diagram));

        Assert.Equal(SequenceValidationErrorKind.UnknownMessageSource, error.Kind);
        Assert.Equal("Customer", error.ParticipantId);
        Assert.Equal("Customer", error.MessageFromId);
        Assert.Equal("Api", error.MessageToId);
    }
}
