using Pzks.Core.Ast;

namespace Pzks.Tests;

/// <summary>Значення виразу при псевдовипадкових значеннях змінних (seed) — для перевірки еквівалентності.</summary>
internal static class ExpressionValue
{
    public static double Of(ExprNode node, int seed) => node switch
    {
        NumberNode n => n.Value,
        VariableNode v => 1.5 + (v.Name.Sum(c => c * seed) % 97) / 50.0,
        UnaryNode u => u.Operator == '-' ? -Of(u.Operand, seed) : Of(u.Operand, seed),
        BinaryNode b => b.Operator switch
        {
            '+' => Of(b.Left, seed) + Of(b.Right, seed),
            '-' => Of(b.Left, seed) - Of(b.Right, seed),
            '*' => Of(b.Left, seed) * Of(b.Right, seed),
            _ => Of(b.Left, seed) / Of(b.Right, seed),
        },
        FunctionNode f => f.Name switch
        {
            "sin" => Math.Sin(Of(f.Arguments[0], seed)),
            "cos" => Math.Cos(Of(f.Arguments[0], seed)),
            "exp" => Math.Exp(Of(f.Arguments[0], seed)),
            "log" => Math.Log(Of(f.Arguments[0], seed)),
            _ => f.Arguments.Sum(a => Of(a, seed)),
        },
        _ => throw new ArgumentOutOfRangeException(nameof(node)),
    };

    public static void AssertEqual(ExprNode expected, ExprNode actual)
    {
        foreach (int seed in new[] { 1, 2, 3 })
        {
            double a = Of(expected, seed), b = Of(actual, seed);
            Assert.Equal(a, b, Math.Max(1e-9, Math.Abs(a) * 1e-9));
        }
    }
}
