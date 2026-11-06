using Pzks.Core;
using Pzks.Core.Factoring;
using Pzks.Core.Samples;

namespace Pzks.Tests;

public class DistributiveTests
{
    private const Laws Both = Laws.Distributive | Laws.Associative;

    private static string Memo(string id) => MemoExpressions.Lab4.Single(s => s.Id == id).Expression;

    private static IEnumerable<string> Infixes(FactoringResult result) => result.Forms.Select(f => f.Infix);

    [Fact]
    public void MemoExample_OpeningBracketsGivesTheSevenFormsOfTheMemo()
    {
        var result = FactoringExplorer.Explore(Memo("Приклад"), Laws.Distributive);

        Assert.Equal(
            ["(a-c)*k+(b-c)*t-(a-t)*x", "a*k-c*k+(b-c)*t-(a-t)*x", "(a-c)*k+b*t-c*t-(a-t)*x", "(a-c)*k+(b-c)*t-a*x+t*x",
             "a*k-c*k+b*t-c*t-(a-t)*x", "a*k-c*k+(b-c)*t-a*x+t*x", "(a-c)*k+b*t-c*t-a*x+t*x", "a*k-c*k+b*t-c*t-a*x+t*x"],
            Infixes(result));
        Assert.Equal(["0", "1Dk", "1Dt", "1Dx", "2Dk,t", "2Dk,x", "2Dt,x", "3Dk,t,x"], result.Forms.Select(f => f.Code));
        Assert.Equal(7, result.ExpandedId);
        Assert.All(result.Edges, e => Assert.Equal(Laws.Distributive, e.Law));
    }

    [Fact]
    public void MemoExample_WithFactoringContainsTheFactoredFormsOfTheMemo()
    {
        var result = FactoringExplorer.Explore(Memo("Приклад"), Both);

        Assert.Contains("a*(k-x)-c*k+b*t-c*t+t*x", Infixes(result));
        Assert.Contains("a*k-c*k+t*(b-c+x)-a*x", Infixes(result));
        Assert.Contains("a*(k-x)-c*k+t*(b-c+x)", Infixes(result));
        Assert.Contains(result.Forms, f => f.Code.Contains('D') && f.Code.Contains('A'));
    }

    [Fact]
    public void D4_FullyExpandedIsTestA6()
    {
        var result = FactoringExplorer.Explore(Memo("Д4"), Laws.Distributive);
        var expanded = result.Forms[result.ExpandedId!.Value];

        Assert.Equal(MemoExpressions.Associative.Single(s => s.Id == "А6").Expression, expanded.Infix);
        Assert.Equal("3Db,(f*5.9-q),/(d+q-w)", expanded.Code);
        Assert.Equal(6, expanded.Tiers);
        Assert.Equal(22, expanded.Operations);
    }

    [Fact]
    public void D5_FullyExpandedDiffersFromA7OnlyInTermsWithX()
    {
        var result = FactoringExplorer.Explore(Memo("Д5"), Laws.Distributive);
        var expanded = result.Forms[result.ExpandedId!.Value];

        Assert.Equal("a-b*k+b*t-b*f*f*5.9+b*f*q+b*g*f*5.9-b*g*q-b*w/p+b*y*m/p-b*y/p-x*x/(d+q-w)+9/(d+q-w)", expanded.Infix);
    }

    [Theory]
    [InlineData("Д1", "a*(b+c-1)*d", 4, 4)]
    [InlineData("Д2", "(a-c)*(b-k+1)", 3, 4)]
    [InlineData("Д3", "(1/e-d/e)/(a+b-2)", 3, 6)]
    [InlineData("Д4", "a-b*(k-t)-(f-g)*(f*5.9-q)-(f+g)/(d+q-w)", 5, 13)]
    [InlineData("Д5", "a-b*k+b*t-b*(f-g)*(f*5.9-q)-(b*w-b*y*(m-1))/p-(x*x-9)/(d+q-w)", 6, 23)]
    public void OpeningBrackets_FindsExpectedBestForm(string id, string best, int tiers, int operations)
    {
        var result = FactoringExplorer.Explore(Memo(id), Laws.Distributive);

        Assert.False(result.Truncated);
        Assert.Equal(best, result.Best!.Infix);
        Assert.Equal(tiers, result.Best.Tiers);
        Assert.Equal(operations, result.Best.Operations);
    }

    [Theory]
    [InlineData("Д1", "a*d*(b+c)-a*d", 3, 5)]
    [InlineData("Д2", "(a-c)*(b-k+1)", 3, 4)]
    [InlineData("Д3", "(1/e-d/e)/(a+b-2)", 3, 6)]
    [InlineData("Д4", "a-b*(k-t)-(f-g)*(f*5.9-q)-(f+g)/(d+q-w)", 5, 13)]
    public void OpeningAndFactoring_FindsExpectedBestForm(string id, string best, int tiers, int operations)
    {
        var result = FactoringExplorer.Explore(Memo(id), Both);

        Assert.False(result.Truncated);
        Assert.Equal(best, result.Best!.Infix);
        Assert.Equal(tiers, result.Best.Tiers);
        Assert.Equal(operations, result.Best.Operations);
    }

    [Fact]
    public void D1_BestFormOpensThenFactors()
    {
        var best = FactoringExplorer.Explore(Memo("Д1"), Both).Best!;

        Assert.Equal("3Da*dAa,d", best.Code);
        Assert.Equal(new FactoringStep("a*d", "a*(b+c-1)*d", "a*b*d+a*c*d-a*d", Laws.Distributive), best.Path[0]);
        Assert.All(best.Path.Skip(1), s => Assert.Equal(Laws.Associative, s.Law));
    }

    [Fact]
    public void D5_WithFactoringUsesLimitedSearchAndStillHasTheExpandedForm()
    {
        var result = FactoringExplorer.Explore(Memo("Д5"), Both);

        Assert.True(result.Truncated);
        Assert.Equal(8, result.Forms[0].Tiers);
        Assert.Equal(6, result.Best!.Tiers);
        Assert.True(result.Best.Operations <= 22);
        Assert.NotNull(result.ExpandedId);
        Assert.DoesNotContain("(x", result.Forms[result.ExpandedId!.Value].Infix);
    }

    [Theory]
    [InlineData("a*(b+c)", "a*b+a*c")]
    [InlineData("(a+b)*c*d", "a*c*d+b*c*d")]
    [InlineData("a*(b+c)*d", "(a*b+a*c)*d")]
    [InlineData("a*(b+c)*d", "a*(b*d+c*d)")]
    [InlineData("(a+b)*(c+d)", "a*c+a*d+b*c+b*d")]
    [InlineData("(a+b)/c", "a/c+b/c")]
    [InlineData("(1-d)/(a+b-2)/e", "(1-d)/(a*e+b*e-2*e)")]
    [InlineData("a-b*(c-d)", "a-b*c+b*d")]
    [InlineData("sin(a*(b+c))", "sin(a*b+a*c)")]
    [InlineData("(x-3)*(x+3)", "x*x-9")]
    public void OpensBracketsAsInTheMethodology(string expression, string expected)
    {
        var result = FactoringExplorer.Explore(expression, Laws.Distributive);

        Assert.Contains(expected, Infixes(result));
        Assert.All(result.Forms, f => ExpressionValue.AssertEqual(result.Original.Source!, f.Tree));
    }

    [Theory]
    [InlineData("x/(a+b)")]
    [InlineData("a+b*c")]
    [InlineData("sin(a+b)*c/(d-e)")]
    public void DivisorsAndBracketlessExpressions_AreNotOpened(string expression)
    {
        var result = FactoringExplorer.Explore(expression, Laws.Distributive);

        Assert.Single(result.Forms);
        Assert.Equal(0, result.ExpandedId);
    }

    [Fact]
    public void AssociativeOnly_NeverOpensBrackets()
    {
        var result = FactoringExplorer.Explore(Memo("Д4"));

        Assert.Equal(Laws.Associative, result.Laws);
        Assert.Null(result.ExpandedId);
        Assert.All(result.Forms, f => Assert.DoesNotContain('D', f.Code));
    }

    public static TheoryData<string, Laws> Samples()
    {
        var data = new TheoryData<string, Laws>();
        foreach (var sample in MemoExpressions.Lab4)
        {
            data.Add(sample.Id, Laws.Distributive);
            data.Add(sample.Id, Both);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void AllForms_AreEquivalentDistinctAndReachable(string id, Laws laws)
    {
        var result = FactoringExplorer.Explore(Memo(id), laws);
        var source = result.Original.Source!;

        Assert.Equal(result.Forms.Count, Infixes(result).Distinct().Count());
        foreach (var form in result.Forms)
        {
            Assert.True(ExpressionAnalyzer.Analyze(form.Infix).IsValid, form.Infix);
            ExpressionValue.AssertEqual(source, form.Tree);

            var path = result.PathTo(form.Id);
            Assert.Equal(0, path[0]);
            Assert.Equal(form.Depth, path.Count - 1);
            Assert.Equal(form.Depth, form.Path.Count);
        }
    }
}
