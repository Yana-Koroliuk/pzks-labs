using Pzks.Core.Factoring;
using Pzks.Core.Simulation;

namespace Pzks.Core.Optimization;

/// <summary>
/// Optimal — оптимальна форма: найменший Tp серед форм повного перебору і направленого пошуку.
/// Errors — чому моделювання неможливе (параметри системи, функції у виразі, немає операцій).
/// </summary>
public sealed record OptimizationResult(
    FactoringResult Factoring,
    VliwParameters Parameters,
    IReadOnlyList<string> Errors,
    IReadOnlyList<FormRun> Runs,
    DirectedSearch? Directed,
    IReadOnlyList<ParameterStudy> Study)
{
    public bool IsValid => Factoring.IsValid && Errors.Count == 0;

    /// <summary>Вихідна форма №0: дерево ЛР2 введеного виразу, як у ЛР5.</summary>
    public FormRun Original => Runs.First(r => r.Form.Id == 0);

    /// <summary>Найкраща форма повного перебору ЛР3–ЛР4 за часом Tp.</summary>
    public FormRun Best => Runs[0];

    /// <summary>Найкраща форма за критерієм ЛР3–ЛР4 (менше ярусів, потім операцій).</summary>
    public FormRun ByTiers => Runs.First(r => r.Form.Id == Factoring.Best!.Id);

    public FormRun MaxSpeedup => Runs.OrderByDescending(r => r.Speedup).ThenBy(r => r.Parallel).First();

    public FormRun MaxEfficiency => Runs.OrderByDescending(r => r.Efficiency).ThenBy(r => r.Parallel).First();

    /// <summary>Направлений пошук знайшов швидшу форму, ніж повний перебір (буває, коли перебір обмежений).</summary>
    public bool DirectedIsBetter => Directed is { } d && d.Best.Parallel < Best.Parallel;

    public FormRun Optimal => DirectedIsBetter ? Directed!.Best : Best;

    /// <summary>Усі форми з найменшим Tp (оптимальних форм може бути кілька); Optimal — перша з них за Rank.</summary>
    public IReadOnlyList<FormRun> OptimalForms =>
        (DirectedIsBetter ? Directed!.Runs : Runs).TakeWhile(r => r.Parallel == Optimal.Parallel).ToList();
}

/// <summary>
/// ЛР6: вибір оптимальної форми виразу для заданої VLIW-системи.
/// 1. Множина еквівалентних форм: ЛР3 (A, винесення за дужки) і ЛР4 (D, розкриття дужок), закони поєднуються.
///    Для кожної форми ЛР2 будує дерево паралельної форми.
/// 2. Кожна форма моделюється на VLIW-системі ЛР5: T1, Tp, Ky = T1/Tp, E = Ky/p.
/// 3. Оптимальна форма — з найменшим Tp (якщо таких кілька, усі вони в OptimalForms, а Optimal — та, що має менше
///    операцій, ярусів і перетворень). Ky і E для вибору не годяться: T1 у різних форм різний,
///    тому форма з більшою кількістю операцій може мати більше Ky і E, але виконуватися довше.
/// 4. Направлений пошук: граф перетворень обходиться від форми №0, але далі йдуть лише форми з найменшим
///    змодельованим Tp. Так знаходяться форми, адекватні архітектурі, навіть там, де повний перебір обмежено.
/// 5. Дослідження: оптимальна форма для інших параметрів системи і час обраної форми на них.
/// </summary>
public static class FormOptimizer
{
    public const Laws VariantLaws = Laws.Associative | Laws.Distributive;
    public const int DirectedBeam = 6;
    public const int DirectedPatience = 4;

    /// <summary>Параметри для дослідження: інша кількість процесорів і банків пам'яті.</summary>
    public static IReadOnlyList<(string Label, VliwParameters Parameters)> StudyParameters(VliwParameters parameters) =>
    [
        ("1 процесор", parameters with { Processors = 1 }),
        ("2 процесори, 1 банк", parameters with { Processors = 2, Banks = 1 }),
        ("3 процесори, 1 банк", parameters with { Processors = 3, Banks = 1 }),
        ("4 процесори, 1 банк", parameters with { Processors = 4, Banks = 1 }),
        ("2 процесори, 2 банки", parameters with { Processors = 2, Banks = 2 }),
        ("4 процесори, 4 банки", parameters with { Processors = 4, Banks = 4 }),
    ];

    public static OptimizationResult Optimize(string expression, VliwParameters? parameters = null, bool directed = true, bool study = true)
    {
        parameters ??= new VliwParameters();
        var factoring = FactoringExplorer.Explore(expression, VariantLaws);
        if (!factoring.IsValid)
            return new OptimizationResult(factoring, parameters, [], [], null, []);

        var errors = parameters.Validate().ToList();
        if (errors.Count == 0)
        {
            TaskGraph.Build(factoring.Forms[0].Tree, parameters, out var graphErrors);
            errors.AddRange(graphErrors);
            if (graphErrors.Count == 0 && factoring.Forms[0].Operations == 0)
                errors.Add("після спрощення ЛР2 операцій немає, моделювати нічого");
        }
        if (errors.Count > 0)
            return new OptimizationResult(factoring, parameters, errors, [], null, []);

        var runs = FormRun.Rank(SimulateAll(factoring.Forms, parameters)).ToList();
        var search = directed ? Directed(expression, parameters) : null;
        var result = new OptimizationResult(factoring, parameters, [], runs, search, []);
        return study ? result with { Study = Study(result) } : result;
    }

    public static FormRun Simulate(EquivalentForm form, VliwParameters parameters)
    {
        var graph = TaskGraph.Build(form.Tree, parameters, out _);
        return new FormRun(form, VliwSimulator.Run(graph, parameters));
    }

    private static List<FormRun> SimulateAll(IEnumerable<EquivalentForm> forms, VliwParameters parameters) =>
        forms.AsParallel().AsOrdered().Select(f => Simulate(f, parameters)).ToList();

    public static DirectedSearch Directed(string expression, VliwParameters parameters,
        int beamWidth = DirectedBeam, int patience = DirectedPatience)
    {
        var cache = new Dictionary<EquivalentForm, FormRun>(ReferenceEqualityComparer.Instance);
        FormRun Run(EquivalentForm form)
        {
            if (!cache.TryGetValue(form, out var run))
                cache[form] = run = Simulate(form, parameters);
            return run;
        }

        var factoring = FactoringExplorer.ExploreDirected(expression, VariantLaws, f => Run(f).Parallel, beamWidth, patience);
        var runs = factoring.Forms.Select(Run).ToList();

        var levels = new List<SearchLevel>();
        int best = int.MaxValue, evaluated = 0;
        foreach (var depth in runs.GroupBy(r => r.Form.Depth).OrderBy(g => g.Key))
        {
            evaluated += depth.Count();
            best = Math.Min(best, depth.Min(r => r.Parallel));
            levels.Add(new SearchLevel(depth.Key, evaluated, best));
        }
        return new DirectedSearch(beamWidth, patience, factoring, FormRun.Rank(runs).ToList(), levels);
    }

    private static IReadOnlyList<ParameterStudy> Study(OptimizationResult result)
    {
        var forms = result.Factoring.Forms.ToList();
        if (result.DirectedIsBetter)
            forms.Add(result.Optimal.Form);
        return StudyParameters(result.Parameters).Select(item =>
        {
            var runs = FormRun.Rank(SimulateAll(forms, item.Parameters)).ToList();
            var chosen = runs.First(r => ReferenceEquals(r.Form, result.Optimal.Form));
            return new ParameterStudy(item.Label, item.Parameters, runs[0], chosen);
        }).ToList();
    }
}
