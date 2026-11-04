namespace Pzks.Core.Lexing;

public enum TokenType
{
    Number,
    Identifier,
    Plus,
    Minus,
    Star,
    Slash,
    LeftParen,
    RightParen,
    Comma,
    Unknown,
}

public static class TokenTypeExtensions
{
    public static string ToDisplayName(this TokenType type) => type switch
    {
        TokenType.Number => "число",
        TokenType.Identifier => "ідентифікатор",
        TokenType.Plus => "плюс",
        TokenType.Minus => "мінус",
        TokenType.Star => "множення",
        TokenType.Slash => "ділення",
        TokenType.LeftParen => "відкр. дужка",
        TokenType.RightParen => "закр. дужка",
        TokenType.Comma => "кома",
        TokenType.Unknown => "невідомий символ",
        _ => type.ToString(),
    };
}
