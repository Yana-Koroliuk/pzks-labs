using Pzks.Core.Ast;
using Pzks.Core.Factoring;
using Pzks.Core.Optimization;
using Pzks.Core.Parallelization;
using Pzks.Core.Simulation;

namespace Pzks.Web;

/// <summary>Рядок таблиці ЛР6: форма і результат її моделювання. Rank — місце за часом Tp (1 — оптимальна).</summary>
public sealed record RunInfo(
    int Rank,
    int Id,
    int Depth,
    string Code,
    string Infix,
    string Parallel,
    int Tiers,
    int Operations,
    int Width,
    int InitialTasks,
    int Sequential,
    int ParallelTime,
    double Speedup,
    double Efficiency,
    int MemoryWaits,
    int Words)
{
    public static RunInfo From(FormRun run, int rank) => new(
        rank,
        run.Form.Id,
        run.Form.Depth,
        run.Form.Code,
        run.Form.Infix,
        ExprPrinter.ToGroupedInfix(run.Form.Tree),
        run.Form.Tiers,
        run.Form.Operations,
        run.Form.Width,
        run.InitialTasks,
        run.Sequential,
        run.Parallel,
        run.Speedup,
        run.Efficiency,
        run.MemoryWaits,
        run.Words);
}

/// <summary>Схеми форми для сторінки: дерево ЛР2, граф задачі і діаграма Ганта.</summary>
public sealed record RunPictures(string Tree, string Graph, string Gantt, IReadOnlyList<FormStepInfo> Path)
{
    public static RunPictures From(FormRun run) => new(
        TreeSvg.Render(run.Form.Tree),
        TaskGraphSvg.Render(run.Form.Tree, run.Schedule.Graph, run.Schedule.Parameters.Memory),
        GanttSvg.Render(run.Schedule),
        run.Form.Path.Select(FormStepInfo.From).ToList());
}

public sealed record DirectedInfo(int BeamWidth, int Patience, int Evaluated, RunInfo Best, IReadOnlyList<SearchLevel> Levels);

public sealed record StudyInfo(string Label, VliwParameters Parameters, RunInfo Best, RunInfo Chosen);

public sealed record TimeCount(int ParallelTime, int Forms);

/// <summary>
/// Відповідь ЛР6. Runs — усі змодельовані форми за зростанням Tp.
/// Optimal — оптимальна форма (з повного перебору або з направленого пошуку, якщо там вона швидша),
/// OptimalCount — скільки форм мають такий самий найменший Tp.
/// </summary>
public sealed record OptimizationResponse(
    ParallelResponse Original,
    VliwParameters Parameters,
    IReadOnlyList<string> Errors,
    bool Valid,
    int FormCount,
    bool Truncated,
    IReadOnlyList<RunInfo> Runs,
    RunInfo? OriginalRun,
    RunInfo? Best,
    RunInfo? ByTiers,
    RunInfo? MaxSpeedup,
    RunInfo? MaxEfficiency,
    RunInfo? Optimal,
    int OptimalCount,
    bool DirectedIsBetter,
    RunPictures? OptimalPictures,
    RunPictures? OriginalPictures,
    IReadOnlyList<TimeCount> Distribution,
    DirectedInfo? Directed,
    IReadOnlyList<StudyInfo> Study)
{
    public int FullSearchForms => FactoringExplorer.FullSearchForms;

    public static OptimizationResponse From(OptimizationResult result)
    {
        var original = ParallelResponse.From(result.Factoring.Original);
        if (!result.IsValid)
            return new OptimizationResponse(original, result.Parameters, result.Errors, false, result.Factoring.Forms.Count,
                false, [], null, null, null, null, null, null, 0, false, null, null, [], null, []);

        var ranks = result.Runs.Select((run, i) => (run, i)).ToDictionary(x => x.run.Form.Id, x => x.i + 1);
        RunInfo Info(FormRun run) => RunInfo.From(run, ranks.GetValueOrDefault(run.Form.Id));

        var rows = result.Runs.Select(Info).ToList();

        var optimal = result.DirectedIsBetter ? RunInfo.From(result.Optimal, 0) : Info(result.Optimal);
        var directed = result.Directed is { } d
            ? new DirectedInfo(d.BeamWidth, d.Patience, d.Evaluated, RunInfo.From(d.Best, 0), d.Levels)
            : null;
        var study = result.Study.Select(s => new StudyInfo(s.Label, s.Parameters, RunInfo.From(s.Best, 0), RunInfo.From(s.Chosen, 0))).ToList();
        var distribution = result.Runs.GroupBy(r => r.Parallel).OrderBy(g => g.Key).Select(g => new TimeCount(g.Key, g.Count())).ToList();

        return new OptimizationResponse(
            original,
            result.Parameters,
            [],
            true,
            result.Runs.Count,
            result.Factoring.Truncated,
            rows,
            Info(result.Original),
            Info(result.Best),
            Info(result.ByTiers),
            Info(result.MaxSpeedup),
            Info(result.MaxEfficiency),
            optimal,
            result.OptimalForms.Count,
            result.DirectedIsBetter,
            RunPictures.From(result.Optimal),
            RunPictures.From(result.Original),
            distribution,
            directed,
            study);
    }
}
