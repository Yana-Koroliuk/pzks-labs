using System.Text;

namespace Pzks.Core.Ast;

public static class ExprPrinter
{
    /// <summary>Запис дерева у звичайному (інфіксному) вигляді з мінімально потрібними дужками.</summary>
    public static string ToInfix(ExprNode node) => Format(node, atStart: true);

    // atStart — вузол стоїть там, де граматика дозволяє унарний знак без дужок:
    // на початку виразу, аргументу функції або вмісту дужок.
    private static string Format(ExprNode node, bool atStart) => node switch
    {
        NumberNode n => n.Text,
        VariableNode v => v.Name,
        FunctionNode f => $"{f.Name}({string.Join(", ", f.Arguments.Select(a => Format(a, atStart: true)))})",
        UnaryNode u when atStart => $"{u.Operator}{FormatUnaryOperand(u.Operand)}",
        UnaryNode u => $"({u.Operator}{FormatUnaryOperand(u.Operand)})",
        BinaryNode b => FormatBinary(b, atStart),
        _ => throw new ArgumentOutOfRangeException(nameof(node)),
    };

    private static string FormatUnaryOperand(ExprNode operand) =>
        operand is BinaryNode or UnaryNode ? Parenthesize(operand) : Format(operand, atStart: false);

    private static string FormatBinary(BinaryNode node, bool atStart)
    {
        int precedence = Precedence(node);

        string left = Precedence(node.Left) < precedence
            ? Parenthesize(node.Left)
            : Format(node.Left, atStart);

        int rightPrecedence = Precedence(node.Right);
        bool rightNeedsParens = rightPrecedence < precedence
            || (rightPrecedence == precedence && node.Operator is '-' or '/');
        string right = rightNeedsParens ? Parenthesize(node.Right) : Format(node.Right, atStart: false);

        return $"{left}{node.Operator}{right}";
    }

    private static string Parenthesize(ExprNode node) => $"({Format(node, atStart: true)})";

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
