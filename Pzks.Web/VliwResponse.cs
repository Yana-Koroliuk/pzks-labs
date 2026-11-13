using Pzks.Core.Parallelization;
using Pzks.Core.Simulation;

namespace Pzks.Web;

public sealed record VliwRequest(
    string? Expression,
    int? Processors,
    int? Banks,
    int? Add,
    int? Subtract,
    int? Multiply,
    int? Divide,
    int? Memory)
{
    public VliwParameters Parameters
    {
        get
        {
            var defaults = new VliwParameters();
            return new VliwParameters(
                Processors ?? defaults.Processors,
                Banks ?? defaults.Banks,
                Add ?? defaults.Add,
                Subtract ?? defaults.Subtract,
                Multiply ?? defaults.Multiply,
                Divide ?? defaults.Divide,
                Memory ?? defaults.Memory);
        }
    }
}

public sealed record TaskInfo(int Id, string Operator, string Text, string Expression, int Tier, int Duration,
    IReadOnlyList<int> Inputs, int? Consumer, bool Initial, int Priority, int Word, int Processor, int Start, int End);

public sealed record CommandInfo(int Task, int Processor, int Order, int Start, int End, int Reads, int Waits);

public sealed record WordInfo(int Index, int Start, int End, int Length, IReadOnlyList<CommandInfo> Commands);

public sealed record ProcessorInfo(int Id, int Tasks, int Reads, int Compute, int Writes, int Waits, int Useful, double Efficiency);

public sealed record BankInfo(int Id, int Reads, int Writes, int Busy);

public sealed record VliwMetrics(
    int Sequential,
    int Parallel,
    double Speedup,
    double Efficiency,
    double EucalyptusEfficiency,
    int Processors,
    int ActiveProcessors,
    double ActiveEfficiency,
    int Words,
    int MemoryWaits,
    int TreeWidth,
    int Tiers);

public sealed record VliwResponse(
    ParallelResponse Parallel,
    VliwParameters Parameters,
    IReadOnlyList<string> ParameterErrors,
    IReadOnlyList<string> GraphErrors,
    bool NoOperations,
    IReadOnlyList<TaskInfo> Tasks,
    string? GraphSvg,
    IReadOnlyList<WordInfo> Words,
    string? GanttSvg,
    IReadOnlyList<ProcessorInfo> Processors,
    IReadOnlyList<BankInfo> Banks,
    VliwMetrics? Metrics,
    IReadOnlyList<ProcessorSweep> Sweep)
{
    public static VliwResponse From(VliwResult result)
    {
        var parallel = ParallelResponse.From(result.Parallel);
        if (result.Schedule is not { } schedule)
            return new VliwResponse(parallel, result.Parameters, result.ParameterErrors, result.GraphErrors, result.NoOperations,
                [], null, [], null, [], [], null, []);

        var graph = schedule.Graph;
        var tasks = graph.Tasks.Select(t =>
        {
            var word = schedule.Words.First(w => w.Commands.Any(c => c.Task == t.Id));
            var command = word.Commands.First(c => c.Task == t.Id);
            return new TaskInfo(t.Id, t.Operator.ToString(), t.Text, t.Expression, t.Tier, t.Duration, t.Inputs, t.Consumer,
                t.IsInitial, t.Priority, word.Index, command.Processor, command.Start, command.End);
        }).ToList();

        var words = schedule.Words.Select(w => new WordInfo(w.Index, w.Start, w.End, w.Length,
            w.Commands.OrderBy(c => c.Order)
                .Select(c => new CommandInfo(c.Task, c.Processor, c.Order, c.Start, c.End, c.Reads, c.Waits)).ToList())).ToList();

        var processors = Enumerable.Range(1, schedule.Parameters.Processors).Select(p => new ProcessorInfo(p,
            schedule.Words.Sum(w => w.Commands.Count(c => c.Processor == p)),
            schedule.Count(p, CellKind.Read), schedule.Count(p, CellKind.Compute), schedule.Count(p, CellKind.Write),
            schedule.Count(p, CellKind.Wait), schedule.UsefulCells(p), schedule.ProcessorEfficiency(p))).ToList();

        var banks = Enumerable.Range(1, schedule.Parameters.Banks).Select(b => new BankInfo(b,
            schedule.Banks[b - 1].Count(c => c.Kind == CellKind.Read),
            schedule.Banks[b - 1].Count(c => c.Kind == CellKind.Write),
            schedule.BankBusy(b))).ToList();

        var tree = result.Parallel.Tree!;
        var metrics = new VliwMetrics(schedule.SequentialTime, schedule.ParallelTime, schedule.Speedup, schedule.Efficiency,
            schedule.EucalyptusEfficiency, schedule.Parameters.Processors, schedule.ActiveProcessors, schedule.ActiveEfficiency,
            schedule.Words.Count, schedule.MemoryWaits, TreeLevels.Width(tree), graph.Tiers);

        return new VliwResponse(parallel, result.Parameters, result.ParameterErrors, result.GraphErrors, false,
            tasks, TaskGraphSvg.Render(tree, graph, schedule.Parameters.Memory), words, Core.Simulation.GanttSvg.Render(schedule),
            processors, banks, metrics, result.Sweep);
    }
}
