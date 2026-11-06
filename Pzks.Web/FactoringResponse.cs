using Pzks.Core.Ast;
using Pzks.Core.Factoring;

namespace Pzks.Web;

/// <summary>Запит ЛР4. Laws: «D» — лише розкриття дужок, «DA» — розкриття разом з винесенням за дужки.</summary>
public sealed record FormsRequest(string? Expression, string? Laws)
{
    public Laws ParsedLaws => Laws == "D" ? Core.Factoring.Laws.Distributive : Core.Factoring.Laws.Distributive | Core.Factoring.Laws.Associative;
}

/// <summary>Крок перетворення; Law — літера закону: «A» (винесення) або «D» (розкриття дужок).</summary>
public sealed record FormStepInfo(string Law, string Factor, string Before, string After)
{
    public static FormStepInfo From(FactoringStep step) => new(step.Law.Letter().ToString(), step.Factor, step.Before, step.After);
}

/// <summary>Форма для таблиці ЛР3–ЛР4. Step — крок, яким форма отримана з форми ParentId.</summary>
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
    FormStepInfo? Step)
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
        form.Path.LastOrDefault() is { } step ? FormStepInfo.From(step) : null);
}

/// <summary>
/// Original — результат ЛР2 для введеного виразу (помилки ЛР1, ділення на 0, дерево форми №0).
/// Forms — від кращої до гіршої. Graph — граф перетворень (якщо форм багато — лише шляхи до найкращих).
/// Laws — застосовані закони («A», «D» або «DA»); ExpandedId — форма з усіма розкритими дужками.
/// </summary>
public sealed record FactoringResponse(
    ParallelResponse Original,
    string Laws,
    bool Truncated,
    int EdgeCount,
    int? BestId,
    int? ExpandedId,
    IReadOnlyList<FormInfo> Forms,
    string? Graph,
    int GraphNodes)
{
    public int FullSearchForms => FactoringExplorer.FullSearchForms;
    public int BeamWidth => FactoringExplorer.BeamWidth;

    public static FactoringResponse From(FactoringResult result)
    {
        var original = ParallelResponse.From(result.Original);
        string laws = (result.Laws.HasFlag(Core.Factoring.Laws.Distributive) ? "D" : "")
            + (result.Laws.HasFlag(Core.Factoring.Laws.Associative) ? "A" : "");
        if (!result.IsValid)
            return new FactoringResponse(original, laws, false, 0, null, null, [], null, 0);

        var nodes = FormGraphSvg.Nodes(result);
        return new FactoringResponse(
            original,
            laws,
            result.Truncated,
            result.Edges.Count,
            result.Best?.Id,
            result.ExpandedId,
            result.Ranked.Select(FormInfo.From).ToList(),
            FormGraphSvg.Render(result, nodes),
            nodes.Count);
    }
}
