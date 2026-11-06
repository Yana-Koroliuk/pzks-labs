using System.Globalization;
using System.Security;
using System.Text;

namespace Pzks.Core.Factoring;

/// <summary>
/// Граф перетворень у стилі пам'ятки: вершини — форми (номер у колі), ребра — винесення, підпис ребра —
/// винесений множник. Вершини одного кроку стоять в одному ряду. Суцільне ребро — перше отримання форми
/// (з підписом), пунктирне — та сама форма іншим шляхом (множник — у підказці). Під графом — список форм.
/// </summary>
public static class FormGraphSvg
{
    public const int MaxNodes = 30;

    private const double Step = 72;
    private const double LayerHeight = 110;
    private const double Radius = 15;
    private const double Margin = 30;
    private const double LegendLine = 20;
    private const string Font = "'Times New Roman', serif";

    /// <summary>
    /// Вершини для малюнка: усі форми, якщо їх не більше MaxNodes, інакше — шляхи від вихідної форми
    /// до найкращих форм (у порядку рейтингу), поки вершин не більше MaxNodes.
    /// </summary>
    public static IReadOnlyList<int> Nodes(FactoringResult result)
    {
        if (result.Forms.Count <= MaxNodes)
            return result.Forms.Select(f => f.Id).ToList();

        var nodes = new SortedSet<int>();
        var minOperations = result.Forms.MinBy(f => (f.Operations, f.Tiers, f.Id))!;
        var first = result.ExpandedId is int expanded ? [result.Ranked[0], result.Forms[expanded], minOperations] : new[] { minOperations };
        foreach (var form in first.Concat(result.Ranked))
        {
            var path = result.PathTo(form.Id);
            if (nodes.Count + path.Count(id => !nodes.Contains(id)) > MaxNodes)
                continue;
            nodes.UnionWith(path);
        }
        return nodes.ToList();
    }

    public static string Render(FactoringResult result) => Render(result, Nodes(result));

    public static string Render(FactoringResult result, IReadOnlyCollection<int> nodes)
    {
        var shown = nodes.ToHashSet();
        var layers = nodes.Select(id => result.Forms[id])
            .GroupBy(f => f.Depth)
            .OrderBy(g => g.Key)
            .Select(g => g.OrderBy(f => f.Id).ToList())
            .ToList();
        int widest = layers.Max(l => l.Count);
        double graphWidth = Math.Max(widest * Step, 200);

        var position = new Dictionary<int, (double X, double Y)>();
        for (int layer = 0; layer < layers.Count; layer++)
        {
            double offset = Margin + (graphWidth - layers[layer].Count * Step) / 2 + Step / 2;
            for (int i = 0; i < layers[layer].Count; i++)
                position[layers[layer][i].Id] = (offset + i * Step, Margin + Radius + layer * LayerHeight);
        }

        int? best = result.Best?.Id;
        bool lawsLine = result.Laws.HasFlag(Laws.Distributive);
        var legend = nodes.Order().Select(id => result.Forms[id]).ToList();
        double legendTop = Margin + layers.Count * LayerHeight - LayerHeight / 2 + Radius + 20;
        double height = legendTop + (legend.Count + 1 + (lawsLine ? 1.5 : 0)) * LegendLine + Margin / 2;
        double legendWidth = Math.Max(legend.Max(f => LegendText(f).Length), lawsLine ? 75 : 0) * 7.2 + Margin * 2;
        double width = Math.Max(graphWidth + Margin * 2, legendWidth);

        var svg = new StringBuilder();
        svg.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{N(width)}\" height=\"{N(height)}\" viewBox=\"0 0 {N(width)} {N(height)}\" font-family=\"{Font}\">");
        svg.AppendLine("<defs><marker id=\"arrow\" viewBox=\"0 0 10 10\" refX=\"10\" refY=\"5\" markerWidth=\"7\" markerHeight=\"7\" orient=\"auto-start-reverse\"><path d=\"M0,0 L10,5 L0,10 z\" fill=\"#333\"/></marker></defs>");
        svg.AppendLine("<rect width=\"100%\" height=\"100%\" fill=\"white\"/>");

        foreach (var edge in result.Edges.Where(e => shown.Contains(e.From) && shown.Contains(e.To)))
            DrawEdge(svg, position[edge.From], position[edge.To], edge,
                tree: result.Forms[edge.To].ParentId == edge.From);

        foreach (int id in nodes)
        {
            var form = result.Forms[id];
            var (x, y) = position[id];
            string fill = id == best ? "#d9f2d9" : id == 0 ? "#eef3fb" : id == result.ExpandedId ? "#fbe7d6" : "white";
            double stroke = id == best ? 2.4 : 1.2;
            svg.AppendLine($"<g class=\"form-node\" data-id=\"{id}\"><title>{Escape(LegendText(form))}</title>");
            svg.AppendLine($"<circle cx=\"{N(x)}\" cy=\"{N(y)}\" r=\"{N(Radius)}\" fill=\"{fill}\" stroke=\"black\" stroke-width=\"{N(stroke)}\"/>");
            svg.AppendLine($"<text x=\"{N(x)}\" y=\"{N(y + 5)}\" font-size=\"14\" text-anchor=\"middle\">{id}</text>");
            svg.AppendLine("</g>");
        }

        if (lawsLine)
        {
            svg.AppendLine($"<text x=\"{N(Margin)}\" y=\"{N(legendTop)}\" font-size=\"13\">Підпис ребра: "
                + $"<tspan fill=\"{LawColor(Laws.Distributive)}\" font-style=\"italic\">D — розкрито дужку (що внесено в дужку)</tspan>, "
                + $"<tspan fill=\"{LawColor(Laws.Associative)}\" font-style=\"italic\">A — винесено множник</tspan></text>");
            legendTop += LegendLine * 1.5;
        }
        svg.AppendLine($"<text x=\"{N(Margin)}\" y=\"{N(legendTop)}\" font-size=\"14\" font-weight=\"bold\">Форми (ярусів / операцій):</text>");
        for (int i = 0; i < legend.Count; i++)
        {
            string weight = legend[i].Id == best ? " font-weight=\"bold\"" : "";
            svg.AppendLine($"<text x=\"{N(Margin)}\" y=\"{N(legendTop + (i + 1) * LegendLine)}\" font-size=\"13\"{weight}>{Escape(LegendText(legend[i]))}</text>");
        }

        svg.AppendLine("</svg>");
        return svg.ToString();
    }

    private static string LawColor(Laws law) => law == Laws.Distributive ? "#b5520b" : "#1a4f9c";

    private static void DrawEdge(StringBuilder svg, (double X, double Y) from, (double X, double Y) to, FormEdge edge, bool tree)
    {
        string factor = edge.Factor;
        double dx = to.X - from.X, dy = to.Y - from.Y, length = Math.Sqrt(dx * dx + dy * dy);
        double ux = dx / length, uy = dy / length;
        double x1 = from.X + ux * Radius, y1 = from.Y + uy * Radius;
        double x2 = to.X - ux * Radius, y2 = to.Y - uy * Radius;
        string style = tree ? "stroke=\"#333\"" : "stroke=\"#999\" stroke-dasharray=\"4 3\"";
        svg.AppendLine($"<line x1=\"{N(x1)}\" y1=\"{N(y1)}\" x2=\"{N(x2)}\" y2=\"{N(y2)}\" {style} stroke-width=\"1\" marker-end=\"url(#arrow)\"><title>{Escape(factor)}</title></line>");
        if (!tree)
            return;

        // Біля дочірньої вершини ребра одного батька вже розійшлися, тому підписи не накладаються.
        double lx = x1 + (x2 - x1) * 0.7, ly = y1 + (y2 - y1) * 0.7;
        svg.AppendLine($"<text x=\"{N(lx)}\" y=\"{N(ly + 4)}\" font-size=\"12\" font-style=\"italic\" text-anchor=\"middle\" fill=\"{LawColor(edge.Law)}\" stroke=\"white\" stroke-width=\"3\" paint-order=\"stroke\">{Escape(factor)}</text>");
    }

    private static string LegendText(EquivalentForm form) => $"{form.Id}: {form.Infix}   ({form.Tiers} / {form.Operations})";

    private static string N(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);

    private static string Escape(string text) => SecurityElement.Escape(text) ?? "";
}
