using Pzks.Core.Optimization;
using Pzks.Core.Samples;
using Pzks.Core.Simulation;

namespace Pzks.Tests;

public class FormOptimizerTests
{
    private const string K4 = "a + b*c + d + e*f*g + h*i + j*(k + L + m*(n-p*q+r) - s*t)";

    private static OptimizationResult Full(string expression, VliwParameters? parameters = null) =>
        FormOptimizer.Optimize(expression, parameters, directed: false, study: false);

    public static TheoryData<string> Lab6Expressions()
    {
        var data = new TheoryData<string>();
        foreach (var sample in MemoExpressions.Lab6.Where(s => !s.Id.StartsWith("Ск")))
            data.Add(sample.Expression);
        data.Add(K4);
        return data;
    }

    [Theory]
    [MemberData(nameof(Lab6Expressions))]
    public void OriginalForm_IsTheLr5Model(string expression)
    {
        var result = Full(expression);
        var lr5 = VliwModel.Run(expression).Schedule!;

        Assert.True(result.IsValid);
        Assert.Equal(lr5.SequentialTime, result.Original.Sequential);
        Assert.Equal(lr5.ParallelTime, result.Original.Parallel);
    }

    [Theory]
    [MemberData(nameof(Lab6Expressions))]
    public void Optimal_HasTheSmallestParallelTime(string expression)
    {
        var result = Full(expression);

        Assert.Equal(result.Factoring.Forms.Count, result.Runs.Count);
        Assert.Equal(result.Runs.Min(r => r.Parallel), result.Optimal.Parallel);
        Assert.True(result.Optimal.Parallel <= result.Original.Parallel);
        Assert.Equal(result.Runs.Select(r => r.Parallel).Order(), result.Runs.Select(r => r.Parallel));
    }

    [Fact]
    public void MemoExample_HasSeveralOptimalForms()
    {
        var result = Full("(a-c)*k+(b-c)*t-(a-t)*x");

        Assert.Equal(3, result.OptimalForms.Count);
        Assert.All(result.OptimalForms, r => Assert.Equal(26, r.Parallel));
        Assert.True(result.Runs[3].Parallel > 26);
        // З однаковим Tp обрано вихідну форму: операцій стільки ж, а перетворень немає.
        Assert.Same(result.Original, result.Optimal);
    }

    [Fact]
    public void A2_FactoringGivesTheOptimalForm()
    {
        var result = Full("a*(b-2)+c*(b-2)");

        Assert.Equal(16, result.Original.Parallel);
        Assert.Equal("(b-2)*(a+c)", result.Optimal.Form.Infix);
        Assert.Equal(12, result.Optimal.Parallel);
        // Найбільше Ky має вихідна форма: у неї більше операцій і більший T1, але виконується вона довше.
        Assert.Equal(0, result.MaxSpeedup.Form.Id);
        Assert.True(result.MaxSpeedup.Speedup > result.Optimal.Speedup);
    }

    [Fact]
    public void K4_FastestFormIsNotTheOneWithFewestTiers()
    {
        var result = Full(K4);

        Assert.Equal(54, result.Original.Parallel);
        Assert.Equal(52, result.Optimal.Parallel);
        Assert.Equal(56, result.ByTiers.Parallel);
        Assert.True(result.ByTiers.Form.Tiers < result.Optimal.Form.Tiers);
    }

    [Fact]
    public void OneProcessor_OptimalFormHasTheSmallestSequentialTime()
    {
        var result = Full("(a-c)*k+(b-c)*t-(a-t)*x", new VliwParameters(Processors: 1));

        Assert.All(result.Runs, r => Assert.Equal(r.Sequential, r.Parallel));
        Assert.Equal(result.Runs.Min(r => r.Sequential), result.Optimal.Parallel);
    }

    [Theory]
    [InlineData("(a-c)*k+(b-c)*t-(a-t)*x")]
    [InlineData("a-b*(k-t)-(f-g)*(f*5.9-q)-(f+g)/(d+q-w)")]
    [InlineData(K4)]
    public void Directed_FindsTheOptimumWithFewerSimulations(string expression)
    {
        var full = Full(expression);
        var directed = FormOptimizer.Directed(expression, new VliwParameters());

        Assert.Equal(full.Optimal.Parallel, directed.Best.Parallel);
        Assert.True(directed.Evaluated < full.Runs.Count);
        Assert.Equal(directed.Levels.Select(l => l.BestTime).OrderDescending(), directed.Levels.Select(l => l.BestTime));
        Assert.Equal(directed.Evaluated, directed.Levels[^1].Evaluated);
    }

    [Fact]
    public void Study_ComparesTheChosenFormOnOtherSystems()
    {
        var result = FormOptimizer.Optimize("a*(b+c-1)*d");

        Assert.Equal(FormOptimizer.StudyParameters(result.Parameters).Count, result.Study.Count);
        Assert.All(result.Study, s => Assert.True(s.Chosen.Parallel >= s.Best.Parallel));
        var variant = result.Study.Single(s => s.Parameters == result.Parameters);
        Assert.Equal(result.Optimal.Parallel, variant.Best.Parallel);
        Assert.Equal(result.Optimal.Parallel, variant.Chosen.Parallel);
    }

    [Fact]
    public void Functions_CannotBeSimulated()
    {
        var result = Full("sin(x)+a*(b+c)");

        Assert.True(result.Factoring.IsValid);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("функція"));
    }

    [Fact]
    public void Lr1Errors_StopTheOptimization()
    {
        var result = Full("a*(b+");

        Assert.False(result.Factoring.IsValid);
        Assert.False(result.IsValid);
        Assert.Empty(result.Runs);
    }
}
