using Pzks.Core.Ast;

namespace Pzks.Core.Parallelization;

/// <summary>
/// Яруси дерева: операція виконується на ярусі на 1 вищому за найвищий ярус своїх операндів
/// (змінні й константи — ярус 0). Операції одного ярусу можна виконувати паралельно.
/// </summary>
public static class TreeLevels
{
    /// <summary>Кількість ярусів операцій (тривалість обчислення в тактах при необмеженій кількості процесорів).</summary>
    public static int Tiers(ExprNode node) => node switch
    {
        UnaryNode u => 1 + Tiers(u.Operand),
        BinaryNode b => 1 + Math.Max(Tiers(b.Left), Tiers(b.Right)),
        FunctionNode { Arguments.Count: > 0 } f => 1 + f.Arguments.Max(Tiers),
        _ => 0,
    };

    /// <summary>Операції по ярусах: елемент 0 — ярус 1. Усередині ярусу — зліва направо.</summary>
    public static IReadOnlyList<IReadOnlyList<ExprNode>> Operations(ExprNode root)
    {
        var levels = new List<List<ExprNode>>();
        Visit(root);
        return levels;

        int Visit(ExprNode node)
        {
            var children = Children(node);
            if (children.Count == 0)
                return 0;

            int tier = 1 + children.Max(Visit);
            while (levels.Count < tier)
                levels.Add([]);
            levels[tier - 1].Add(node);
            return tier;
        }
    }

    public static int OperationCount(ExprNode root) => Operations(root).Sum(level => level.Count);

    /// <summary>Ширина дерева — найбільша кількість операцій на одному ярусі.</summary>
    public static int Width(ExprNode root) => Operations(root).Select(level => level.Count).DefaultIfEmpty(0).Max();

    internal static IReadOnlyList<ExprNode> Children(ExprNode node) => node switch
    {
        UnaryNode u => [u.Operand],
        BinaryNode b => [b.Left, b.Right],
        FunctionNode f => f.Arguments,
        _ => [],
    };
}
