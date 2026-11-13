using System.Globalization;
using System.Security;
using System.Text;
using Pzks.Core.Ast;
using Pzks.Core.Parallelization;

namespace Pzks.Core.Simulation;

/// <summary>
/// Граф задачі в стилі Eucalyptus: кожна вершина є операцією (угорі номер, нижче тривалість і тип),
/// дуги йдуть від задачі-операнда до задачі, що використовує результат; число на дузі означає тривалість
/// передачі через пам'ять. «*» над вершиною позначає читання вхідних даних. Ярус 1 угорі, корінь унизу.
/// </summary>
public static class TaskGraphSvg
{
    private const double LeafStep = 34;
    private const double TierHeight = 86;
    private const double Radius = 22;
    private const double Margin = 30;
    private const double Top = 46;
    private const double TierLabelWidth = 64;
    private const string Font = "'Times New Roman', serif";

    public static string Render(ExprNode tree, TaskGraph graph, int memory)
    {
        var x = new Dictionary<ExprNode, double>(ReferenceEqualityComparer.Instance);
        double nextLeaf = 0;
        Place(tree);

        var nodes = TreeLevels.Operations(tree).SelectMany(level => level).ToList();
        var position = nodes.Select((node, i) => (Task: graph.Tasks[i], X: x[node])).ToDictionary(p => p.Task.Id, p => p.X);
        double minX = position.Values.Min(), maxX = position.Values.Max();
        double Px(int id) => Margin + Radius + position[id] - minX;
        double Py(int id) => Top + (graph[id].Tier - 1) * TierHeight + Radius;

        double width = Margin * 2 + Radius * 2 + (maxX - minX) + TierLabelWidth;
        double height = Top + (graph.Tiers - 1) * TierHeight + Radius * 2 + Margin;

        var svg = new StringBuilder();
        svg.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{N(width)}\" height=\"{N(height)}\" viewBox=\"0 0 {N(width)} {N(height)}\" font-family=\"{Font}\">");
        svg.AppendLine("<defs><marker id=\"tg-arrow\" viewBox=\"0 0 10 10\" refX=\"10\" refY=\"5\" markerWidth=\"7\" markerHeight=\"7\" orient=\"auto-start-reverse\"><path d=\"M0,0 L10,5 L0,10 z\" fill=\"#333\"/></marker></defs>");
        svg.AppendLine("<rect width=\"100%\" height=\"100%\" fill=\"white\"/>");

        double right = width - Margin - TierLabelWidth + 10;
        for (int tier = 1; tier <= graph.Tiers; tier++)
        {
            double y = Top + (tier - 1) * TierHeight + Radius;
            svg.AppendLine($"<line x1=\"{N(Margin / 2)}\" y1=\"{N(y)}\" x2=\"{N(right)}\" y2=\"{N(y)}\" stroke=\"#e4e4e4\" stroke-dasharray=\"4 4\"/>");
            svg.AppendLine($"<text x=\"{N(right + 8)}\" y=\"{N(y + 4)}\" font-size=\"12\" fill=\"#888\">ярус {tier}</text>");
        }

        foreach (var task in graph.Tasks)
        {
            if (task.IsInitial)
            {
                double tx = Px(task.Id), ty = Py(task.Id) - Radius;
                svg.AppendLine($"<line x1=\"{N(tx)}\" y1=\"{N(ty - 24)}\" x2=\"{N(tx)}\" y2=\"{N(ty - 1)}\" stroke=\"#1d4ed8\" stroke-width=\"1\" marker-end=\"url(#tg-arrow)\"/>");
                svg.AppendLine($"<text x=\"{N(tx + 5)}\" y=\"{N(ty - 14)}\" font-size=\"15\" fill=\"#1d4ed8\">*</text>");
            }
            if (task.Consumer is not int consumer)
                continue;

            double x1 = Px(task.Id), y1 = Py(task.Id), x2 = Px(consumer), y2 = Py(consumer);
            double dx = x2 - x1, dy = y2 - y1, length = Math.Sqrt(dx * dx + dy * dy);
            double ux = dx / length, uy = dy / length;
            double sx = x1 + ux * Radius, sy = y1 + uy * Radius, ex = x2 - ux * Radius, ey = y2 - uy * Radius;
            svg.AppendLine($"<line x1=\"{N(sx)}\" y1=\"{N(sy)}\" x2=\"{N(ex)}\" y2=\"{N(ey)}\" stroke=\"#333\" stroke-width=\"1\" marker-end=\"url(#tg-arrow)\"/>");
            double mx = (sx + ex) / 2, my = (sy + ey) / 2;
            svg.AppendLine($"<text x=\"{N(mx + (dx >= 0 ? 6 : -6))}\" y=\"{N(my)}\" font-size=\"12\" fill=\"#666\" text-anchor=\"{(dx >= 0 ? "start" : "end")}\">{memory}</text>");
        }

        foreach (var task in graph.Tasks)
        {
            double cx = Px(task.Id), cy = Py(task.Id);
            string fill = task.IsInitial ? "#ecfdf5" : "white";
            svg.AppendLine($"<g class=\"task\" data-task=\"{task.Id}\"><title>Задача {task.Id}: {Escape(task.Text)} = {Escape(task.Expression)}; тривалість {task.Duration}, пріоритет {task.Priority}</title>");
            svg.AppendLine($"<circle cx=\"{N(cx)}\" cy=\"{N(cy)}\" r=\"{N(Radius)}\" fill=\"{fill}\" stroke=\"black\" stroke-width=\"1.2\"/>");
            svg.AppendLine($"<line x1=\"{N(cx - Radius)}\" y1=\"{N(cy)}\" x2=\"{N(cx + Radius)}\" y2=\"{N(cy)}\" stroke=\"#999\" stroke-width=\"0.8\"/>");
            svg.AppendLine($"<text x=\"{N(cx)}\" y=\"{N(cy - 6)}\" font-size=\"14\" font-weight=\"bold\" text-anchor=\"middle\">{task.Id}</text>");
            svg.AppendLine($"<text x=\"{N(cx)}\" y=\"{N(cy + 15)}\" font-size=\"13\" text-anchor=\"middle\">{task.Duration} {Escape(Label(task.Operator))}</text>");
            svg.AppendLine("</g>");
        }

        svg.AppendLine("</svg>");
        return svg.ToString();

        void Place(ExprNode node)
        {
            var children = TreeLevels.Children(node);
            if (children.Count == 0)
            {
                x[node] = nextLeaf;
                nextLeaf += LeafStep;
                return;
            }
            foreach (var child in children)
                Place(child);
            x[node] = (x[children[0]] + x[children[^1]]) / 2;
        }
    }

    private static string Label(char operation) => operation == '-' ? "−" : operation.ToString();

    private static string N(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);

    private static string Escape(string text) => SecurityElement.Escape(text) ?? "";
}
