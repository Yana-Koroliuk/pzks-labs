using Pzks.Core.Ast;

namespace Pzks.Core.Parallelization;

/// <summary>Операція ярусу: номер, запис через номери попередніх операцій і повний підвираз.</summary>
public sealed record NumberedOperation(int Number, string Text, string Expression);

/// <summary>
/// Текстове зображення дерева паралельної форми, як табличне дерево в пам'ятці:
/// зверху операнди, під ними яруси; кожна операція — дужка └─op─┘ над своїми операндами.
/// </summary>
public static class TierDiagram
{
    public static IReadOnlyList<string> Lines(ExprNode root)
    {
        var leaves = new List<string>();
        var spans = new Dictionary<ExprNode, (int First, int Last)>(ReferenceEqualityComparer.Instance);
        Collect(root);

        int cell = Math.Max(4, leaves.Max(l => l.Length) + 2);
        int width = leaves.Count * cell;
        int Center(int index) => index * cell + cell / 2;

        var lines = new List<string>();
        var row = Blank();
        for (int i = 0; i < leaves.Count; i++)
            Put(row, Center(i) - leaves[i].Length / 2, leaves[i]);
        lines.Add(new string(row).TrimEnd());

        var levels = TreeLevels.Operations(root);
        for (int tier = 0; tier < levels.Count; tier++)
        {
            row = Blank();
            foreach (var operation in levels[tier])
            {
                var (first, last) = spans[operation];
                int x0 = Center(first), x1 = Center(last);
                if (x1 > x0)
                {
                    row[x0] = '└';
                    row[x1] = '┘';
                    for (int x = x0 + 1; x < x1; x++)
                        row[x] = '─';
                }
                string label = Label(operation);
                Put(row, (x0 + x1) / 2 - label.Length / 2, label);
            }
            lines.Add($"{new string(row)}   ярус {tier + 1}");
        }
        return lines;

        char[] Blank() => Enumerable.Repeat(' ', width).ToArray();

        void Collect(ExprNode node)
        {
            var children = TreeLevels.Children(node);
            if (children.Count == 0)
            {
                spans[node] = (leaves.Count, leaves.Count);
                leaves.Add(LeafLabel(node));
                return;
            }
            foreach (var child in children)
                Collect(child);
            spans[node] = (spans[children[0]].First, spans[children[^1]].Last);
        }
    }

    /// <summary>Перелік операцій по ярусах; результат операції позначено [номер].</summary>
    public static IReadOnlyList<string> OperationLines(ExprNode root) =>
        Operations(root)
            .Select((level, i) => $"Ярус {i + 1} ({level.Count} оп.): "
                + string.Join("   ", level.Select(op => $"[{op.Number}] {op.Text}")))
            .ToList();

    /// <summary>Операції кожного ярусу з номерами; операнд-результат іншої операції записано як [номер].</summary>
    public static IReadOnlyList<IReadOnlyList<NumberedOperation>> Operations(ExprNode root)
    {
        var levels = TreeLevels.Operations(root);
        var ids = new Dictionary<ExprNode, int>(ReferenceEqualityComparer.Instance);
        foreach (var operation in levels.SelectMany(level => level))
            ids[operation] = ids.Count + 1;

        string Ref(ExprNode node) => ids.TryGetValue(node, out int id) ? $"[{id}]" : LeafLabel(node);

        string Describe(ExprNode operation) => operation switch
        {
            BinaryNode b => $"{Ref(b.Left)}{b.Operator}{Ref(b.Right)}",
            UnaryNode u => $"{u.Operator}{Ref(u.Operand)}",
            FunctionNode f => $"{f.Name}({string.Join(", ", f.Arguments.Select(Ref))})",
            _ => Ref(operation),
        };

        return levels
            .Select(level => (IReadOnlyList<NumberedOperation>)level
                .Select(op => new NumberedOperation(ids[op], Describe(op), ExprPrinter.ToGroupedInfix(op)))
                .ToList())
            .ToList();
    }

    private static string Label(ExprNode operation) => operation switch
    {
        BinaryNode b => b.Operator.ToString(),
        UnaryNode u => u.Operator.ToString(),
        FunctionNode f => f.Name,
        _ => "",
    };

    private static string LeafLabel(ExprNode node) => node switch
    {
        NumberNode n => n.Text,
        VariableNode v => v.Name,
        FunctionNode f => $"{f.Name}()",
        _ => node.ToString() ?? "",
    };

    private static void Put(char[] row, int start, string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            int x = start + i;
            if (x >= 0 && x < row.Length)
                row[x] = text[i];
        }
    }
}
