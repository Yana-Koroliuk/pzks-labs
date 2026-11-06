using Pzks.Core.Ast;
using Pzks.Core.Parallelization;

namespace Pzks.Core.Factoring;

/// <summary>
/// Один крок перетворення: Before = After. Для A (винесення) Factor — винесений множник:
/// «-b*k+b*t = -b*(k-t)». Для D (розкриття) Factor — на що помножено дужку: «b*(k-t) = b*k-b*t».
/// </summary>
public sealed record FactoringStep(string Factor, string Before, string After, Laws Law = Laws.Associative);

/// <summary>
/// Еквівалентна форма виразу. Code — шлях у позначеннях пам'ятки: кількість кроків, літера закону
/// (A — винесення, D — розкриття дужок) і множники, наприклад «2Ab,(d+q-w)» або «4Dk,t,xAa».
/// Parallel — дерево паралельної форми ЛР2 для цієї форми.
/// </summary>
public sealed record EquivalentForm(
    int Id,
    int? ParentId,
    int Depth,
    string Code,
    string Infix,
    IReadOnlyList<FactoringStep> Path,
    ParallelResult Parallel)
{
    public ExprNode Tree => Parallel.Tree!;
    public int Tiers => TreeLevels.Tiers(Tree);
    public int Operations => TreeLevels.OperationCount(Tree);
    public int Width => TreeLevels.Width(Tree);
}

/// <summary>Ребро графа перетворень: з форми From одним кроком закону Law отримано форму To.</summary>
public sealed record FormEdge(int From, int To, string Factor, Laws Law = Laws.Associative);

/// <summary>ExpandedId — форма, у якій розкрито всі дужки (лише якщо застосовується закон D).</summary>
public sealed record FactoringResult(
    ParallelResult Original,
    IReadOnlyList<EquivalentForm> Forms,
    IReadOnlyList<FormEdge> Edges,
    bool Truncated,
    Laws Laws = Laws.Associative,
    int? ExpandedId = null)
{
    public bool IsValid => Original.IsValid;

    /// <summary>Форми від кращої до гіршої: менше ярусів, потім менше операцій, потім коротший шлях.</summary>
    public IReadOnlyList<EquivalentForm> Ranked =>
        Forms.OrderBy(f => f.Tiers).ThenBy(f => f.Operations).ThenBy(f => f.Depth).ThenBy(f => f.Id).ToList();

    public EquivalentForm? Best => Ranked.FirstOrDefault();

    /// <summary>Номери форм від вихідної до заданої.</summary>
    public IReadOnlyList<int> PathTo(int id)
    {
        var path = new List<int>();
        for (int? current = id; current is int c; current = Forms[c].ParentId)
            path.Add(c);
        path.Reverse();
        return path;
    }
}
