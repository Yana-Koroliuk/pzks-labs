using System.Text;
using Pzks.App;
using Pzks.Core;
using Pzks.Core.Correction;
using Pzks.Core.Parallelization;
using Pzks.Core.Samples;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

if (args is ["--lr2", .. var rest])
{
    if (rest.Length > 0)
        BuildParallel(string.Join(' ', rest));
    else
        RunParallelSamples(MemoExpressions.Lab2);
    return;
}

if (args.Length > 0)
{
    AnalyzeInDetail(string.Join(' ', args));
    return;
}

while (true)
{
    Console.WriteLine();
    ConsoleRenderer.WriteLine("=== ПЗКС. Аналіз і розпаралелювання арифметичного виразу ===", ConsoleColor.Cyan);
    Console.WriteLine("ЛР1:");
    Console.WriteLine("  1 — Ввести вираз (лексичний і синтаксичний аналіз)");
    Console.WriteLine("  2 — Загальні тести з пам'ятки (8)");
    Console.WriteLine("  3 — Граматика");
    Console.WriteLine("ЛР2:");
    Console.WriteLine("  4 — Ввести вираз (дерево паралельної форми)");
    Console.WriteLine("  5 — Тести ЛР2 з пам'ятки (М1–М4, P1–P2, E1–E3, S1–S2)");
    Console.WriteLine("0 — Вихід");
    Console.Write("> ");

    string? choice = Console.ReadLine();
    if (choice is null)
        return;

    switch (choice.Trim())
    {
        case "1":
            if (ReadExpression() is { } expression)
                AnalyzeInDetail(expression);
            break;
        case "2":
            RunSamples(MemoExpressions.Lab1General);
            break;
        case "3":
            Console.WriteLine(GrammarHelp.Text);
            break;
        case "4":
            if (ReadExpression() is { } parallelExpression)
                BuildParallel(parallelExpression);
            break;
        case "5":
            RunParallelSamples(MemoExpressions.Lab2);
            break;
        case "0":
            return;
        default:
            Console.WriteLine("Невідомий пункт меню.");
            break;
    }
}

static string? ReadExpression()
{
    Console.Write("Вираз: ");
    return Console.ReadLine();
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
        ConsoleRenderer.WriteLine(Title(sample), ConsoleColor.Cyan);
        ConsoleRenderer.PrintErrors(ExpressionAnalyzer.Analyze(sample.Expression));
    }
}

static void BuildParallel(string expression)
{
    Console.WriteLine();
    ConsoleRenderer.PrintParallel(ParallelTreeBuilder.Build(expression));
}

static void RunParallelSamples(IEnumerable<SampleExpression> samples)
{
    foreach (var sample in samples)
    {
        Console.WriteLine();
        ConsoleRenderer.WriteLine(new string('═', 70), ConsoleColor.DarkGray);
        ConsoleRenderer.WriteLine(Title(sample), ConsoleColor.Cyan);
        ConsoleRenderer.PrintParallel(ParallelTreeBuilder.Build(sample.Expression));
    }
}

static string Title(SampleExpression sample) =>
    string.IsNullOrEmpty(sample.Description) ? $"Тест {sample.Id}" : $"Тест {sample.Id}: {sample.Description}";
