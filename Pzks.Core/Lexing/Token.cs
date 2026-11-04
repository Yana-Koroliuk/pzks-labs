namespace Pzks.Core.Lexing;

/// <summary>Лексема: тип, текст і позиція першого символу у виразі (з 0).</summary>
public sealed record Token(TokenType Type, string Text, int Position)
{
    public int Length => Text.Length;

    public override string ToString() => $"{Type}('{Text}')@{Position}";
}
