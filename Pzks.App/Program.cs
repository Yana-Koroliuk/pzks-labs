using System.Text;
using Pzks.App;
using Pzks.Core;
using Pzks.Core.Correction;
using Pzks.Core.Samples;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

if (args.Length > 0)
{
    AnalyzeInDetail(string.Join(' ', args));
    return;
}

while (true)
{
    Console.WriteLine();
    ConsoleRenderer.WriteLine("=== ПЗКС. ЛР1: лексичний і синтаксичний аналіз арифметичного виразу ===", ConsoleColor.Cyan);
    Console.WriteLine("1 — Ввести вираз");
    Console.WriteLine("2 — Загальні тести з пам'ятки (8)");
    Console.WriteLine("3 — Граматика");
    Console.WriteLine("0 — Вихід");
    Console.Write("> ");

    string? choice = Console.ReadLine();
    if (choice is null)
        return;

    switch (choice.Trim())
    {
        case "1":
            Console.Write("Вираз: ");
            string? expression = Console.ReadLine();
            if (expression is not null)
                AnalyzeInDetail(expression);
            break;
        case "2":
            RunSamples(MemoExpressions.Lab1General);
            break;
        case "3":
            Console.WriteLine(GrammarHelp.Text);
            break;
        case "0":
            return;
        default:
            Console.WriteLine("Невідомий пункт меню.");
            break;
    }
}

static void AnalyzeInDetail(string expression)
{
    var result = ExpressionAnalyzer.Analyze(expression);
    Console.WriteLine();
    ConsoleRenderer.PrintTokens(result.Tokens);
    Console.WriteLine();
    ConsoleRenderer.PrintTrace(result.Trace);
    Console.WriteLine();
    ConsoleRenderer.PrintErrors(result);
    Console.WriteLine();
    if (result.Tree is not null)
        ConsoleRenderer.PrintTree(result.Tree);
    else
        ConsoleRenderer.PrintCorrection(AutoCorrector.Correct(expression));
}

static void RunSamples(IEnumerable<SampleExpression> samples)
{
    foreach (var sample in samples)
    {
        Console.WriteLine();
        string title = string.IsNullOrEmpty(sample.Description) ? $"Тест {sample.Id}" : $"Тест {sample.Id}: {sample.Description}";
        ConsoleRenderer.WriteLine(title, ConsoleColor.Cyan);
        ConsoleRenderer.PrintErrors(ExpressionAnalyzer.Analyze(sample.Expression));
    }
}
