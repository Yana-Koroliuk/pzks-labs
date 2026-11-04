using System.Globalization;
using System.Security;
using System.Text;
using Pzks.Core.Ast;

namespace Pzks.Core.Parallelization;

/// <summary>
/// Малюнок дерева у форматі SVG у стилі рис. 1 методички: операції — кола з номером
/// (порядок виконання по ярусах), операнди — курсивом в нижньому рядку.
/// Висота вузла відповідає його ярусу, тому операції одного ярусу стоять на одній лінії.
/// </summary>
public static class TreeSvg
{
    private const double LeafStep = 54;
    private const double TierHeight = 72;
    private const double Radius = 15;
    private const double Margin = 30;
    private const double PanelGap = 70;
    private const double TitleHeight = 50;
    private const double CaptionHeight = 34;
    private const double TierLabelWidth = 60;
    private const string Font = "'Times New Roman', serif";

    /// <summary>Одне дерево без заголовків.</summary>
    public static string Render(ExprNode tree) => Render("", ("", tree));

    public static string Render(string title, params (string Caption, ExprNode Tree)[] panels)
    {
        double titleHeight = title.Length == 0 ? Margin / 2 : TitleHeight;
        var layouts = panels.Select(p => new Layout(p.Caption, p.Tree)).ToList();
        double width = Margin * 2 + layouts.Sum(l => l.Width) + PanelGap * Math.Max(0, layouts.Count - 1);
        width = Math.Max(width, Margin * 2 + title.Length * 8.5);
        double height = titleHeight + layouts.Max(l => l.Height) + Margin / 2;

        var svg = new StringBuilder();
        svg.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{N(width)}\" height=\"{N(height)}\" viewBox=\"0 0 {N(width)} {N(height)}\" font-family=\"{Font}\">");
        svg.AppendLine($"<rect width=\"100%\" height=\"100%\" fill=\"white\"/>");
        if (title.Length > 0)
            svg.AppendLine($"<text x=\"{N(Margin)}\" y=\"30\" font-size=\"18\" font-weight=\"bold\">{Escape(title)}</text>");

        double x = Margin;
        foreach (var layout in layouts)
        {
            layout.Draw(svg, x, titleHeight);
            x += layout.Width + PanelGap;
        }

        svg.AppendLine("</svg>");
        return svg.ToString();
    }

    private sealed class Layout
    {
        private readonly string _caption;
        private readonly double _top;
        private readonly ExprNode _root;
        private readonly int _tiers;
        private readonly List<(ExprNode Node, double X)> _leaves = [];
        private readonly Dictionary<ExprNode, double> _x = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<ExprNode, int> _tier = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<ExprNode, int> _number = new(ReferenceEqualityComparer.Instance);

        public double Width { get; }
        public double Height { get; }

        public Layout(string caption, ExprNode root)
        {
            _caption = caption;
            _top = caption.Length == 0 ? 0 : CaptionHeight;
            _root = root;
            _tiers = TreeLevels.Tiers(root);
            Place(root);

            foreach (var operation in TreeLevels.Operations(root).SelectMany(level => level))
                _number[operation] = _number.Count + 1;

            double treeWidth = _leaves.Count == 0 ? 0 : _leaves[^1].X + LeafStep / 2;
            Width = Math.Max(treeWidth + TierLabelWidth, caption.Length * 7.5);
            Height = _top + _tiers * TierHeight + Radius * 2 + 20;
        }

        private double LeafY => _top + _tiers * TierHeight + Radius + 12;

        private double Y(ExprNode node) => _tier[node] == 0 ? LeafY : _top + (_tiers - _tier[node]) * TierHeight + Radius;

        private void Place(ExprNode node)
        {
            var children = TreeLevels.Children(node);
            if (children.Count == 0)
            {
                string label = LeafLabel(node);
                double x = _leaves.Count == 0
                    ? LeafStep / 2
                    : _leaves[^1].X + Math.Max(LeafStep, (LeafLabel(_leaves[^1].Node).Length + label.Length) * 4.5 + 16);
                _leaves.Add((node, x));
                _x[node] = x;
                _tier[node] = 0;
                return;
            }

            foreach (var child in children)
                Place(child);
            _x[node] = (_x[children[0]] + _x[children[^1]]) / 2;
            _tier[node] = 1 + children.Max(c => _tier[c]);
        }

        public void Draw(StringBuilder svg, double left, double top)
        {
            svg.AppendLine($"<g transform=\"translate({N(left)},{N(top)})\">");
            if (_caption.Length > 0)
                svg.AppendLine($"<text x=\"0\" y=\"16\" font-size=\"15\">{Escape(_caption)}</text>");

            double treeRight = _leaves.Count == 0 ? 0 : _leaves[^1].X + LeafStep / 2;
            for (int tier = 1; tier <= _tiers; tier++)
            {
                double y = _top + (_tiers - tier) * TierHeight + Radius;
                svg.AppendLine($"<line x1=\"0\" y1=\"{N(y)}\" x2=\"{N(treeRight)}\" y2=\"{N(y)}\" stroke=\"#e4e4e4\" stroke-dasharray=\"4 4\"/>");
                svg.AppendLine($"<text x=\"{N(treeRight + 8)}\" y=\"{N(y + 4)}\" font-size=\"12\" fill=\"#888\">ярус {tier}</text>");
            }

            DrawEdges(svg, _root);
            DrawNodes(svg, _root);
            svg.AppendLine("</g>");
        }

        private void DrawEdges(StringBuilder svg, ExprNode node)
        {
            foreach (var child in TreeLevels.Children(node))
            {
                double px = _x[node], py = Y(node), cx = _x[child], cy = Y(child);
                double dx = cx - px, dy = cy - py, length = Math.Sqrt(dx * dx + dy * dy);
                double ux = dx / length, uy = dy / length;

                double startX = px + ux * Radius, startY = py + uy * Radius;
                double endX, endY;
                if (_tier[child] == 0)
                {
                    // До верхнього краю підпису операнда.
                    double t = (cy - 16 - py) / dy;
                    endX = px + dx * t;
                    endY = cy - 16;
                }
                else
                {
                    endX = cx - ux * Radius;
                    endY = cy - uy * Radius;
                }
                svg.AppendLine($"<line x1=\"{N(startX)}\" y1=\"{N(startY)}\" x2=\"{N(endX)}\" y2=\"{N(endY)}\" stroke=\"black\" stroke-width=\"1\"/>");
                DrawEdges(svg, child);
            }
        }

        private void DrawNodes(StringBuilder svg, ExprNode node)
        {
            double x = _x[node], y = Y(node);
            if (_tier[node] == 0)
            {
                svg.AppendLine($"<text x=\"{N(x)}\" y=\"{N(y)}\" font-size=\"17\" font-style=\"italic\" text-anchor=\"middle\">{Escape(LeafLabel(node))}</text>");
                return;
            }

            string label = OperationLabel(node);
            double rx = Math.Max(Radius, label.Length * 4.5 + 7);
            svg.AppendLine($"<g class=\"node\" data-tier=\"{_tier[node]}\"><title>{_number[node]}: {Escape(ExprPrinter.ToGroupedInfix(node))} (ярус {_tier[node]})</title>");
            svg.AppendLine($"<ellipse cx=\"{N(x)}\" cy=\"{N(y)}\" rx=\"{N(rx)}\" ry=\"{N(Radius)}\" fill=\"white\" stroke=\"black\" stroke-width=\"1.2\"/>");
            svg.AppendLine($"<text x=\"{N(x)}\" y=\"{N(y + 5)}\" font-size=\"15\" text-anchor=\"middle\">{Escape(label)}</text>");
            svg.AppendLine($"<text x=\"{N(x + rx + 3)}\" y=\"{N(y - Radius + 6)}\" font-size=\"13\">{_number[node]}</text>");
            svg.AppendLine("</g>");

            foreach (var child in TreeLevels.Children(node))
                DrawNodes(svg, child);
        }
    }

    private static string OperationLabel(ExprNode node) => node switch
    {
        BinaryNode { Operator: '-' } => "−",
        BinaryNode b => b.Operator.ToString(),
        UnaryNode { Operator: '-' } => "−",
        UnaryNode u => u.Operator.ToString(),
        FunctionNode f => f.Name,
        _ => "",
    };

    private static string LeafLabel(ExprNode node) => node switch
    {
        NumberNode n => n.Text,
        VariableNode v => v.Name,
        FunctionNode f => $"{f.Name}()",
        _ => "",
    };

    private static string N(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);

    private static string Escape(string text) => SecurityElement.Escape(text) ?? "";
}
