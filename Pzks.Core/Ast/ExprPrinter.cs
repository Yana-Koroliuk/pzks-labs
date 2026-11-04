using System.Text;

namespace Pzks.Core.Ast;

public static class ExprPrinter
{
    /// <summary>Запис дерева у звичайному (інфіксному) вигляді з мінімально потрібними дужками.</summary>
    public static string ToInfix(ExprNode node) => Format(node, atStart: true, grouped: false);

    /// <summary>
    /// Запис, з якого видно структуру дерева: кожна операція того ж пріоритету, що й батьківська,
    /// береться в дужки, наприклад ((a+b)+(c+d)). Потрібен для дерева паралельної форми.
    /// </summary>
    public static string ToGroupedInfix(ExprNode node) => Format(node, atStart: true, grouped: true);

    // atStart — вузол стоїть там, де граматика дозволяє унарний знак без дужок:
    // на початку виразу, аргументу функції або вмісту дужок.
    private static string Format(ExprNode node, bool atStart, bool grouped) => node switch
    {
        NumberNode n => n.Text,
        VariableNode v => v.Name,
        FunctionNode f => $"{f.Name}({string.Join(", ", f.Arguments.Select(a => Format(a, atStart: true, grouped)))})",
        UnaryNode u when atStart => $"{u.Operator}{FormatUnaryOperand(u.Operand, grouped)}",
        UnaryNode u => $"({u.Operator}{FormatUnaryOperand(u.Operand, grouped)})",
        BinaryNode b => FormatBinary(b, atStart, grouped),
        _ => throw new ArgumentOutOfRangeException(nameof(node)),
    };

    private static string FormatUnaryOperand(ExprNode operand, bool grouped) =>
        operand is BinaryNode or UnaryNode ? Parenthesize(operand, grouped) : Format(operand, atStart: false, grouped);

    private static string FormatBinary(BinaryNode node, bool atStart, bool grouped)
    {
        int precedence = Precedence(node);

        int leftPrecedence = Precedence(node.Left);
        bool leftNeedsParens = leftPrecedence < precedence || (grouped && leftPrecedence == precedence);
        string left = leftNeedsParens ? Parenthesize(node.Left, grouped) : Format(node.Left, atStart, grouped);

        int rightPrecedence = Precedence(node.Right);
        bool rightNeedsParens = rightPrecedence < precedence
            || (rightPrecedence == precedence && (grouped || node.Operator is '-' or '/'));
        string right = rightNeedsParens ? Parenthesize(node.Right, grouped) : Format(node.Right, atStart: false, grouped);

        return $"{left}{node.Operator}{right}";
    }

    private static string Parenthesize(ExprNode node, bool grouped) => $"({Format(node, atStart: true, grouped)})";

    /// <summary>Дерево у вигляді рядків з псевдографікою, корінь зверху.</summary>
    public static IReadOnlyList<string> ToTreeLines(ExprNode node)
    {
        var lines = new List<string>();
        AppendTree(node, "", "", lines);
        return lines;
    }

    /// <summary>Висота дерева (кількість рівнів).</summary>
    public static int Height(ExprNode node) => node switch
    {
        UnaryNode u => 1 + Height(u.Operand),
        BinaryNode b => 1 + Math.Max(Height(b.Left), Height(b.Right)),
        FunctionNode { Arguments.Count: > 0 } f => 1 + f.Arguments.Max(Height),
        _ => 1,
    };

    private static int Precedence(ExprNode node) => node switch
    {
        BinaryNode { Operator: '+' or '-' } => 1,
        BinaryNode => 2,
        UnaryNode => 3,
        _ => 4,
    };

    private static void AppendTree(ExprNode node, string prefix, string childPrefix, List<string> lines)
    {
        lines.Add(prefix + Label(node));

        var children = Children(node);
        for (int i = 0; i < children.Count; i++)
        {
            bool last = i == children.Count - 1;
            AppendTree(children[i],
                childPrefix + (last ? "└── " : "├── "),
                childPrefix + (last ? "    " : "│   "),
                lines);
        }
    }

    private static string Label(ExprNode node) => node switch
    {
        NumberNode n => n.Text,
        VariableNode v => v.Name,
        UnaryNode u => $"{u.Operator} (унарний)",
        BinaryNode b => b.Operator.ToString(),
        FunctionNode f => $"{f.Name}()",
        _ => node.ToString() ?? "",
    };

    private static IReadOnlyList<ExprNode> Children(ExprNode node) => node switch
    {
        UnaryNode u => [u.Operand],
        BinaryNode b => [b.Left, b.Right],
        FunctionNode f => f.Arguments,
        _ => [],
    };
}
