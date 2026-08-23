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
    }
}
