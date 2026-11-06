using Pzks.Core.Ast;
using Pzks.Core.Factoring;

namespace Pzks.Web;

/// <summary>Форма для таблиці ЛР3. Step — винесення, яким форма отримана з форми ParentId.</summary>
public sealed record FormInfo(
    int Id,
    int? ParentId,
    int Depth,
    string Code,
    string Infix,
    string Parallel,
    int Tiers,
    int Operations,
    int Width,
    FactoringStep? Step)
{
    public static FormInfo From(EquivalentForm form) => new(
        form.Id,
        form.ParentId,
        form.Depth,
        form.Code,
        form.Infix,
        ExprPrinter.ToGroupedInfix(form.Tree),
        form.Tiers,
        form.Operations,
        form.Width,
        form.Path.LastOrDefault());
}

/// <summary>
/// Original — результат ЛР2 для введеного виразу (помилки ЛР1, ділення на 0, дерево форми №0).
/// Forms — від кращої до гіршої. Graph — граф перетворень (якщо форм багато — лише шляхи до найкращих).
/// </summary>
public sealed record FactoringResponse(
    ParallelResponse Original,
    bool Truncated,
    int EdgeCount,
    int? BestId,
    IReadOnlyList<FormInfo> Forms,
    string? Graph,
    int GraphNodes)
{
    public int FullSearchForms => FactoringExplorer.FullSearchForms;
    public int BeamWidth => FactoringExplorer.BeamWidth;

    public static FactoringResponse From(FactoringResult result)
    {
        var original = ParallelResponse.From(result.Original);
        if (!result.IsValid)
            return new FactoringResponse(original, false, 0, null, [], null, 0);

        var nodes = FormGraphSvg.Nodes(result);
        return new FactoringResponse(
            original,
            result.Truncated,
            result.Edges.Count,
            result.Best?.Id,
            result.Ranked.Select(FormInfo.From).ToList(),
            FormGraphSvg.Render(result, nodes),
            nodes.Count);
    }
}
