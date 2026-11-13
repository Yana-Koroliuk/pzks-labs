using Pzks.Core.Samples;
using Pzks.Core.Simulation;

namespace Pzks.Tests;

public class VliwSimulatorTests
{
    private static VliwSchedule Schedule(string expression, VliwParameters? parameters = null) =>
        VliwModel.Run(expression, parameters).Schedule ?? throw new InvalidOperationException("Розклад не побудовано");

    private static string Row(IReadOnlyList<ProcessorCell> cells) => string.Concat(cells.Select(c => c.Kind switch
    {
        CellKind.Read => 'r',
        CellKind.Compute => 'c',
        CellKind.Write => 'w',
        CellKind.Wait => '-',
        _ => '.',
    }));

    private static string Row(IReadOnlyList<BankCell> cells) => string.Concat(cells.Select(c => c.Kind switch
    {
        CellKind.Read => 'r',
        CellKind.Write => 'w',
        _ => '.',
    }));

    public static TheoryData<string> Lab5Expressions()
    {
        var data = new TheoryData<string>();
        foreach (var sample in MemoExpressions.Lab5.Where(s => s.Id != "Т6"))
            data.Add(sample.Expression);
        data.Add(MemoExpressions.Lab2.Single(s => s.Id == "М4").Expression);
        return data;
    }

    [Fact]
    public void TaskGraph_NumbersOperationsLikeLr2Tiers()
    {
        var graph = Schedule("a+b+c*d+d/e-g*h+i/j").Graph;

        Assert.Equal(["a+b", "c*d", "d/e", "g*h", "i/j", "[1]+[2]", "[3]-[4]", "[7]+[5]", "[6]+[8]"], graph.Tasks.Select(t => t.Text));
        Assert.Equal([2, 4, 8, 4, 8, 2, 3, 2, 2], graph.Tasks.Select(t => t.Duration));
        Assert.Equal([6, 6, 7, 7, 8, 9, 8, 9, null], graph.Tasks.Select(t => t.Consumer));
        Assert.All(graph.Tasks.Take(5), t => Assert.True(t.IsInitial));
        // Пріоритет дорівнює довжині шляху до кореня: d/e → [3]-[4] → [7]+[5] → корінь = 8 + 3 + 2 + 2.
        Assert.Equal(15, graph[3].Priority);
    }

    [Fact]
    public void FourSums_MatchHandCalculation()
    {
        // Слово 1: P1 читає a,b (такт 1), рахує 2–3, пише 4; P2 чекає банк, читає 2, рахує 3–4, пише 5.
        // Слово 2: P1 має результат 1, читає результат 2 (6), рахує 7–8, пише 9.
        var s = Schedule("a+b+c+d");

        Assert.Equal(2, s.Words.Count);
        Assert.Equal("rccw.rccw", Row(s.Processors[0]));
        Assert.Equal("-rccw....", Row(s.Processors[1]));
        Assert.Equal("rr.wwr..w", Row(s.Banks[0]));
        Assert.Equal(9, s.ParallelTime);
        Assert.Equal(11, s.SequentialTime);
        Assert.Equal(1.22, Math.Round(s.Speedup, 2));
        Assert.Equal(0.61, Math.Round(s.Efficiency, 2));
        Assert.Equal(1, s.MemoryWaits);
    }

    [Fact]
    public void Test1_MatchesEucalyptusTactByTact()
    {
        // Ті самі такти показав клас VLIW симулятора Eucalyptus для цього розміщення задач.
        var s = Schedule("a+b+c*d+d/e-g*h+i/j");

        Assert.Equal("rccccccccw.rccccw.cccw.rccwrccw", Row(s.Processors[0]));
        Assert.Equal("-rccccccccw-rccccwrcc-wccw.....", Row(s.Processors[1]));
        Assert.Equal("rr.......wwrr...wwr..wwr.wwr..w", Row(s.Banks[0]));
        Assert.Equal(31, s.ParallelTime);
        Assert.Equal(49, s.SequentialTime);
        Assert.Equal(1.58, Math.Round(s.Speedup, 2));
        Assert.Equal(0.79, Math.Round(s.Efficiency, 2));
        Assert.Equal(0.90, Math.Round(s.ProcessorEfficiency(1), 2));
        Assert.Equal(0.74, Math.Round(s.ProcessorEfficiency(2), 2));
    }

    [Fact]
    public void EucalyptusEfficiency_DividesRoundedSpeedup()
    {
        var s = Schedule(MemoExpressions.Lab5.Single(x => x.Id == "Т5").Expression);

        Assert.Equal(61, s.ParallelTime);
        Assert.Equal(87, s.SequentialTime);
        Assert.Equal(0.71, Math.Round(s.Efficiency, 2));
        Assert.Equal(0.72, s.EucalyptusEfficiency);
    }

    [Theory]
    [MemberData(nameof(Lab5Expressions))]
    public void OneProcessor_TakesSequentialTime(string expression)
    {
        var s = Schedule(expression, new VliwParameters(Processors: 1));

        Assert.Equal(s.SequentialTime, s.ParallelTime);
        Assert.Equal(0, s.MemoryWaits);
    }

    [Theory]
    [MemberData(nameof(Lab5Expressions))]
    public void Schedule_KeepsVliwAndMemoryRules(string expression)
    {
        var s = Schedule(expression);
        var graph = s.Graph;

        Assert.Equal(graph.Tasks.Count, s.Words.Sum(w => w.Commands.Count));
        for (int i = 0; i < s.Words.Count; i++)
        {
            var word = s.Words[i];
            Assert.All(word.Commands, c => Assert.Equal(word.Start, c.Start));
            Assert.Equal(word.End, word.Commands.Max(c => c.End));
            Assert.True(word.Commands.Count <= s.Parameters.Processors);
            Assert.Equal(word.Commands.Count, word.Commands.Select(c => c.Processor).Distinct().Count());
            if (i > 0)
                Assert.Equal(s.Words[i - 1].End + 1, word.Start);
        }

        foreach (var task in graph.Tasks)
            foreach (int input in task.Inputs)
                Assert.True(s.CommandOf(input).End < s.CommandOf(task.Id).Start);

        // Один банк: у кожному такті не більше одного звернення, і кожне звернення процесора видно в банку.
        int accesses = s.Processors.Sum(cells => cells.Count(c => c.Kind is CellKind.Read or CellKind.Write));
        Assert.Equal(accesses, s.BankBusy(1));
        for (int t = 0; t < s.ParallelTime; t++)
            Assert.True(s.Processors.Count(cells => cells[t].Kind is CellKind.Read or CellKind.Write) <= 1);

        Assert.Equal(graph.Tasks.Count(t => t.IsInitial), s.Banks[0].Count(c => c.Input));
        Assert.Equal(graph.Tasks.Count, s.Banks[0].Count(c => c.Kind == CellKind.Write));
        Assert.True(s.Speedup > 1 && s.Speedup <= s.Parameters.Processors);
    }

    [Fact]
    public void ProcessorReusesOwnResult_WithoutReading()
    {
        // -a-b = -(a+b): обидві задачі на одному процесорі, результат задачі 1 не перечитується.
        var s = Schedule("-a-b");

        Assert.Equal(["a+b", "-[1]"], s.Graph.Tasks.Select(t => t.Text));
        Assert.Equal("rccwcccw", Row(s.Processors[0]));
        Assert.Equal(1, s.ActiveProcessors);
        Assert.Equal(1.0, s.Speedup);
        Assert.Equal(0.5, s.Efficiency);
        Assert.Equal(1.0, s.ActiveEfficiency);
    }

    [Fact]
    public void MoreBanks_RemoveMemoryConflicts()
    {
        var one = Schedule("a+b+c+d+e+f+g+h");
        var two = Schedule("a+b+c+d+e+f+g+h", new VliwParameters(Banks: 2));

        Assert.True(one.MemoryWaits > 0);
        Assert.True(two.ParallelTime < one.ParallelTime);
        Assert.Equal(one.SequentialTime, two.SequentialTime);
    }

    [Fact]
    public void ConstantExpression_HasNoTasks()
    {
        var result = VliwModel.Run("5040/8/7/6/5/4/3/2");

        Assert.True(result.NoOperations);
        Assert.Null(result.Schedule);
    }

    [Fact]
    public void Functions_AreNotAllowed()
    {
        var result = VliwModel.Run("sin(x)+1");

        Assert.Single(result.GraphErrors);
        Assert.Contains("sin(x)", result.GraphErrors[0]);
        Assert.Null(result.Schedule);
    }

    [Fact]
    public void InvalidExpression_StopsBeforeModel()
    {
        var result = VliwModel.Run("a+*b");

        Assert.False(result.Parallel.Analysis.IsValid);
        Assert.Null(result.Graph);
    }

    [Fact]
    public void WrongParameters_AreReported()
    {
        var result = VliwModel.Run("a+b", new VliwParameters(Processors: 0, Divide: 50));

        Assert.Equal(2, result.ParameterErrors.Count);
        Assert.Null(result.Schedule);
    }

    [Fact]
    public void Sweep_CoversOneToFourProcessors()
    {
        var result = VliwModel.Run("a+b+c+d+e+f+g+h");

        Assert.Equal([1, 2, 3, 4], result.Sweep.Select(s => s.Processors));
        Assert.Equal(result.Schedule!.ParallelTime, result.Sweep[1].ParallelTime);
        Assert.Equal(1.0, result.Sweep[0].Speedup);
    }

    [Fact]
    public void Svgs_AreRendered()
    {
        var result = VliwModel.Run("a*2 + b/(b-a) - 1/x4/x3");
        var schedule = result.Schedule!;

        string gantt = GanttSvg.Render(schedule);
        string graph = TaskGraphSvg.Render(result.Parallel.Tree!, schedule.Graph, schedule.Parameters.Memory);

        Assert.StartsWith("<svg", gantt);
        Assert.Contains("Банк 1", gantt);
        Assert.Equal(schedule.Graph.Tasks.Count, graph.Split("class=\"task\"").Length - 1);
    }
}
