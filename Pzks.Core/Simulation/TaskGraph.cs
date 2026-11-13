using Pzks.Core.Ast;
using Pzks.Core.Parallelization;

namespace Pzks.Core.Simulation;

/// <summary>
/// Задача відповідає одній операції дерева паралельної форми. Номер і запис такі самі, як у таблиці ярусів ЛР2:
/// «[1]*c» означає, що операндом є результат задачі 1.
/// </summary>
public sealed record VliwTask(
    int Id,
    char Operator,
    string Text,
    string Expression,
    int Tier,
    int Duration,
    IReadOnlyList<int> Inputs,
    int? Consumer,
    int Priority)
{
    /// <summary>
    /// Початкова задача: серед операндів немає результатів інших задач. Як у Eucalyptus,
    /// лише такі задачі читають вхідні дані («*»); решта читає тільки результати попередніх задач.
    /// </summary>
    public bool IsInitial => Inputs.Count == 0;
}

/// <summary>Граф задачі: вершини є операціями, дуга йде з задачі-операнда в задачу, що використовує її результат.</summary>
public sealed record TaskGraph(IReadOnlyList<VliwTask> Tasks)
{
    public VliwTask this[int id] => Tasks[id - 1];

    public int Tiers => Tasks.Count == 0 ? 0 : Tasks.Max(t => t.Tier);

    /// <summary>
    /// Граф з дерева паралельної форми. Функції в VLIW-моделі (як і в Eucalyptus) недопустимі:
    /// для них повертаються помилки, а граф не будується.
    /// </summary>
    public static TaskGraph Build(ExprNode tree, VliwParameters parameters, out IReadOnlyList<string> errors)
    {
        var functions = new List<string>();
        CollectFunctions(tree, functions);
        errors = functions;
        if (functions.Count > 0)
            return new TaskGraph([]);

        var nodes = TreeLevels.Operations(tree).SelectMany(level => level).ToList();
        var described = TierDiagram.Operations(tree).SelectMany(level => level).ToList();
        var ids = new Dictionary<ExprNode, int>(ReferenceEqualityComparer.Instance);
        foreach (var node in nodes)
            ids[node] = ids.Count + 1;

        var consumers = new Dictionary<int, int>();
        foreach (var node in nodes)
            foreach (var child in TreeLevels.Children(node))
                if (ids.TryGetValue(child, out int input))
                    consumers[input] = ids[node];

        var tiers = new Dictionary<ExprNode, int>(ReferenceEqualityComparer.Instance);
        foreach (var (level, i) in TreeLevels.Operations(tree).Select((level, i) => (level, i)))
            foreach (var node in level)
                tiers[node] = i + 1;

        var operators = nodes.Select(Operator).ToList();
        var priority = new int[nodes.Count + 1];
        for (int id = nodes.Count; id >= 1; id--)
        {
            int own = parameters.Duration(operators[id - 1]);
            priority[id] = own + (consumers.TryGetValue(id, out int consumer) ? priority[consumer] : 0);
        }

        var tasks = nodes.Select((node, i) =>
        {
            int id = i + 1;
            var inputs = TreeLevels.Children(node).Where(ids.ContainsKey).Select(c => ids[c]).ToList();
            return new VliwTask(id, operators[i], described[i].Text, described[i].Expression, tiers[node],
                parameters.Duration(operators[i]), inputs, consumers.TryGetValue(id, out int c) ? c : null, priority[id]);
        }).ToList();
        return new TaskGraph(tasks);
    }

    /// <summary>Унарний мінус стає операцією «-» (0 - x) з тривалістю віднімання.</summary>
    private static char Operator(ExprNode node) => node switch
    {
        BinaryNode b => b.Operator,
        UnaryNode u => u.Operator,
        _ => throw new ArgumentOutOfRangeException(nameof(node)),
    };

    private static void CollectFunctions(ExprNode node, List<string> errors)
    {
        if (node is FunctionNode f)
            errors.Add($"функція «{ExprPrinter.ToInfix(f)}»: у VLIW-моделі, як і в Eucalyptus, допустимі лише операції +, -, *, /");
        foreach (var child in TreeLevels.Children(node))
            CollectFunctions(child, errors);
    }
}
