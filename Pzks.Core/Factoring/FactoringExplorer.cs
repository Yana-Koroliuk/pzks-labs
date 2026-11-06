using System.Text;
using Pzks.Core.Ast;
using Pzks.Core.Parallelization;

namespace Pzks.Core.Factoring;

/// <summary>
/// ЛР3–ЛР4: множина еквівалентних форм виразу.
/// ЛР3 (A) — асоціативний закон у трактуванні пам'ятки, винесення спільного множника за дужки:
/// ab + ac = a(b+c), a/c + b/c = (a+b)/c. ЛР4 (D) — дистрибутивний закон, розкриття дужок
/// (див. FactoringExplorer.Distributive.cs). Закони можна поєднувати, як у прикладі пам'ятки.
/// 1. Вираз проходить етапи 1–2 ЛР2 (розкриття дужок з + і -, спрощення) — це вихідна форма №0.
/// 2. A: для кожної суми (на будь-якій глибині: у дужках, в аргументах функцій) і кожного множника,
///    що трапляється хоча б у двох доданках, множник виноситься з усіх таких доданків,
///    а якщо їх не більше чотирьох — і з кожної підмножини з двох і більше доданків.
/// 3. Перебір у ширину: кожна нова форма знову розглядається як вихідна.
///    Однакові форми (з точністю до переставлення доданків і множників) зливаються в одну вершину графа.
/// 4. Повний перебір виконується, якщо форм не більше FullSearchForms. Інакше (довгі вирази)
///    перебір повторюється з обмеженням: на кожен наступний крок переходять лише BeamWidth форм
///    поточного кроку — половина з найменшою кількістю ярусів, половина з найменшою кількістю операцій.
/// 5. Для кожної форми будується дерево паралельної форми ЛР2; найкраща — з найменшою кількістю
///    ярусів, далі — операцій.
/// </summary>
public static partial class FactoringExplorer
{
    public const int FullSearchForms = 1000;
    public const int BeamWidth = 40;

    /// <summary>Найбільша кількість форм при обмеженому переборі.</summary>
    public const int MaxForms = 3000;

    private const int MaxSubsetTerms = 4;

    public static FactoringResult Explore(string expression) => Explore(expression, Laws.Associative);

    public static FactoringResult Explore(string expression, Laws laws)
    {
        var original = ParallelTreeBuilder.Build(expression);
        if (!original.IsValid)
            return new FactoringResult(original, [], [], false, laws);

        var start = ParallelTreeBuilder.Normalize(original.Source!);
        return new Search(original, start, laws, beamWidth: null, FullSearchForms).Run()
            ?? new Search(original, start, laws, BeamWidth, MaxForms).Run()
            ?? throw new InvalidOperationException("Обмежений перебір не може перевищити ліміт форм");
    }

    /// <param name="beamWidth">null — повний перебір.</param>
    private sealed class Search(ParallelResult original, Form start, Laws laws, int? beamWidth, int maxForms)
    {
        private readonly List<EquivalentForm> _forms = [];
        private readonly List<Form> _states = [];
        private readonly Dictionary<string, int> _index = [];
        private readonly List<FormEdge> _edges = [];
        private readonly HashSet<(int, int)> _edgeKeys = [];
        private bool _pruned;

        /// <summary>null — повний перебір перевищив ліміт форм.</summary>
        public FactoringResult? Run()
        {
            Add(start, null, [], original);

            var frontier = new List<int> { 0 };
            while (frontier.Count > 0)
            {
                var next = new List<int>();
                foreach (int from in frontier)
                {
                    foreach (var (result, move) in Moves(_states[from], laws))
                    {
                        var form = Prepare(result);
                        if (_index.TryGetValue(Canon(form), out int existing))
                        {
                            Link(from, existing, move);
                            continue;
                        }
                        if (_forms.Count >= maxForms)
                        {
                            if (beamWidth is null)
                                return null;
                            _pruned = true;
                            continue;
                        }

                        int id = Add(form, from, [.. _forms[from].Path, move], null);
                        Link(from, id, move);
                        next.Add(id);
                    }
                }
                frontier = Select(next);
            }

            int? expanded = laws.HasFlag(Laws.Distributive) ? ExpandAll() : null;
            return new FactoringResult(original, _forms, _edges, _pruned, laws, expanded);
        }

        /// <summary>
        /// Форма з усіма розкритими дужками (алгоритм методички): від форми №0 розкриття виконуються, доки
        /// можливо. Після обмеженого перебору її може не бути в множині — тоді вона додається разом зі шляхом.
        /// </summary>
        private int ExpandAll()
        {
            int current = 0;
            for (int guard = 0; guard < MaxForms; guard++)
            {
                var (result, move) = Moves(_states[current], Laws.Distributive).FirstOrDefault();
                if (result is null)
                    return current;

                var form = Prepare(result);
                if (!_index.TryGetValue(Canon(form), out int next))
                    next = Add(form, current, [.. _forms[current].Path, move], null);
                if (next == current)
                    return current;
                Link(current, next, move);
                current = next;
            }
            return current;
        }

        private List<int> Select(List<int> next)
        {
            if (beamWidth is not int width || next.Count <= width)
                return next;

            _pruned = true;
            var byTiers = next.OrderBy(id => _forms[id].Tiers).ThenBy(id => _forms[id].Operations).ThenBy(id => id);
            var byOperations = next.OrderBy(id => _forms[id].Operations).ThenBy(id => _forms[id].Tiers).ThenBy(id => id);
            return byTiers.Take(width / 2).Union(byOperations.Take(width - width / 2)).ToList();
        }

        private void Link(int from, int to, FactoringStep move)
        {
            if (from != to && _edgeKeys.Add((from, to)))
                _edges.Add(new FormEdge(from, to, move.Factor, move.Law));
        }

        private int Add(Form form, int? parent, List<FactoringStep> path, ParallelResult? parallel)
        {
            int id = _forms.Count;
            string infix = ExprPrinter.ToInfix(ParallelTreeBuilder.Sequential(form));
            parallel ??= ParallelTreeBuilder.Build(infix);
            if (!parallel.IsValid)
                throw new InvalidOperationException($"Форма «{infix}» не пройшла ЛР2");

            _forms.Add(new EquivalentForm(id, parent, path.Count, Code(path), infix, path, parallel));
            _states.Add(form);
            _index[Canon(form)] = id;
            return id;
        }
    }

    /// <summary>Код у позначеннях пам'ятки: «3Aa,c,(b-2)», «4Dk,t,xAa» — кроки одного закону йдуть групою.</summary>
    private static string Code(IReadOnlyList<FactoringStep> path)
    {
        if (path.Count == 0)
            return "0";
        var code = new StringBuilder().Append(path.Count);
        for (int i = 0; i < path.Count; i++)
        {
            if (i == 0 || path[i].Law != path[i - 1].Law)
                code.Append(path[i].Law.Letter());
            else
                code.Append(',');
            code.Append(path[i].Factor);
        }
        return code.ToString();
    }

    /// <summary>Нова форма після кроку: злиття ланцюжків, спрощення ЛР2, знищення протилежних доданків.</summary>
    private static Form Prepare(Form result)
    {
        var form = ParallelTreeBuilder.Normalize(ParallelTreeBuilder.Sequential(Flatten(result)));
        var cancelled = CancelOpposites(form);
        return ReferenceEquals(cancelled, form)
            ? form
            : ParallelTreeBuilder.Normalize(ParallelTreeBuilder.Sequential(cancelled));
    }

    // ---------- Можливі кроки ----------

    /// <summary>
    /// Усі форми, які виходять з даної одним кроком: винесенням за дужки в будь-якій сумі (A)
    /// або розкриттям дужки в будь-якому добутку (D).
    /// </summary>
    private static IEnumerable<(Form Result, FactoringStep Move)> Moves(Form form, Laws laws)
    {
        if (form is CallForm call)
        {
            for (int i = 0; i < call.Arguments.Count; i++)
            {
                foreach (var (child, move) in Moves(call.Arguments[i], laws))
                {
                    var arguments = call.Arguments.ToList();
                    arguments[i] = child;
                    yield return (call with { Arguments = arguments }, move);
                }
            }
            yield break;
        }

        if (form is not ChainForm chain)
            yield break;

        if (chain.Kind == ChainKind.Sum && laws.HasFlag(Laws.Associative))
        {
            foreach (var move in FactorOut(chain))
                yield return move;
        }
        if (chain.Kind == ChainKind.Product && laws.HasFlag(Laws.Distributive))
        {
            foreach (var move in Expand(chain))
                yield return move;
        }

        for (int i = 0; i < chain.Parts.Count; i++)
        {
            foreach (var (child, move) in Moves(chain.Parts[i].Item, laws))
            {
                var parts = chain.Parts.ToList();
                parts[i] = parts[i] with { Item = child };
                yield return (chain with { Parts = parts }, move);
            }
        }
    }

    private sealed record Factor(Part Part, string Key, List<int> Terms);

    private static IEnumerable<(Form Result, FactoringStep Move)> FactorOut(ChainForm sum)
    {
        var terms = sum.Parts.Select(p => Factors(p.Item)).ToList();

        var factors = new List<Factor>();
        for (int i = 0; i < terms.Count; i++)
        {
            foreach (var part in terms[i])
            {
                if (part.Item.IsNumber(1))
                    continue;
                string key = FactorKey(part);
                var factor = factors.FirstOrDefault(f => f.Key == key);
                if (factor is null)
                    factors.Add(factor = new Factor(part, key, []));
                if (factor.Terms.LastOrDefault(-1) != i)
                    factor.Terms.Add(i);
            }
        }

        foreach (var factor in factors.Where(f => f.Terms.Count >= 2))
        {
            foreach (var subset in Subsets(factor.Terms))
                yield return Apply(sum, terms, factor, subset);
        }
    }

    /// <summary>Спершу всі доданки з множником, потім (якщо їх не більше чотирьох) — кожна менша підмножина.</summary>
    private static IEnumerable<List<int>> Subsets(List<int> terms)
    {
        yield return terms;
        if (terms.Count > MaxSubsetTerms)
            yield break;

        int full = (1 << terms.Count) - 1;
        for (int mask = full - 1; mask > 0; mask--)
        {
            if (int.PopCount(mask) < 2)
                continue;
            yield return terms.Where((_, bit) => (mask & (1 << bit)) != 0).ToList();
        }
    }

    private static (Form Result, FactoringStep Move) Apply(ChainForm sum, List<List<Part>> terms, Factor factor, List<int> subset)
    {
        var inner = new List<Part>();
        foreach (int i in subset)
        {
            var rest = terms[i].ToList();
            rest.RemoveAt(rest.FindIndex(p => FactorKey(p) == factor.Key));
            Form remainder = rest switch
            {
                [] => NumberForm.Of(1, "1"),
                [{ Inverse: false } single] => single.Item,
                _ => new ChainForm(ChainKind.Product, rest, ""),
            };
            inner.Add(new Part(sum.Parts[i].Inverse, remainder));
        }

        var innerSum = (ChainForm)Flatten(new ChainForm(ChainKind.Sum, inner, ""));
        bool negative = innerSum.Parts[0].Inverse;
        if (negative)
            innerSum = innerSum with { Parts = innerSum.Parts.Select(p => p with { Inverse = !p.Inverse }).ToList() };

        Part[] groupParts = factor.Part.Inverse
            ? [new Part(false, innerSum), factor.Part]
            : [factor.Part, new Part(false, innerSum)];
        var group = new Part(negative, new ChainForm(ChainKind.Product, groupParts, ""));

        var parts = sum.Parts.ToList();
        parts[subset[0]] = group;
        foreach (int i in subset.Skip(1).Reverse())
            parts.RemoveAt(i);

        var move = new FactoringStep(
            FactorLabel(factor.Part),
            SumText(subset.Select(i => sum.Parts[i]).ToList()),
            SumText([group]));
        return (new ChainForm(ChainKind.Sum, parts, ""), move);
    }

    private static List<Part> Factors(Form term) =>
        term is ChainForm { Kind: ChainKind.Product } product ? product.Parts.ToList() : [new Part(false, term)];

    // ---------- Нормалізація та порівняння форм ----------

    /// <summary>Вкладені однакові ланцюжки зливаються: a*(b*c) → a*b*c, сума з одного доданка — сам доданок.</summary>
    private static Form Flatten(Form form) => form switch
    {
        CallForm call => call with { Arguments = call.Arguments.Select(Flatten).ToList() },
        ChainForm chain => FlattenChain(chain),
        _ => form,
    };

    private static Form FlattenChain(ChainForm chain)
    {
        var parts = new List<Part>();
        foreach (var part in chain.Parts)
        {
            var item = Flatten(part.Item);
            if (item is ChainForm inner && inner.Kind == chain.Kind)
                parts.AddRange(inner.Parts.Select(p => p with { Inverse = p.Inverse != part.Inverse }));
            else
                parts.Add(part with { Item = item });
        }
        return parts is [{ Inverse: false } single] ? single.Item : chain with { Parts = parts };
    }

    /// <summary>
    /// Однакові доданки з протилежними знаками взаємно знищуються і тоді, коли множники в них записані
    /// в іншому порядку: x*3 - 3*x = 0 (ЛР2 знищує лише записані однаково). Без змін повертає ту саму форму.
    /// </summary>
    private static Form CancelOpposites(Form form)
    {
        switch (form)
        {
            case CallForm call:
            {
                var arguments = call.Arguments.Select(CancelOpposites).ToList();
                return arguments.SequenceEqual(call.Arguments, ReferenceEqualityComparer.Instance)
                    ? call
                    : call with { Arguments = arguments };
            }
            case ChainForm chain:
            {
                var parts = chain.Parts.Select(p => p with { Item = CancelOpposites(p.Item) }).ToList();
                bool changed = parts.Where((p, i) => !ReferenceEquals(p.Item, chain.Parts[i].Item)).Any();
                if (chain.Kind == ChainKind.Sum)
                {
                    for (int i = 0; i < parts.Count; i++)
                    {
                        string key = Canon(parts[i].Item);
                        int j = parts.FindIndex(i + 1, p => p.Inverse != parts[i].Inverse && Canon(p.Item) == key);
                        if (j < 0)
                            continue;
                        parts.RemoveAt(j);
                        parts.RemoveAt(i--);
                        changed = true;
                    }
                    if (parts.Count == 0)
                        return NumberForm.Of(0, "0");
                }
                return changed ? chain with { Parts = parts } : chain;
            }
            default:
                return form;
        }
    }

    /// <summary>Ключ форми без урахування порядку доданків і множників (переставний закон).</summary>
    private static string Canon(Form form) => form switch
    {
        NumberForm n => NumberForm.Format(n.Value),
        LeafForm l => l.Source,
        CallForm c => $"{c.Name}({string.Join(",", c.Arguments.Select(Canon))})",
        ChainForm ch => (ch.Kind == ChainKind.Sum ? "S(" : "P(")
            + string.Join(" ", ch.Parts.Select(p => ch.Kind.Operator(p.Inverse) + Canon(p.Item)).Order(StringComparer.Ordinal))
            + ")",
        _ => throw new ArgumentOutOfRangeException(nameof(form)),
    };

    private static string FactorKey(Part part) => (part.Inverse ? "/" : "*") + Canon(part.Item);

    // ---------- Запис ----------

    private static string Text(Form form) => ExprPrinter.ToInfix(ParallelTreeBuilder.Sequential(form));

    private static string ItemText(Form item) =>
        item is ChainForm { Kind: ChainKind.Sum } ? $"({Text(item)})" : Text(item);

    private static string FactorLabel(Part part) => (part.Inverse ? "/" : "") + ItemText(part.Item);

    /// <summary>Запис доданків: «-b*k+b*t».</summary>
    private static string SumText(IReadOnlyList<Part> parts) =>
        string.Concat(parts.Select((p, i) => (p.Inverse ? "-" : i == 0 ? "" : "+") + ItemText(p.Item)));
}
