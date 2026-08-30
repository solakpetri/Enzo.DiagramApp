using Xunit;

namespace Enzo.Diagrams.Language.Tests;

public sealed class DiagramLexerTests
{
    [Fact]
    public void Tokenize_RecognizesFlowchartTokens()
    {
        var result = DiagramLexer.Tokenize("flow Checkout\nBegin -> Validate : yes");

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [
                TokenKind.Flow,
                TokenKind.Identifier,
                TokenKind.EndOfLine,
                TokenKind.Identifier,
                TokenKind.Arrow,
                TokenKind.Identifier,
                TokenKind.Colon,
                TokenKind.Identifier,
                TokenKind.EndOfFile
            ],
            result.Tokens.Select(token => token.Kind).ToArray());
    }

    [Fact]
    public void Tokenize_UnterminatedString_ReturnsLineError()
    {
        var result = DiagramLexer.Tokenize("flow Checkout\ntask Validate \"Validate order");

        var error = Assert.Single(result.Errors);
        Assert.Equal(2, error.Line);
        Assert.Contains("Unterminated", error.Message);

        var token = Assert.Single(result.Tokens, token => token.Kind == TokenKind.String);
        Assert.Equal("Validate order", token.Text);
    }

    [Fact]
    public void Tokenize_UnterminatedStringBeforeNewLine_EmitsPartialStringAndContinues()
    {
        var result = DiagramLexer.Tokenize("flow Checkout\ntask Validate \"Validate order\nend Complete \"Done\"");

        var error = Assert.Single(result.Errors);
        Assert.Equal(2, error.Line);
        Assert.Contains("Unterminated", error.Message);

        Assert.Equal(
            [
                TokenKind.Flow,
                TokenKind.Identifier,
                TokenKind.EndOfLine,
                TokenKind.Task,
                TokenKind.Identifier,
                TokenKind.String,
                TokenKind.EndOfLine,
                TokenKind.End,
                TokenKind.Identifier,
                TokenKind.String,
                TokenKind.EndOfFile
            ],
            result.Tokens.Select(token => token.Kind).ToArray());
    }

    [Fact]
    public void Tokenize_RecognizesSequenceTokens()
    {
        var result = DiagramLexer.Tokenize("sequence Checkout\nactor Customer\nPayment --> API : Success");

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [
                TokenKind.Sequence,
                TokenKind.Identifier,
                TokenKind.EndOfLine,
                TokenKind.Actor,
                TokenKind.Identifier,
                TokenKind.EndOfLine,
                TokenKind.Identifier,
                TokenKind.DashedArrow,
                TokenKind.Identifier,
                TokenKind.Colon,
                TokenKind.Identifier,
                TokenKind.EndOfFile
            ],
            result.Tokens.Select(token => token.Kind).ToArray());
    }
}
