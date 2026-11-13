using System.Globalization;
using System.Security;
using System.Text;

namespace Pzks.Core.Simulation;

/// <summary>
/// Діаграма Ганта VLIW-системи: рядки процесорів і банків пам'яті, стовпці відповідають тактам,
/// угорі позначено межі наддовгих командних слів. Кольори як у Eucalyptus: синій означає читання,
/// червоний запис, зелений обчислення, сірий з рискою очікування банку пам'яті.
/// </summary>
public static class GanttSvg
{
    private const double LabelWidth = 74;
    private const double RowHeight = 28;
    private const double Margin = 16;
    private const double WordBand = 24;
    private const double TactRow = 18;
    private const double GroupGap = 12;
    private const double LegendHeight = 36;
    private const string Font = "'Segoe UI', Arial, sans-serif";

    private const string ReadFill = "#bfdbfe", WriteFill = "#fecaca", ComputeFill = "#bbf7d0", WaitFill = "#e5e7eb";

    public static string Render(VliwSchedule schedule)
    {
        int total = schedule.ParallelTime;
        double cell = total <= 45 ? 24 : total <= 80 ? 18 : 13;
        int processors = schedule.Processors.Count, banks = schedule.Banks.Count;
        double gridTop = Margin + WordBand + TactRow;
        double banksTop = gridTop + processors * RowHeight + GroupGap;
        double gridBottom = banksTop + banks * RowHeight;
        double width = Math.Max(Margin * 2 + LabelWidth + total * cell, 420);
        var legend = LegendLayout(width - Margin * 2);
        double height = gridBottom + 14 + legend.Max(l => l.Y) + LegendHeight / 2 + Margin;
        double X(int tact) => Margin + LabelWidth + (tact - 1) * cell;

        var svg = new StringBuilder();
        svg.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{N(width)}\" height=\"{N(height)}\" viewBox=\"0 0 {N(width)} {N(height)}\" font-family=\"{Font}\">");
        svg.AppendLine($"<defs><pattern id=\"gantt-wait\" width=\"6\" height=\"6\" patternUnits=\"userSpaceOnUse\" patternTransform=\"rotate(45)\"><rect width=\"6\" height=\"6\" fill=\"{WaitFill}\"/><line x1=\"0\" y1=\"0\" x2=\"0\" y2=\"6\" stroke=\"#9ca3af\" stroke-width=\"1.5\"/></pattern></defs>");
        svg.AppendLine("<rect width=\"100%\" height=\"100%\" fill=\"white\"/>");

        foreach (var word in schedule.Words)
        {
            double x = X(word.Start), w = word.Length * cell;
            string fill = word.Index % 2 == 1 ? "#e0e7ff" : "#f1f5f9";
            svg.AppendLine($"<g class=\"word\"><title>Слово {word.Index}: такти {word.Start}–{word.End} ({word.Length}), команд {word.Commands.Count}</title>");
            svg.AppendLine($"<rect x=\"{N(x)}\" y=\"{N(Margin)}\" width=\"{N(w)}\" height=\"{N(WordBand - 4)}\" rx=\"3\" fill=\"{fill}\"/>");
            string label = w >= 48 ? $"слово {word.Index}" : word.Index.ToString(CultureInfo.InvariantCulture);
            svg.AppendLine($"<text x=\"{N(x + w / 2)}\" y=\"{N(Margin + 14)}\" font-size=\"11\" text-anchor=\"middle\" fill=\"#3730a3\">{label}</text></g>");
        }

        int step = cell >= 18 ? 1 : 5;
        for (int t = 1; t <= total; t++)
            if (t == 1 || t % step == 0)
                svg.AppendLine($"<text x=\"{N(X(t) + cell / 2)}\" y=\"{N(gridTop - 5)}\" font-size=\"10\" fill=\"#6b7280\" text-anchor=\"middle\">{t}</text>");

        for (int p = 0; p < processors; p++)
        {
            double y = gridTop + p * RowHeight;
            RowFrame(svg, y, $"P{p + 1}", total, cell, X);
            DrawProcessor(svg, schedule, p + 1, y, cell, X);
        }
        for (int b = 0; b < banks; b++)
        {
            double y = banksTop + b * RowHeight;
            RowFrame(svg, y, $"Банк {b + 1}", total, cell, X);
            DrawBank(svg, schedule.Banks[b], b + 1, y, cell, X);
        }

        foreach (var word in schedule.Words.Skip(1))
            svg.AppendLine($"<line x1=\"{N(X(word.Start))}\" y1=\"{N(Margin)}\" x2=\"{N(X(word.Start))}\" y2=\"{N(gridBottom)}\" stroke=\"#4f46e5\" stroke-width=\"1.2\" stroke-dasharray=\"5 3\"/>");

        foreach (var item in legend)
        {
            double ly = gridBottom + 14 + item.Y;
            svg.AppendLine($"<rect x=\"{N(item.X)}\" y=\"{N(ly)}\" width=\"16\" height=\"14\" rx=\"2\" fill=\"{item.Fill}\" stroke=\"{item.Stroke}\" stroke-width=\"0.8\"/>");
            svg.AppendLine($"<text x=\"{N(item.X + 22)}\" y=\"{N(ly + 11)}\" font-size=\"12\" fill=\"#374151\">{Escape(item.Text)}</text>");
        }
        svg.AppendLine("</svg>");
        return svg.ToString();
    }

    private static void RowFrame(StringBuilder svg, double y, string label, int total, double cell, Func<int, double> x)
    {
        svg.AppendLine($"<text x=\"{N(Margin)}\" y=\"{N(y + RowHeight / 2 + 5)}\" font-size=\"13\" font-weight=\"600\">{Escape(label)}</text>");
        svg.AppendLine($"<rect x=\"{N(x(1))}\" y=\"{N(y + 2)}\" width=\"{N(total * cell)}\" height=\"{N(RowHeight - 4)}\" fill=\"#fafafa\" stroke=\"#e5e7eb\"/>");
        for (int t = 2; t <= total; t++)
            svg.AppendLine($"<line x1=\"{N(x(t))}\" y1=\"{N(y + 2)}\" x2=\"{N(x(t))}\" y2=\"{N(y + RowHeight - 2)}\" stroke=\"#eef0f2\"/>");
    }

    private static void DrawProcessor(StringBuilder svg, VliwSchedule schedule, int processor, double y, double cell, Func<int, double> x)
    {
        var cells = schedule.Processors[processor - 1];
        for (int t = 1; t <= cells.Count; t++)
        {
            var c = cells[t - 1];
            switch (c.Kind)
            {
                case CellKind.Compute:
                {
                    int end = t;
                    while (end < cells.Count && cells[end] is { Kind: CellKind.Compute } next && next.Task == c.Task)
                        end++;
                    var task = schedule.Graph[c.Task];
                    double w = (end - t + 1) * cell;
                    string label = w >= 40 ? $"{task.Id}: {OperatorLabel(task.Operator)}" : task.Id.ToString(CultureInfo.InvariantCulture);
                    Block(svg, x(t), y, w, ComputeFill, "#16a34a", label,
                        $"P{processor}, такти {t}–{end}: обчислення задачі {task.Id} ({task.Text})");
                    t = end;
                    break;
                }
                case CellKind.Read:
                    Block(svg, x(t), y, cell, ReadFill, "#2563eb", c.Source == 0 ? "*" : c.Source.ToString(CultureInfo.InvariantCulture),
                        c.Source == 0
                            ? $"P{processor}, такт {t}: читання вхідних даних задачі {c.Task} з банку {c.Bank}"
                            : $"P{processor}, такт {t}: читання результату задачі {c.Source} з банку {c.Bank} (для задачі {c.Task})");
                    break;
                case CellKind.Write:
                    Block(svg, x(t), y, cell, WriteFill, "#dc2626", c.Task.ToString(CultureInfo.InvariantCulture),
                        $"P{processor}, такт {t}: запис результату задачі {c.Task} у банк {c.Bank}");
                    break;
                case CellKind.Wait:
                    Block(svg, x(t), y, cell, "url(#gantt-wait)", "#9ca3af", "",
                        $"P{processor}, такт {t}: очікування банку пам'яті (задача {c.Task})");
                    break;
            }
        }
    }

    private static void DrawBank(StringBuilder svg, IReadOnlyList<BankCell> cells, int bank, double y, double cell, Func<int, double> x)
    {
        for (int t = 1; t <= cells.Count; t++)
        {
            var c = cells[t - 1];
            if (c.Kind == CellKind.Read)
                Block(svg, x(t), y, cell, ReadFill, "#2563eb", c.Input ? $"*{c.Data}" : c.Data.ToString(CultureInfo.InvariantCulture),
                    c.Input
                        ? $"Банк {bank}, такт {t}: P{c.Processor} читає вхідні дані задачі {c.Data}"
                        : $"Банк {bank}, такт {t}: P{c.Processor} читає результат задачі {c.Data}");
            else if (c.Kind == CellKind.Write)
                Block(svg, x(t), y, cell, WriteFill, "#dc2626", c.Data.ToString(CultureInfo.InvariantCulture),
                    $"Банк {bank}, такт {t}: P{c.Processor} записує результат задачі {c.Data}");
        }
    }

    private static void Block(StringBuilder svg, double x, double y, double w, string fill, string stroke, string label, string title)
    {
        svg.Append($"<g><title>{Escape(title)}</title><rect x=\"{N(x + 0.5)}\" y=\"{N(y + 3)}\" width=\"{N(w - 1)}\" height=\"{N(RowHeight - 6)}\" rx=\"2\" fill=\"{fill}\" stroke=\"{stroke}\" stroke-width=\"0.8\"/>");
        if (label.Length > 0)
        {
            double size = w >= 18 ? 11 : 9;
            svg.Append($"<text x=\"{N(x + w / 2)}\" y=\"{N(y + RowHeight / 2 + 4)}\" font-size=\"{N(size)}\" text-anchor=\"middle\">{Escape(label)}</text>");
        }
        svg.AppendLine("</g>");
    }

    private static List<(double X, double Y, string Fill, string Stroke, string Text)> LegendLayout(double available)
    {
        (string Fill, string Stroke, string Text)[] items =
        [
            (ReadFill, "#2563eb", "читання (*: вхідні дані, число: результат задачі)"),
            (ComputeFill, "#16a34a", "обчислення задачі"),
            (WriteFill, "#dc2626", "запис результату"),
            ("url(#gantt-wait)", "#9ca3af", "очікування банку пам'яті"),
        ];
        var layout = new List<(double, double, string, string, string)>();
        double x = 0, y = 0;
        foreach (var (fill, stroke, text) in items)
        {
            double w = 30 + text.Length * 6.3;
            if (x > 0 && x + w > available)
                (x, y) = (0, y + 20);
            layout.Add((Margin + x, y, fill, stroke, text));
            x += w;
        }
        return layout;
    }

    private static string OperatorLabel(char operation) => operation == '-' ? "−" : operation.ToString();

    private static string N(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);

    private static string Escape(string text) => SecurityElement.Escape(text) ?? "";
}
