using Pzks.Core.Diagnostics;
using Pzks.Core.Lexing;

namespace Pzks.Tests;

public class LexerTests
{
    [Fact]
    public void Tokenize_SplitsExpressionIntoTokensWithPositions()
    {
        var result = Lexer.Tokenize("sin(x1) + 2.5*_y");

        Assert.Empty(result.Errors);
        Assert.Equal(
            [
                new Token(TokenType.Identifier, "sin", 0),
                new Token(TokenType.LeftParen, "(", 3),
                new Token(TokenType.Identifier, "x1", 4),
                new Token(TokenType.RightParen, ")", 6),
                new Token(TokenType.Plus, "+", 8),
                new Token(TokenType.Number, "2.5", 10),
                new Token(TokenType.Star, "*", 13),
                new Token(TokenType.Identifier, "_y", 14),
            ],
            result.Tokens);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("125")]
    [InlineData("0.71")]
    [InlineData("2048.000")]
    [InlineData(".71")]
    [InlineData("172.")]
    [InlineData("0x6AB4")]
    [InlineData("0XFF")]
    [InlineData("6AB4h")]
    [InlineData("0b0110")]
    [InlineData("0110b")]
    public void Tokenize_AcceptsNumbers(string number)
    {
        var result = Lexer.Tokenize(number);

        Assert.Empty(result.Errors);
        Assert.Equal(TokenType.Number, Assert.Single(result.Tokens).Type);
    }

    [Theory]
    [InlineData("0.72.3", 4)]
    [InlineData("127.0.0.1", 5)]
    [InlineData("1..2", 2)]
    [InlineData("2nb", 0)]
    [InlineData("6var_", 0)]
    [InlineData("6.07ab", 0)]
    [InlineData("0x0R", 3)]
    [InlineData("0x", 0)]
    [InlineData("0b20", 2)]
    [InlineData("20b", 0)]
    [InlineData("0Rh", 1)]
    public void Tokenize_ReportsMalformedNumber(string number, int errorPosition)
    {
        var result = Lexer.Tokenize(number);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorStage.Lexical, error.Stage);
        Assert.Equal(errorPosition, error.Position);
        Assert.Equal(TokenType.Number, Assert.Single(result.Tokens).Type);
    }

    [Theory]
    [InlineData("12", 12.0)]
    [InlineData(".71", 0.71)]
    [InlineData("172.", 172.0)]
    [InlineData("0x6AB4", 27316.0)]
    [InlineData("6AB4h", 27316.0)]
    [InlineData("0b0110", 6.0)]
    [InlineData("0110b", 6.0)]
    public void NumberLiteral_ComputesValue(string text, double expected)
    {
        Assert.Equal(expected, NumberLiteral.Value(text));
    }

    [Fact]
    public void Tokenize_TreatsIdentifierEndingWithH_AsIdentifier()
    {
        Assert.Equal(TokenType.Identifier, Assert.Single(Lexer.Tokenize("ABh").Tokens).Type);
    }

    [Fact]
    public void Tokenize_RecognizesSquareAndCurlyBrackets()
    {
        var result = Lexer.Tokenize("[a]{b}");

        Assert.Empty(result.Errors);
        Assert.Equal(
            [TokenType.LeftParen, TokenType.Identifier, TokenType.RightParen,
             TokenType.LeftParen, TokenType.Identifier, TokenType.RightParen],
            result.Tokens.Select(t => t.Type));
    }

    [Theory]
    [InlineData("a^b", 1)]
    [InlineData("a$b", 1)]
    [InlineData("a;b", 1)]
    [InlineData("a % b", 2)]
    [InlineData("x.y", 1)]
    public void Tokenize_ReportsUnknownSymbol(string expression, int position)
    {
        var result = Lexer.Tokenize(expression);

        var error = Assert.Single(result.Errors);
        Assert.Equal(position, error.Position);
        Assert.Contains(result.Tokens, t => t.Type == TokenType.Unknown && t.Position == position);
    }

    [Fact]
    public void Tokenize_SkipsSpacesAndTabs()
    {
        var result = Lexer.Tokenize(" a\t+  b ");

        Assert.Empty(result.Errors);
        Assert.Equal([1, 3, 6], result.Tokens.Select(t => t.Position));
    }
}
