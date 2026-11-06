using Pzks.Core.Parallelization;

namespace Pzks.Core.Factoring;

/// <summary>
/// ЛР4 (D): дистрибутивний закон — розкриття дужок a(b+c) = ab + ac за алгоритмом методички.
/// Дужка — сума всередині добутку (дужки з + і - без множників розкриває вже ЛР2). Розкривати можна
/// всі дужки, крім тих, перед якими стоїть «/»:
/// - _*(), ()*_*_/_ — кожен доданок дужки множиться (ділиться) на інші елементи добутку: на всі одразу
///   або, якщо їх не більше чотирьох, на будь-яку їх частину;
/// - ()*() — дві дужки перемножуються, результат — одна дужка;
/// - ()/()/() — останні знаки ділення замінюються множенням: дільники перемножуються, і дужка-дільник
///   розкривається всередині дільника: x/(a+b)/c = x/(a*c+b*c).
/// </summary>
public static partial class FactoringExplorer
{
    private const int MaxSubsetFactors = 4;

    private static IEnumerable<(Form Result, FactoringStep Move)> Expand(ChainForm product)
    {
        var parts = product.Parts;
        for (int i = 0; i < parts.Count; i++)
        {
            if (!IsBracket(parts[i]))
                continue;

            bool divisor = parts[i].Inverse;
            var others = Enumerable.Range(0, parts.Count)
                .Where(j => j != i && (!divisor || parts[j].Inverse))
                .ToList();
            foreach (var subset in MultiplierSubsets(others))
            {
                // Добуток двох дужок отримується з боку кожної з них — лишаємо варіант від першої.
                if (subset.Any(j => j < i && IsBracket(parts[j]) && parts[j].Inverse == divisor))
                    continue;
                yield return Distribute(product, i, subset);
            }
        }
    }

    private static bool IsBracket(Part part) => part.Item is ChainForm { Kind: ChainKind.Sum };

    /// <summary>Спершу всі елементи, потім (якщо їх не більше чотирьох) — кожна менша непорожня підмножина.</summary>
    private static IEnumerable<List<int>> MultiplierSubsets(List<int> others)
    {
        if (others.Count == 0)
            yield break;
        yield return others;
        if (others.Count > MaxSubsetFactors)
            yield break;

        int full = (1 << others.Count) - 1;
        for (int mask = full - 1; mask > 0; mask--)
            yield return others.Where((_, bit) => (mask & (1 << bit)) != 0).ToList();
    }

    /// <summary>Дужку bracket добутку product помножено на елементи subset; результат — одна дужка на місці першого з них.</summary>
    private static (Form Result, FactoringStep Move) Distribute(ChainForm product, int bracket, List<int> subset)
    {
        var parts = product.Parts;
        bool divisor = parts[bracket].Inverse;
        var used = subset.Append(bracket).Order().ToList();

        // Усередині дільника його елементи — множники: /(a+b)/c = /((a+b)*c).
        var terms = new List<(bool Negative, List<Part> Factors)> { (false, []) };
        foreach (int j in used)
        {
            var part = divisor ? parts[j] with { Inverse = false } : parts[j];
            if (!part.Inverse && part.Item is ChainForm { Kind: ChainKind.Sum } sum)
                terms = [.. from t in terms
                            from u in sum.Parts
                            select (t.Negative != u.Inverse, (List<Part>)[.. t.Factors, .. Factors(u.Item)])];
            else
                terms = [.. terms.Select(t => (t.Negative, (List<Part>)[.. t.Factors, part]))];
        }

        var expanded = new ChainForm(ChainKind.Sum, terms.Select(t => new Part(t.Negative, Term(t.Factors))).ToList(), "");
        var resultParts = parts.ToList();
        resultParts[used[0]] = new Part(divisor, expanded);
        foreach (int j in used.Skip(1).Reverse())
            resultParts.RemoveAt(j);
        var result = new ChainForm(ChainKind.Product, resultParts, "");

        // Підпис — що внесено в дужку: «k», «a*d», «/e»; для дужки-дільника — інші дільники як множники.
        string factor = string.Concat(subset.Select((j, k) =>
            (parts[j].Inverse && !divisor ? "/" : k == 0 ? "" : "*") + ItemText(parts[j].Item)));
        var after = ParallelTreeBuilder.Normalize(ParallelTreeBuilder.Sequential(Flatten(result)));
        return (result, new FactoringStep(factor, Text(product), Text(after), Laws.Distributive));
    }

    /// <summary>Доданок з множників; добуток не може починатися з дільника, тому на початок стає множник або 1.</summary>
    private static Form Term(List<Part> factors)
    {
        int first = factors.FindIndex(p => !p.Inverse);
        if (first < 0)
            factors = [new Part(false, NumberForm.Of(1, "1")), .. factors];
        else if (first > 0)
            factors = [factors[first], .. factors.Where((_, k) => k != first)];

        return factors is [{ Inverse: false } single] ? single.Item : new ChainForm(ChainKind.Product, factors, "");
    }
}
