using System.Globalization;

namespace Pzks.Core.Lexing;

public enum NumberFormat
{
    Decimal,
    Hexadecimal,
    Binary,
}

/// <summary>Помилка в записі числа: зсув від початку числа, довжина фрагмента, опис.</summary>
public sealed record NumberProblem(int Offset, int Length, string Message);

/// <summary>
/// Формати числових констант:
/// <code>
/// десяткові      12   0.71   .71 (= 0.71)   172. (= 172.0)
/// шістнадцяткові 0x6AB4   6AB4h    (має починатися з цифри: ABh — це ім'я змінної)
/// двійкові       0b0110   0110b
/// </code>
/// </summary>
public static class NumberLiteral
{
    public static NumberFormat FormatOf(string text)
    {
        if (HasHexPrefix(text) || EndsWith(text, 'h'))
            return NumberFormat.Hexadecimal;
        if (HasBinaryPrefix(text) || EndsWith(text, 'b'))
            return NumberFormat.Binary;
        return NumberFormat.Decimal;
    }

    /// <summary>Значення коректної константи.</summary>
    public static double Value(string text) => FormatOf(text) switch
    {
        NumberFormat.Hexadecimal => Accumulate(HexDigits(text), 16),
        NumberFormat.Binary => Accumulate(BinaryDigits(text), 2),
        _ => double.Parse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture),
    };

    /// <summary>Перевіряє запис числа. Повертає першу знайдену помилку або null.</summary>
    public static NumberProblem? Validate(string word)
    {
        if (word.Contains('.'))
            return ValidateDecimalFraction(word);

        if (word.All(IsDigit))
            return null;

        if (HasHexPrefix(word))
            return ValidateDigits(word, 2, word.Length, IsHexDigit, "шістнадцяткова", "0-9, A-F");

        if (EndsWith(word, 'h') && word[..^1].All(IsAsciiLetterOrDigit))
            return ValidateDigits(word, 0, word.Length - 1, IsHexDigit, "шістнадцяткова", "0-9, A-F");

        if (HasBinaryPrefix(word))
            return ValidateDigits(word, 2, word.Length, IsBinaryDigit, "двійкова", "0 або 1");

        if (EndsWith(word, 'b') && word[..^1].All(IsDigit))
            return ValidateDigits(word, 0, word.Length - 1, IsBinaryDigit, "двійкова", "0 або 1");

        return LetterInNumber(word);
    }

    private static NumberProblem? ValidateDecimalFraction(string word)
    {
        if (word.Any(ch => IsAsciiLetter(ch) || ch == '_'))
            return LetterInNumber(word);

        int firstDot = word.IndexOf('.');
        int secondDot = word.IndexOf('.', firstDot + 1);
        if (secondDot >= 0)
            return new NumberProblem(secondDot, 1, $"Зайва десяткова крапка в числі '{word}'");

        return null;
    }

    private static NumberProblem? ValidateDigits(
        string word, int from, int to, Func<char, bool> isDigit, string kind, string allowed)
    {
        if (from == to)
            return new NumberProblem(0, word.Length, $"Некоректна {kind} константа '{word}': немає жодної цифри");

        for (int i = from; i < to; i++)
        {
            if (!isDigit(word[i]))
            {
                return new NumberProblem(i, 1,
                    $"Некоректна {kind} константа '{word}': '{word[i]}' не є допустимою цифрою ({allowed})");
            }
        }
        return null;
    }

    private static NumberProblem LetterInNumber(string word)
    {
        char bad = word.First(ch => IsAsciiLetter(ch) || ch == '_');
        return new NumberProblem(0, word.Length,
            $"Некоректне число '{word}': символ '{bad}' не може бути частиною числа " +
            "(ім'я не може починатися з цифри, неявного множення немає)");
    }

    private static string HexDigits(string text) => HasHexPrefix(text) ? text[2..] : text[..^1];

    private static string BinaryDigits(string text) => HasBinaryPrefix(text) ? text[2..] : text[..^1];

    private static double Accumulate(string digits, int radix) =>
        digits.Aggregate(0.0, (value, ch) => value * radix + Convert.ToInt32(ch.ToString(), 16));

    private static bool HasHexPrefix(string s) => s.Length >= 2 && s[0] == '0' && s[1] is 'x' or 'X';

    private static bool HasBinaryPrefix(string s) =>
        s.Length >= 3 && s[0] == '0' && s[1] is 'b' or 'B' && IsDigit(s[2]);

    private static bool EndsWith(string s, char suffix) => s.Length > 1 && char.ToLowerInvariant(s[^1]) == suffix;

    internal static bool IsDigit(char c) => c is >= '0' and <= '9';

    internal static bool IsAsciiLetter(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z';

    private static bool IsAsciiLetterOrDigit(char c) => IsAsciiLetter(c) || IsDigit(c);

    private static bool IsHexDigit(char c) => IsDigit(c) || c is >= 'a' and <= 'f' or >= 'A' and <= 'F';

    private static bool IsBinaryDigit(char c) => c is '0' or '1';
}
