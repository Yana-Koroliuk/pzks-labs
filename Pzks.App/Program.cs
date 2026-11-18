using System.Text;
using Pzks.App;
using Pzks.Core;
using Pzks.Core.Correction;
using Pzks.Core.Factoring;
using Pzks.Core.Optimization;
using Pzks.Core.Parallelization;
using Pzks.Core.Samples;
using Pzks.Core.Simulation;

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

if (args is ["--lr3", .. var lr3Rest])
{
    if (lr3Rest.Length > 0)
        BuildForms(string.Join(' ', lr3Rest));
    else
        RunFactoringSamples(MemoExpressions.Associative);
    return;
}

if (args is ["--lr4", .. var lr4Rest])
{
    var lr4Laws = Laws.Distributive | Laws.Associative;
    if (lr4Rest is ["--d", .. var dRest])
        (lr4Laws, lr4Rest) = (Laws.Distributive, dRest);
    if (lr4Rest.Length > 0)
        BuildForms(string.Join(' ', lr4Rest), lr4Laws);
    else
        RunFactoringSamples(MemoExpressions.Lab4, lr4Laws);
    return;
}

if (args is ["--lr5", .. var lr5Rest])
{
    var (vliw, expressionArgs) = ParseVliwOptions(lr5Rest);
    if (expressionArgs.Length > 0)
        SimulateVliw(string.Join(' ', expressionArgs), vliw);
    else
        RunVliwSamples(MemoExpressions.Lab5, vliw);
    return;
}

if (args is ["--lr6", .. var lr6Rest])
{
    var (vliw, expressionArgs) = ParseVliwOptions(lr6Rest);
    if (expressionArgs.Length > 0)
        Optimize(string.Join(' ', expressionArgs), vliw);
    else
        RunOptimizationStudy(MemoExpressions.Lab6, vliw);
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
    Console.WriteLine("ЛР3:");
    Console.WriteLine("  6 — Ввести вираз (еквівалентні форми: винесення за дужки)");
    Console.WriteLine("  7 — Тести ЛР3 з пам'ятки (А1–А7)");
    Console.WriteLine("ЛР4:");
    Console.WriteLine("  8 — Ввести вираз (еквівалентні форми: розкриття дужок)");
    Console.WriteLine("  9 — Тести ЛР4 з пам'ятки (Д1–Д5 і приклад графа)");
    Console.WriteLine("ЛР5:");
    Console.WriteLine("  10 — Ввести вираз (модель VLIW: 2 процесори, 1 банк пам'яті)");
    Console.WriteLine("  11 — Тести ЛР5 (Т1–Т6 з прикладу)");
    Console.WriteLine("ЛР6:");
    Console.WriteLine("  12 — Ввести вираз (вибір оптимальної форми для VLIW)");
    Console.WriteLine("  13 — Дослідження ЛР6 (прості, середні і складні вирази)");
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
        case "6":
            if (ReadExpression() is { } formsExpression)
                BuildForms(formsExpression);
            break;
        case "7":
            RunFactoringSamples(MemoExpressions.Associative);
            break;
        case "8":
            if (ReadExpression() is { } lr4Expression)
                BuildForms(lr4Expression, ReadLaws());
            break;
        case "9":
            RunFactoringSamples(MemoExpressions.Lab4, ReadLaws());
            break;
        case "10":
            if (ReadExpression() is { } lr5Expression)
                SimulateVliw(lr5Expression, ReadVliwParameters());
            break;
        case "11":
            RunVliwSamples(MemoExpressions.Lab5, ReadVliwParameters());
            break;
        case "12":
            if (ReadExpression() is { } lr6Expression)
                Optimize(lr6Expression, ReadVliwParameters());
            break;
        case "13":
            RunOptimizationStudy(MemoExpressions.Lab6, ReadVliwParameters());
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

static Laws ReadLaws()
{
    Console.Write("Закони: 1 — D + A (розкриття і винесення, як у прикладі пам'ятки), 2 — лише D [1]: ");
    return Console.ReadLine()?.Trim() == "2" ? Laws.Distributive : Laws.Distributive | Laws.Associative;
}

static void BuildForms(string expression, Laws laws = Laws.Associative)
{
    Console.WriteLine();
    ConsoleRenderer.PrintFactoring(FactoringExplorer.Explore(expression, laws));
}

static void RunFactoringSamples(IEnumerable<SampleExpression> samples, Laws laws = Laws.Associative)
{
    foreach (var sample in samples)
    {
        Console.WriteLine();
        ConsoleRenderer.WriteLine(new string('═', 70), ConsoleColor.DarkGray);
        ConsoleRenderer.WriteLine(Title(sample), ConsoleColor.Cyan);
        ConsoleRenderer.PrintFactoring(FactoringExplorer.Explore(sample.Expression, laws));
    }
}

static VliwParameters ReadVliwParameters()
{
    var defaults = new VliwParameters();
    Console.WriteLine($"Параметри системи (Enter залишає варіант 66: {defaults})");
    Console.Write("Процесорів, банків, +, -, *, /, пам'ять через пробіл: ");
    var numbers = (Console.ReadLine() ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Select(s => int.TryParse(s, out int n) ? n : (int?)null).ToList();
    int Get(int i, int fallback) => i < numbers.Count && numbers[i] is int n ? n : fallback;
    return new VliwParameters(Get(0, defaults.Processors), Get(1, defaults.Banks), Get(2, defaults.Add),
        Get(3, defaults.Subtract), Get(4, defaults.Multiply), Get(5, defaults.Divide), Get(6, defaults.Memory));
}

static void SimulateVliw(string expression, VliwParameters parameters)
{
    Console.WriteLine();
    ConsoleRenderer.PrintVliw(VliwModel.Run(expression, parameters));
}

static void RunVliwSamples(IEnumerable<SampleExpression> samples, VliwParameters parameters)
{
    foreach (var sample in samples)
    {
        Console.WriteLine();
        ConsoleRenderer.WriteLine(new string('═', 70), ConsoleColor.DarkGray);
        ConsoleRenderer.WriteLine(Title(sample), ConsoleColor.Cyan);
        ConsoleRenderer.PrintVliw(VliwModel.Run(sample.Expression, parameters));
    }
}

static (VliwParameters Parameters, string[] Expression) ParseVliwOptions(string[] args)
{
    var vliw = new VliwParameters();
    while (args is [var option, var value, .. var optionRest] && option.StartsWith("--") && int.TryParse(value, out int number))
    {
        vliw = option switch
        {
            "--p" => vliw with { Processors = number },
            "--banks" => vliw with { Banks = number },
            "--add" => vliw with { Add = number },
            "--sub" => vliw with { Subtract = number },
            "--mul" => vliw with { Multiply = number },
            "--div" => vliw with { Divide = number },
            "--mem" => vliw with { Memory = number },
            _ => vliw,
        };
        args = optionRest;
    }
    return (vliw, args);
}

static void Optimize(string expression, VliwParameters parameters)
{
    Console.WriteLine();
    ConsoleRenderer.PrintOptimization(FormOptimizer.Optimize(expression, parameters));
}

static void RunOptimizationStudy(IEnumerable<SampleExpression> samples, VliwParameters parameters)
{
    var results = new List<(string, OptimizationResult)>();
    foreach (var sample in samples)
    {
        Console.WriteLine();
        ConsoleRenderer.WriteLine(new string('═', 70), ConsoleColor.DarkGray);
        ConsoleRenderer.WriteLine(Title(sample), ConsoleColor.Cyan);
        var result = FormOptimizer.Optimize(sample.Expression, parameters);
        ConsoleRenderer.PrintOptimization(result, top: 5);
        results.Add((sample.Id, result));
    }
    Console.WriteLine();
    ConsoleRenderer.WriteLine("Підсумок дослідження:", ConsoleColor.Cyan);
    ConsoleRenderer.PrintOptimizationSummary(results);
}

static string Title(SampleExpression sample) =>
    string.IsNullOrEmpty(sample.Description) ? $"Тест {sample.Id}" : $"Тест {sample.Id}: {sample.Description}";
