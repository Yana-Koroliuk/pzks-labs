using System.Globalization;
using Pzks.Core;
using Pzks.Core.Ast;
using Pzks.Core.Correction;
using Pzks.Core.Diagnostics;
using Pzks.Core.Factoring;
using Pzks.Core.Lexing;
using Pzks.Core.Optimization;
using Pzks.Core.Parallelization;
using Pzks.Core.Parsing;
using Pzks.Core.Simulation;

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

    /// <summary>ЛР3: множина еквівалентних форм (винесення за дужки) і найкраща з них.</summary>
    public static void PrintFactoring(FactoringResult result, int top = 15)
    {
        var original = result.Original;
        Console.WriteLine($"Вираз: {original.Analysis.Expression}");
        if (!result.IsValid)
        {
            if (original.Source is null)
            {
                PrintErrors(original.Analysis);
                WriteLine("Вираз не пройшов перевірку ЛР1 — еквівалентні форми не будуються.", SyntaxColor);
            }
            else
            {
                foreach (var error in original.Errors)
                    WriteLine($"  терм «{error.Term}»: {error.Message}", SyntaxColor);
                WriteLine("Вираз містить ділення на 0 — еквівалентні форми не будуються.", SyntaxColor);
            }
            return;
        }

        var start = result.Forms[0];
        Console.WriteLine($"Форма №0 (після розкриття дужок і спрощення ЛР2): {start.Infix}");
        Console.WriteLine($"  паралельна форма: {ExprPrinter.ToGroupedInfix(start.Tree)}  "
            + $"(ярусів {start.Tiers}, операцій {start.Operations}, ширина {start.Width})");

        Console.WriteLine();
        Write($"Еквівалентних форм: {result.Forms.Count}", ConsoleColor.Green);
        Console.WriteLine($" (разом з вихідною), переходів у графі: {result.Edges.Count}");
        if (result.Truncated)
            WriteLine($"  Перебір неповний: форм більше за {FactoringExplorer.FullSearchForms}, тому на кожен наступний крок переходять "
                + $"{FactoringExplorer.BeamWidth} найкращих (за ярусами і за операціями), усього не більше {FactoringExplorer.MaxForms} форм.",
                ConsoleColor.Yellow);

        var ranked = result.Ranked;
        Console.WriteLine();
        WriteLine(ranked.Count > top ? $"Найкращі {top} форм:" : "Усі форми:", ConsoleColor.Cyan);
        Console.WriteLine($"{"№",5}  {"Ярусів",6}  {"Операцій",8}  {"Ширина",6}  {"Код",-22} Форма");
        foreach (var form in ranked.Take(top))
        {
            string line = $"{form.Id,5}  {form.Tiers,6}  {form.Operations,8}  {form.Width,6}  {form.Code,-22} {form.Infix}";
            if (form == result.Best)
                WriteLine(line, ConsoleColor.Green);
            else
                Console.WriteLine(line);
        }

        if (result.ExpandedId is int expandedId)
        {
            var expanded = result.Forms[expandedId];
            Console.WriteLine();
            WriteLine($"Усі дужки розкрито — форма №{expanded.Id} (код {expanded.Code}):", ConsoleColor.Cyan);
            Console.WriteLine($"  {expanded.Infix}  (ярусів {expanded.Tiers}, операцій {expanded.Operations}, ширина {expanded.Width})");
        }

        var best = result.Best!;
        Console.WriteLine();
        WriteLine($"Найкраща форма №{best.Id} (код {best.Code}):", ConsoleColor.Cyan);
        if (best.Path.Count == 0)
            Console.WriteLine(result.Laws == Laws.Associative
                ? "  винесення за дужки не покращує вихідну форму"
                : "  перетворення не покращують вихідну форму");
        for (int i = 0; i < best.Path.Count; i++)
            Console.WriteLine($"{i + 1,3}. {StepText(best.Path[i], result.Laws)}");
        Console.WriteLine($"Форма: {best.Infix}");
        Console.Write("Паралельна форма: ");
        WriteLine(ExprPrinter.ToGroupedInfix(best.Tree), ConsoleColor.Green);

        if (best.Tiers > 0)
        {
            Console.WriteLine();
            WriteLine("Дерево паралельної форми (операнди зверху, яруси вниз):", ConsoleColor.Cyan);
            foreach (string line in TierDiagram.Lines(best.Tree))
                Console.WriteLine("  " + line);
        }

        Console.WriteLine();
        Console.Write("Ярусів: ");
        WriteLine($"{start.Tiers} → {best.Tiers}", ConsoleColor.Green);
        Console.WriteLine($"Операцій: {start.Operations} → {best.Operations}");
        Console.WriteLine($"Найбільша ширина ярусу: {start.Width} → {best.Width}");
    }

    /// <summary>ЛР5: граф задачі, командні слова, діаграма Ганта і метрики VLIW-системи.</summary>
    public static void PrintVliw(VliwResult result)
    {
        var parallel = result.Parallel;
        Console.WriteLine($"Вираз: {parallel.Analysis.Expression}");
        Console.WriteLine($"Система: VLIW, {result.Parameters}");
        foreach (var error in result.ParameterErrors)
            WriteLine($"  Параметри: {error}", SyntaxColor);
        if (result.ParameterErrors.Count > 0)
            return;
        if (parallel.Source is null)
        {
            PrintErrors(parallel.Analysis);
            WriteLine("Вираз не пройшов перевірку ЛР1, тому модель не запускається.", SyntaxColor);
            return;
        }
        WriteLine("Перевірка ЛР1: помилок немає.", ConsoleColor.Green);
        if (!parallel.IsValid)
        {
            foreach (var error in parallel.Errors)
                WriteLine($"  терм «{error.Term}»: {error.Message}", SyntaxColor);
            WriteLine("Вираз містить ділення на 0, тому дерево ЛР2 не будується і модель не запускається.", SyntaxColor);
            return;
        }

        var tree = parallel.Tree!;
        Console.Write("Паралельна форма (ЛР2): ");
        WriteLine(ExprPrinter.ToGroupedInfix(tree), ConsoleColor.Green);
        foreach (var error in result.GraphErrors)
            WriteLine($"  {error}", SyntaxColor);
        if (result.GraphErrors.Count > 0)
            return;
        if (result.NoOperations)
        {
            WriteLine($"Після спрощення вираз дорівнює {ExprPrinter.ToInfix(tree)}: операцій немає, граф задачі не будується.", ConsoleColor.Yellow);
            return;
        }

        var schedule = result.Schedule!;
        var graph = schedule.Graph;
        Console.WriteLine();
        WriteLine("Граф задачі (операції дерева ЛР2; [n]: результат задачі n, *: вхідні дані з пам'яті):", ConsoleColor.Cyan);
        Console.WriteLine($"{"№",4}  {"Операція",-14} {"Ярус",4}  {"Тривалість",10}  {"Пріоритет",9}  Результат іде в");
        foreach (var task in graph.Tasks)
        {
            string text = (task.IsInitial ? "* " : "  ") + task.Text;
            string consumer = task.Consumer is int c ? $"задачу {c}" : "результат виразу";
            Console.WriteLine($"{task.Id,4}  {text,-14} {task.Tier,4}  {task.Duration,10}  {task.Priority,9}  {consumer}");
        }

        Console.WriteLine();
        WriteLine("Наддовгі командні слова (команда = читання операндів → обчислення → запис результату):", ConsoleColor.Cyan);
        foreach (var word in schedule.Words)
        {
            var commands = Enumerable.Range(1, schedule.Parameters.Processors).Select(p =>
                word.Commands.FirstOrDefault(c => c.Processor == p) is { } c
                    ? $"P{p}: [{c.Task}] {graph[c.Task].Text} ({c.Start}–{c.End}{(c.Waits > 0 ? $", очікування {c.Waits}" : "")})"
                    : $"P{p}: —");
            Console.WriteLine($"  Слово {word.Index,2}, такти {word.Start}–{word.End} ({word.Length}): {string.Join(" | ", commands)}");
        }

        Console.WriteLine();
        WriteLine("Діаграма Ганта (R*: читання вхідних даних, Rn: читання результату n, n+: обчислення задачі n, Wn: запис, ~~: очікування банку):",
            ConsoleColor.Cyan);
        PrintGantt(schedule);

        Console.WriteLine();
        WriteLine("Метрики (час у тактах):", ConsoleColor.Cyan);
        int initial = graph.Tasks.Count(t => t.IsInitial), durations = graph.Tasks.Sum(t => t.Duration);
        int memory = schedule.Parameters.Memory, p = schedule.Parameters.Processors;
        Console.WriteLine($"  T1 = {initial}·{memory} (читання вхідних даних) + {durations} (обчислення) + {graph.Tasks.Count}·{memory} (запис) = {schedule.SequentialTime}");
        Console.WriteLine($"  Tp = {schedule.ParallelTime}: кінець останнього командного слова (слів: {schedule.Words.Count})");
        Console.Write("  Ky = T1 / Tp = ");
        WriteLine($"{schedule.SequentialTime} / {schedule.ParallelTime} = {F(schedule.Speedup)}", ConsoleColor.Green);
        Console.Write($"  E  = Ky / p = {F(schedule.Speedup)} / {p} = ");
        WriteLine(F(schedule.Efficiency), ConsoleColor.Green);
        if (F(schedule.EucalyptusEfficiency) != F(schedule.Efficiency))
            Console.WriteLine($"       (Eucalyptus ділить уже округлене Ky і показує {F(schedule.EucalyptusEfficiency)})");
        Console.WriteLine($"  Ea = Ky / pa = {F(schedule.Speedup)} / {schedule.ActiveProcessors} = {F(schedule.ActiveEfficiency)}  (pa: процесори, що виконали задачі)");
        Console.WriteLine($"  Конфлікти пам'яті: тактів очікування банку {schedule.MemoryWaits}; ширина дерева ЛР2: {TreeLevels.Width(tree)}");
        for (int i = 1; i <= p; i++)
            Console.WriteLine($"  P{i}: корисних тактів {schedule.UsefulCells(i)} з {schedule.ParallelTime} (читання {schedule.Count(i, CellKind.Read)}, "
                + $"обчислення {schedule.Count(i, CellKind.Compute)}, запис {schedule.Count(i, CellKind.Write)}, очікування {schedule.Count(i, CellKind.Wait)}), завантаження {F(schedule.ProcessorEfficiency(i))}");

        Console.WriteLine();
        WriteLine($"Залежність від кількості процесорів (банків пам'яті: {schedule.Parameters.Banks}):", ConsoleColor.Cyan);
        Console.WriteLine($"  {"p",3}  {"Слів",5}  {"Tp",5}  {"Очікування",10}  {"Ky",6}  {"E",6}");
        foreach (var row in result.Sweep)
        {
            string line = $"  {row.Processors,3}  {row.Words,5}  {row.ParallelTime,5}  {row.MemoryWaits,10}  {F(row.Speedup),6}  {F(row.Efficiency),6}";
            if (row.Processors == p)
                WriteLine(line, ConsoleColor.Green);
            else
                Console.WriteLine(line);
        }

        Console.WriteLine();
        WriteLine("Розміщення для перевірки в Eucalyptus (у такому порядку):", ConsoleColor.Cyan);
        int step = 0;
        foreach (var (word, command) in schedule.Placement)
            Console.WriteLine($"  {++step,3}. слово {word.Index,2}: задача {command.Task,2} ({graph[command.Task].Text}) → P{command.Processor}, такт {command.Start}");
    }

    /// <summary>ЛР6: моделювання всіх еквівалентних форм, оптимальна форма, направлений пошук і дослідження параметрів.</summary>
    public static void PrintOptimization(OptimizationResult result, int top = 15)
    {
        var original = result.Factoring.Original;
        Console.WriteLine($"Вираз: {original.Analysis.Expression}");
        Console.WriteLine($"Система: VLIW, {result.Parameters}");
        if (!result.Factoring.IsValid)
        {
            if (original.Source is null)
            {
                PrintErrors(original.Analysis);
                WriteLine("Вираз не пройшов перевірку ЛР1, тому форми не будуються.", SyntaxColor);
            }
            else
            {
                foreach (var error in original.Errors)
                    WriteLine($"  терм «{error.Term}»: {error.Message}", SyntaxColor);
                WriteLine("Вираз містить ділення на 0, тому форми не будуються.", SyntaxColor);
            }
            return;
        }
        foreach (var error in result.Errors)
            WriteLine($"  {error}", SyntaxColor);
        if (result.Errors.Count > 0)
            return;

        Console.Write($"Еквівалентних форм (ЛР3 A + ЛР4 D): ");
        WriteLine(result.Runs.Count.ToString(), ConsoleColor.Green);
        if (result.Factoring.Truncated)
            WriteLine($"  Перебір обмежено: форм більше за {FactoringExplorer.FullSearchForms}.", ConsoleColor.Yellow);

        Console.WriteLine();
        WriteLine(result.Runs.Count > top ? $"Найшвидші {top} форм (за зростанням Tp):" : "Усі форми (за зростанням Tp):", ConsoleColor.Cyan);
        Console.WriteLine($"{"Місце",5} {"№",5}  {"Ярусів",6} {"Опер.",5} {"T1",4} {"Tp",4} {"Ky",5} {"E",5}  {"Код",-24} Форма");
        var shown = result.Runs.Take(top).ToList();
        if (!shown.Contains(result.Original))
            shown.Add(result.Original);
        foreach (var run in shown)
        {
            int rank = result.Runs.ToList().IndexOf(run) + 1;
            string line = $"{rank,5} {run.Form.Id,5}  {run.Form.Tiers,6} {run.Form.Operations,5} {run.Sequential,4} {run.Parallel,4} "
                + $"{F(run.Speedup),5} {F(run.Efficiency),5}  {run.Form.Code,-24} {run.Form.Infix}{(run.Form.Id == 0 ? "  (вихідна)" : "")}";
            if (run.Parallel == result.Best.Parallel)
                WriteLine(line, ConsoleColor.Green);
            else
                Console.WriteLine(line);
        }
        if (!result.DirectedIsBetter && result.OptimalForms.Count > 1)
            Console.WriteLine($"Оптимальних форм (Tp = {result.Optimal.Parallel}): {result.OptimalForms.Count}, обрано ту, що має найменше операцій, ярусів і перетворень.");

        Console.WriteLine();
        WriteLine("Критерії вибору:", ConsoleColor.Cyan);
        foreach (var (label, run) in new[]
        {
            ("найменший Tp", result.Best), ("найменше ярусів (ЛР3–ЛР4)", result.ByTiers),
            ("найбільше Ky", result.MaxSpeedup), ("найбільше E", result.MaxEfficiency), ("вихідна форма", result.Original),
        })
            Console.WriteLine($"  {label,-27} Tp = {run.Parallel,3}, T1 = {run.Sequential,3}, Ky = {F(run.Speedup)}, E = {F(run.Efficiency)}  {run.Form.Infix}");

        if (result.Directed is { } directed)
        {
            Console.WriteLine();
            WriteLine($"Направлений пошук (на крок переходять {directed.BeamWidth} форм з найменшим Tp, зупинка після {directed.Patience} кроків без покращення):",
                ConsoleColor.Cyan);
            foreach (var level in directed.Levels)
                Console.WriteLine($"  крок {level.Depth,2}: промодельовано {level.Evaluated,4}, найкращий Tp {level.BestTime}");
            Console.WriteLine($"  Промодельовано {directed.Evaluated} форм замість {result.Runs.Count}; найкраща: Tp = {directed.Best.Parallel}  {directed.Best.Form.Infix}");
        }

        var optimal = result.Optimal;
        Console.WriteLine();
        WriteLine($"Оптимальна форма (код {optimal.Form.Code}){(result.DirectedIsBetter ? ", знайдена направленим пошуком" : "")}:", ConsoleColor.Cyan);
        for (int i = 0; i < optimal.Form.Path.Count; i++)
            Console.WriteLine($"{i + 1,3}. {StepText(optimal.Form.Path[i], FormOptimizer.VariantLaws)}");
        if (optimal.Form.Path.Count == 0)
            Console.WriteLine("  перетворення не зменшують час, вихідна форма вже оптимальна");
        Console.WriteLine($"Форма: {optimal.Form.Infix}");
        Console.Write("Паралельна форма: ");
        WriteLine(ExprPrinter.ToGroupedInfix(optimal.Form.Tree), ConsoleColor.Green);
        Console.Write("Tp: ");
        WriteLine($"{result.Original.Parallel} → {optimal.Parallel}", ConsoleColor.Green);
        Console.WriteLine($"T1: {result.Original.Sequential} → {optimal.Sequential}; Ky: {F(result.Original.Speedup)} → {F(optimal.Speedup)}; "
            + $"E: {F(result.Original.Efficiency)} → {F(optimal.Efficiency)}");
        Console.WriteLine();
        WriteLine("Діаграма Ганта оптимальної форми:", ConsoleColor.Cyan);
        PrintGantt(optimal.Schedule);

        if (result.Study.Count > 0)
        {
            WriteLine("Оптимальна форма для інших параметрів системи:", ConsoleColor.Cyan);
            foreach (var study in result.Study)
                Console.WriteLine($"  {study.Label,-22} найкраща Tp = {study.Best.Parallel,3} (Ky {F(study.Best.Speedup)}, E {F(study.Best.Efficiency)}), "
                    + $"обрана Tp = {study.Chosen.Parallel,3}  {study.Best.Form.Infix}");
        }
    }

    /// <summary>ЛР6, дослідження: короткий рядок результатів для кожного виразу.</summary>
    public static void PrintOptimizationSummary(IReadOnlyList<(string Id, OptimizationResult Result)> results)
    {
        Console.WriteLine($"{"Тест",-5} {"Форм",5}  {"Tp вих.",7} {"Tp опт.",7} {"Виграш",7}  {"Ky",11} {"E",11}  {"Tp за ярус.",11}  {"Напр. пошук",14}");
        foreach (var (id, r) in results)
        {
            if (!r.IsValid)
            {
                Console.WriteLine($"{id,-5} моделювання неможливе");
                continue;
            }
            var (o, b) = (r.Original, r.Optimal);
            double gain = o.Parallel == 0 ? 0 : 100.0 * (o.Parallel - b.Parallel) / o.Parallel;
            string directed = r.Directed is { } d ? $"{d.Best.Parallel} ({d.Evaluated} ф.)" : "";
            Console.WriteLine($"{id,-5} {r.Runs.Count,5}  {o.Parallel,7} {b.Parallel,7} {gain,6:F1}%  {F(o.Speedup)} → {F(b.Speedup)} "
                + $"{F(o.Efficiency)} → {F(b.Efficiency)}  {r.ByTiers.Parallel,11}  {directed,14}");
        }
    }

    private static void PrintGantt(VliwSchedule schedule, int perLine = 20)
    {
        const int width = 5;
        int total = schedule.ParallelTime;
        var wordAt = schedule.Words.ToDictionary(w => w.Start, w => w.Index);
        for (int from = 1; from <= total; from += perLine)
        {
            int to = Math.Min(total, from + perLine - 1);
            var tacts = Enumerable.Range(from, to - from + 1).ToList();
            Console.WriteLine("  " + "Слово".PadRight(8) + string.Concat(tacts.Select(t => (wordAt.TryGetValue(t, out int w) ? $"|{w}" : "").PadRight(width))));
            Console.WriteLine("  " + "Такт".PadRight(8) + string.Concat(tacts.Select(t => t.ToString().PadRight(width))));
            for (int p = 0; p < schedule.Processors.Count; p++)
            {
                Console.Write("  " + $"P{p + 1}".PadRight(8));
                foreach (int t in tacts)
                {
                    var cell = schedule.Processors[p][t - 1];
                    string text = cell.Kind switch
                    {
                        CellKind.Read => cell.Source == 0 ? "R*" : $"R{cell.Source}",
                        CellKind.Compute => $"{cell.Task}{schedule.Graph[cell.Task].Operator}",
                        CellKind.Write => $"W{cell.Task}",
                        CellKind.Wait => "~~",
                        _ => ".",
                    };
                    Write(text.PadRight(width), cell.Kind switch
                    {
                        CellKind.Read => ConsoleColor.Blue,
                        CellKind.Compute => ConsoleColor.Green,
                        CellKind.Write => ConsoleColor.Red,
                        _ => ConsoleColor.DarkGray,
                    });
                }
                Console.WriteLine();
            }
            for (int b = 0; b < schedule.Banks.Count; b++)
            {
                Console.Write("  " + $"Банк {b + 1}".PadRight(8));
                foreach (int t in tacts)
                {
                    var cell = schedule.Banks[b][t - 1];
                    string text = cell.Kind switch
                    {
                        CellKind.Read => cell.Input ? $"R*{cell.Data}" : $"R{cell.Data}",
                        CellKind.Write => $"W{cell.Data}",
                        _ => ".",
                    };
                    Write(text.PadRight(width), cell.Kind switch
                    {
                        CellKind.Read => ConsoleColor.Blue,
                        CellKind.Write => ConsoleColor.Red,
                        _ => ConsoleColor.DarkGray,
                    });
                }
                Console.WriteLine();
            }
            Console.WriteLine();
        }
    }

    private static string F(double value) => value.ToString("F2", CultureInfo.InvariantCulture);

    private static string StepText(FactoringStep step, Laws laws) => step.Law == Laws.Distributive
        ? $"D: розкрито дужку, внесено {step.Factor}: {step.Before} = {step.After}"
        : $"{(laws == Laws.Associative ? "" : "A: ")}винесено {step.Factor}: {step.Before} = {step.After}";

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
