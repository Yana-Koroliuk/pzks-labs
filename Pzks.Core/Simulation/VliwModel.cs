using Pzks.Core.Parallelization;

namespace Pzks.Core.Simulation;

/// <summary>Рядок дослідження: та сама модель з іншою кількістю процесорів.</summary>
public sealed record ProcessorSweep(int Processors, int ParallelTime, double Speedup, double Efficiency, int Words, int MemoryWaits);

public sealed record VliwResult(
    ParallelResult Parallel,
    VliwParameters Parameters,
    IReadOnlyList<string> ParameterErrors,
    IReadOnlyList<string> GraphErrors,
    TaskGraph? Graph,
    VliwSchedule? Schedule,
    IReadOnlyList<ProcessorSweep> Sweep)
{
    /// <summary>Після спрощення ЛР2 операцій не лишилось: вираз є числом або змінною.</summary>
    public bool NoOperations => Parallel.IsValid && ParameterErrors.Count == 0 && GraphErrors.Count == 0 && Graph is { Tasks.Count: 0 };
}

/// <summary>
/// ЛР5: вираз → перевірка ЛР1 → дерево паралельної форми ЛР2 → граф задачі → розклад на VLIW-системі → метрики.
/// </summary>
public static class VliwModel
{
    public const int SweepUpTo = 4;

    public static VliwResult Run(string expression, VliwParameters? parameters = null) =>
        Run(ParallelTreeBuilder.Build(expression), parameters ?? new VliwParameters());

    public static VliwResult Run(ParallelResult parallel, VliwParameters parameters)
    {
        var parameterErrors = parameters.Validate();
        if (!parallel.IsValid || parameterErrors.Count > 0)
            return new VliwResult(parallel, parameters, parameterErrors, [], null, null, []);

        var graph = TaskGraph.Build(parallel.Tree!, parameters, out var graphErrors);
        if (graphErrors.Count > 0 || graph.Tasks.Count == 0)
            return new VliwResult(parallel, parameters, parameterErrors, graphErrors, graph, null, []);

        var schedule = VliwSimulator.Run(graph, parameters);
        var sweep = Enumerable.Range(1, Math.Max(SweepUpTo, parameters.Processors))
            .Select(p => p == parameters.Processors ? schedule : VliwSimulator.Run(graph, parameters with { Processors = p }))
            .Select(s => new ProcessorSweep(s.Parameters.Processors, s.ParallelTime, s.Speedup, s.Efficiency, s.Words.Count, s.MemoryWaits))
            .ToList();
        return new VliwResult(parallel, parameters, parameterErrors, graphErrors, graph, schedule, sweep);
    }
}
