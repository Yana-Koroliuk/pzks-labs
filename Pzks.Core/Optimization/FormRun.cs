using Pzks.Core.Factoring;
using Pzks.Core.Simulation;

namespace Pzks.Core.Optimization;

/// <summary>Еквівалентна форма (ЛР3–ЛР4) і результат її моделювання на VLIW-системі (ЛР5).</summary>
public sealed record FormRun(EquivalentForm Form, VliwSchedule Schedule)
{
    public int Sequential => Schedule.SequentialTime;
    public int Parallel => Schedule.ParallelTime;
    public double Speedup => Schedule.Speedup;
    public double Efficiency => Schedule.Efficiency;
    public int MemoryWaits => Schedule.MemoryWaits;
    public int Words => Schedule.Words.Count;

    /// <summary>Задачі, що читають вхідні дані: кожна з них займає банк пам'яті ще до обчислення.</summary>
    public int InitialTasks => Schedule.Graph.Tasks.Count(t => t.IsInitial);

    /// <summary>
    /// Порядок форм: менший час Tp; за рівності менше операцій (система менше зайнята), менше ярусів,
    /// коротший шлях перетворень.
    /// </summary>
    public static IEnumerable<FormRun> Rank(IEnumerable<FormRun> runs) =>
        runs.OrderBy(r => r.Parallel).ThenBy(r => r.Form.Operations).ThenBy(r => r.Form.Tiers)
            .ThenBy(r => r.Form.Depth).ThenBy(r => r.Form.Id);
}

/// <summary>Крок направленого пошуку: після переходу на глибину Depth промодельовано Evaluated форм, найкращий час BestTime.</summary>
public sealed record SearchLevel(int Depth, int Evaluated, int BestTime);

/// <summary>
/// Направлений пошук: з кожного кроку перетворень далі йдуть лише BeamWidth форм з найменшим Tp,
/// пошук зупиняється, якщо Tp не зменшується Patience кроків поспіль.
/// </summary>
public sealed record DirectedSearch(int BeamWidth, int Patience, FactoringResult Factoring, IReadOnlyList<FormRun> Runs,
    IReadOnlyList<SearchLevel> Levels)
{
    public int Evaluated => Runs.Count;
    public FormRun Best => Runs[0];
}

/// <summary>Оптимальна форма для інших параметрів системи (дослідження адекватності форми архітектурі).</summary>
public sealed record ParameterStudy(string Label, VliwParameters Parameters, FormRun Best, FormRun Chosen);
