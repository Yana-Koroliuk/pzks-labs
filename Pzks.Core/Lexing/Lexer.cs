using Pzks.Core.Diagnostics;

namespace Pzks.Core.Lexing;

public sealed record LexerResult(IReadOnlyList<Token> Tokens, IReadOnlyList<AnalysisError> Errors);

/// <summary>
/// Лексичний аналізатор: перетворює рядок на послідовність лексем (лексичну згортку)
/// і знаходить помилки в записі чисел, імен та невідомі символи.
/// </summary>
public static class Lexer
{
    public static LexerResult Tokenize(string text)
    {
        var tokens = new List<Token>();
        var errors = new List<AnalysisError>();
        int i = 0;

        while (i < text.Length)
        {
            char c = text[i];

            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            if (IsDigit(c) || (c == '.' && i + 1 < text.Length && IsDigit(text[i + 1])))
            {
                int start = i;
                while (i < text.Length && IsNumberPart(text[i]))
                    i++;

                string word = text[start..i];
                tokens.Add(new Token(TokenType.Number, word, start));
                if (NumberLiteral.Validate(word) is { } problem)
                    errors.Add(new AnalysisError(ErrorStage.Lexical, start + problem.Offset, problem.Length, problem.Message));
                continue;
            }

            if (IsIdentifierStart(c))
            {
                int start = i;
                while (i < text.Length && IsIdentifierPart(text[i]))
                    i++;

                tokens.Add(new Token(TokenType.Identifier, text[start..i], start));
                continue;
            }

            var type = c switch
            {
                '+' => TokenType.Plus,
                '-' => TokenType.Minus,
                '*' => TokenType.Star,
                '/' => TokenType.Slash,
                '(' or '[' or '{' => TokenType.LeftParen,
                ')' or ']' or '}' => TokenType.RightParen,
                ',' => TokenType.Comma,
                _ => TokenType.Unknown,
            };

            tokens.Add(new Token(type, c.ToString(), i));
            if (type == TokenType.Unknown)
                errors.Add(new AnalysisError(ErrorStage.Lexical, i, 1, DescribeUnknown(c)));
            i++;
        }

        return new LexerResult(tokens, errors);
    }

    /// <summary>Парна закриваюча дужка для відкриваючої: ( → ), [ → ], { → }.</summary>
    public static string ClosingFor(string opening) => opening switch
    {
        "[" => "]",
        "{" => "}",
        _ => ")",
    };

    private static string DescribeUnknown(char c) => c switch
    {
        '^' or '%' or '!' or '&' or '|' or '=' or '<' or '>' or '?' or ':' =>
            $"Невідомий символ '{c}': такого оператора в граматиці немає (лише + - * /)",
        '.' => "Невідомий символ '.': крапка можлива лише всередині числа",
        ';' => "Невідомий символ ';': аргументи функції розділяються комою",
        _ => $"Невідомий символ '{c}'",
    };

    private static bool IsDigit(char c) => NumberLiteral.IsDigit(c);

    private static bool IsIdentifierStart(char c) => NumberLiteral.IsAsciiLetter(c) || c == '_';

    private static bool IsIdentifierPart(char c) => IsIdentifierStart(c) || IsDigit(c);

    private static bool IsNumberPart(char c) => IsIdentifierPart(c) || c == '.';
}
