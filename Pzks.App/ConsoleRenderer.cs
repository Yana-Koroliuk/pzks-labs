using System.Globalization;
using Pzks.Core;
using Pzks.Core.Ast;
using Pzks.Core.Correction;
using Pzks.Core.Diagnostics;
using Pzks.Core.Lexing;
using Pzks.Core.Parallelization;
using Pzks.Core.Parsing;

namespace Pzks.App;

public static class ConsoleRenderer
{
    private const string PosLabel = "[pos]   ";
    private const string ExprLabel = "[expr]  ";
    private const string PointLabel = "[point] ";

    public const ConsoleColor LexicalColor = ConsoleColor.Magenta;
    public const ConsoleColor SyntaxColor = ConsoleColor.Red;

    public static ConsoleColor ColorOf(ErrorStage stage) =>
        stage == ErrorStage.Lexical ? LexicalColor : SyntaxColor;

    /// <summary>
    /// Вираз з лінійкою позицій, підсвіченими помилками, вказівниками ^ і списком помилок.
    /// Лексичні помилки виводяться фіолетовим, синтаксичні — червоним, і на виразі, і в тексті.
    /// </summary>
    public static void PrintErrors(AnalysisResult result)
    {
        string expression = result.Expression.Replace('\t', ' ');
        int width = Math.Max(expression.Length, result.Errors.Select(e => e.Position + 1).DefaultIfEmpty(0).Max());

        Console.WriteLine(PosLabel + string.Concat(Enumerable.Range(0, width).Select(i => (char)('0' + i % 10))));

        // Якщо дві помилки зачіпають один символ, він отримує колір першої з них у списку.
        var highlight = new ConsoleColor?[width];
        var pointer = new ConsoleColor?[width];
        foreach (var error in result.Errors)
        {
            var color = ColorOf(error.Stage);
            pointer[error.Position] ??= color;
            for (int i = error.Position; i < Math.Min(width, error.Position + Math.Max(1, error.Length)); i++)
                highlight[i] ??= color;
        }

        Console.Write(ExprLabel);
        for (int i = 0; i < expression.Length; i++)
        {
            if (highlight[i] is { } color)
                Write(expression[i].ToString(), color);
            else
                Console.Write(expression[i]);
        }
        Console.WriteLine();

        Console.Write(PointLabel);
        int lastPointer = Array.FindLastIndex(pointer, c => c is not null);
        for (int i = 0; i <= lastPointer; i++)
        {
            if (pointer[i] is { } color)
                Write("^", color);
            else
                Console.Write(' ');
        }
        Console.WriteLine();

        if (result.IsValid)
        {
            WriteLine("Помилок не знайдено — вираз коректний.", ConsoleColor.Green);
            return;
        }

        int lexical = result.Errors.Count(e => e.Stage == ErrorStage.Lexical);
        Console.Write($"Знайдено помилок: {result.Errors.Count} (");
        Write($"лексичних: {lexical}", LexicalColor);
        Console.Write(", ");
        Write($"синтаксичних: {result.Errors.Count - lexical}", SyntaxColor);
        Console.WriteLine(")");

        for (int i = 0; i < result.Errors.Count; i++)
        {
            var error = result.Errors[i];
            string stage = error.Stage == ErrorStage.Lexical ? "лекс." : "синт.";
            WriteLine($"{i + 1,3}. [{stage}] позиція {error.Position}: {error.Message}", ColorOf(error.Stage));
        }
    }

    /// <summary>Результат автовиправлення: список змін, виправлений вираз і його повторна перевірка.</summary>
    public static void PrintCorrection(CorrectionResult? correction)
    {
        WriteLine("Автовиправлення:", ConsoleColor.Cyan);
        if (correction is null)
        {
            WriteLine("Виправити не вдалося: після видалення помилкових лексем від виразу нічого не лишилося.", ConsoleColor.Red);
            return;
        }

        for (int i = 0; i < correction.Corrections.Count; i++)
        {
            var fix = correction.Corrections[i];
            Console.WriteLine($"{i + 1,3}. позиція {fix.Position}: {fix.Description}");
        }

        Console.Write("Виправлений вираз: ");
        WriteLine(correction.Expression, correction.Analysis.IsValid ? ConsoleColor.Green : ConsoleColor.Red);
        if (!correction.Analysis.IsValid)
        {
            WriteLine($"Повторна перевірка знайшла помилок: {correction.Analysis.Errors.Count}", ConsoleColor.Red);
            return;
        }

        WriteLine("Повторна перевірка: помилок немає.", ConsoleColor.Green);
        if (correction.Analysis.Tree is { } tree)
            PrintTree(tree);
    }

    /// <summary>Таблиця лексем — лексична згортка виразу.</summary>
    public static void PrintTokens(IReadOnlyList<Token> tokens)
    {
        WriteLine("Лексична згортка:", ConsoleColor.Cyan);
        Console.WriteLine($"{"№",3}  {"Тип",-18} {"Текст",-12} {"Позиція",-8} Примітка");
        for (int i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            Console.WriteLine($"{i + 1,3}  {token.Type.ToDisplayName(),-18} {token.Text,-12} {token.Position,-8} {NumberNote(token)}");
        }
    }

    private static string NumberNote(Token token)
    {
        if (token.Type != TokenType.Number || NumberLiteral.Validate(token.Text) is not null)
            return "";

        string value = NumberLiteral.Value(token.Text).ToString(CultureInfo.InvariantCulture);
        return NumberLiteral.FormatOf(token.Text) switch
        {
            NumberFormat.Hexadecimal => $"шістнадцяткове = {value}",
            NumberFormat.Binary => $"двійкове = {value}",
            _ when token.Text.StartsWith('.') || token.Text.EndsWith('.') => $"неповний дріб = {value}",
            _ => "",
        };
    }

    /// <summary>Покрокова робота кінцевого автомата.</summary>
    public static void PrintTrace(IReadOnlyList<StateTransition> trace)
    {
        WriteLine("Робота кінцевого автомата:", ConsoleColor.Cyan);
        Console.WriteLine($"{"Лексема",-10} {"Стан до",-28} {"Стан після",-28} Примітка");
        foreach (var step in trace)
        {
            string token = step.Token?.Text ?? "<кінець>";
            string line = $"{token,-10} {step.From.ToDisplayName(),-28} {step.To.ToDisplayName(),-28} ";
            if (step.Note is null)
            {
                Console.WriteLine(line);
            }
            else
            {
                Console.Write(line);
                WriteLine(step.Note, SyntaxColor);
            }
        }
    }

    public static void PrintTree(ExprNode tree)
    {
        WriteLine("Дерево виразу:", ConsoleColor.Cyan);
        foreach (string line in ExprPrinter.ToTreeLines(tree))
            Console.WriteLine("  " + line);
        Console.WriteLine($"Висота дерева: {ExprPrinter.Height(tree)}");
        Console.WriteLine($"Вираз за деревом: {ExprPrinter.ToInfix(tree)}");
    }

    /// <summary>ЛР2: перетворення виразу і дерево паралельної форми.</summary>
    public static void PrintParallel(ParallelResult result)
    {
        Console.WriteLine($"Вираз: {result.Analysis.Expression}");
        if (result.Source is not { } source)
        {
            PrintErrors(result.Analysis);
            WriteLine("Вираз не пройшов перевірку ЛР1 — дерево паралельної форми не будується. "
                + "Автовиправлення показує пункт 1 меню.", SyntaxColor);
            return;
        }

        WriteLine("Перевірка ЛР1: помилок немає.", ConsoleColor.Green);
        Console.WriteLine($"Послідовне дерево (ЛР1): ярусів {TreeLevels.Tiers(source)}, "
            + $"операцій {TreeLevels.OperationCount(source)}, ширина {TreeLevels.Width(source)}");

        Console.WriteLine();
        WriteLine("Перетворення:", ConsoleColor.Cyan);
        if (result.Steps.Count == 0)
            Console.WriteLine("  немає");
        for (int i = 0; i < result.Steps.Count; i++)
        {
            var step = result.Steps[i];
            Console.Write($"{i + 1,3}. ");
            Write($"[{StepName(step.Kind)}]", StepColor(step.Kind));
            Console.WriteLine($" {step.Description}");
        }

        if (result.Errors.Count > 0)
        {
            Console.WriteLine();
            WriteLine($"Недопустимі операції: {result.Errors.Count}", SyntaxColor);
            for (int i = 0; i < result.Errors.Count; i++)
            {
                var error = result.Errors[i];
                WriteLine($"{i + 1,3}. терм «{error.Term}»: {error.Message}", SyntaxColor);
            }
            WriteLine("Дерево паралельної форми не будується, доки вираз містить ділення на 0.", SyntaxColor);
            return;
        }

        var tree = result.Tree!;
        Console.WriteLine();
        Console.WriteLine($"Після розкриття дужок і спрощення: {ExprPrinter.ToInfix(result.Simplified!)}");
        Console.Write("Паралельна форма: ");
        WriteLine(ExprPrinter.ToGroupedInfix(tree), ConsoleColor.Green);

        if (TreeLevels.Tiers(tree) == 0)
        {
            Console.WriteLine();
            WriteLine("Вираз обчислено повністю: операцій не лишилося, дерево складається з одного операнда.", ConsoleColor.Green);
            Console.WriteLine($"Ярусів: {TreeLevels.Tiers(source)} → 0, операцій: {TreeLevels.OperationCount(source)} → 0");
            return;
        }

        Console.WriteLine();
        WriteLine("Дерево паралельної форми (операнди зверху, яруси вниз):", ConsoleColor.Cyan);
        foreach (string line in TierDiagram.Lines(tree))
            Console.WriteLine("  " + line);

        Console.WriteLine();
        WriteLine("Операції по ярусах ([n] — результат операції n):", ConsoleColor.Cyan);
        foreach (string line in TierDiagram.OperationLines(tree))
            Console.WriteLine("  " + line);

        Console.WriteLine();
        WriteLine("Дерево (корінь зверху):", ConsoleColor.Cyan);
        foreach (string line in ExprPrinter.ToTreeLines(tree))
            Console.WriteLine("  " + line);

        Console.WriteLine();
        Console.Write("Ярусів: ");
        WriteLine($"{TreeLevels.Tiers(source)} → {TreeLevels.Tiers(tree)}", ConsoleColor.Green);
        Console.WriteLine($"Операцій: {TreeLevels.OperationCount(source)} → {TreeLevels.OperationCount(tree)}");
        Console.WriteLine($"Найбільша ширина ярусу: {TreeLevels.Width(source)} → {TreeLevels.Width(tree)}");
    }

    private static string StepName(StepKind kind) => kind switch
    {
        StepKind.Brackets => "дужки",
        StepKind.Simplification => "спрощення",
        StepKind.Constants => "константи",
        _ => "перегрупування",
    };

    private static ConsoleColor StepColor(StepKind kind) => kind switch
    {
        StepKind.Brackets => ConsoleColor.Yellow,
        StepKind.Simplification => ConsoleColor.Blue,
        StepKind.Constants => ConsoleColor.Blue,
        _ => ConsoleColor.Green,
    };

    public static void Write(string text, ConsoleColor color)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Write(text);
        Console.ForegroundColor = previous;
    }

    public static void WriteLine(string text, ConsoleColor color)
    {
        Write(text, color);
        Console.WriteLine();
    }
}
