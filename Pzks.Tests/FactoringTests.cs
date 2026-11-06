using Pzks.Core;
using Pzks.Core.Factoring;
using Pzks.Core.Samples;

namespace Pzks.Tests;

public class FactoringTests
{
    private static string Memo(string id) => MemoExpressions.Associative.Single(s => s.Id == id).Expression;

    private static IEnumerable<string> Infixes(FactoringResult result) => result.Forms.Select(f => f.Infix);

    [Fact]
    public void A1_EachCommonFactorGivesItsOwnForm()
    {
        var result = FactoringExplorer.Explore(Memo("А1"));

        Assert.Equal(["a*b+a*c+b*c", "a*(b+c)+b*c", "b*(a+c)+a*c", "a*b+c*(a+b)"], Infixes(result));
        Assert.Equal(["1Aa", "1Ab", "1Ac"], result.Forms.Skip(1).Select(f => f.Code));
        Assert.False(result.Truncated);
    }

    [Theory]
    [InlineData("А2", "(b-2)*(a+c)", 3, 2, 5, 3)]
    [InlineData("А3", "(b-2)*(a+c)", 3, 2, 7, 3)]
    [InlineData("А4", "(a-c+2)/b", 3, 3, 5, 3)]
    [InlineData("А5", "(a-c+2)/(b-1)-t", 4, 4, 9, 5)]
    [InlineData("А6", "a-b*(k-t)-(f*5.9-q)*(f-g)-(f+g)/(d+q-w)", 6, 5, 22, 13)]
    public void MemoTests_FindExpectedBestForm(string id, string best, int tiersBefore, int tiersAfter, int operationsBefore, int operationsAfter)
    {
        var result = FactoringExplorer.Explore(Memo(id));

        Assert.False(result.Truncated);
        Assert.Equal(best, result.Best!.Infix);
        Assert.Equal(tiersBefore, result.Forms[0].Tiers);
        Assert.Equal(tiersAfter, result.Best.Tiers);
        Assert.Equal(operationsBefore, result.Forms[0].Operations);
        Assert.Equal(operationsAfter, result.Best.Operations);
    }

    [Fact]
    public void A3_TwoPathsLeadToTheSameForm()
    {
        var result = FactoringExplorer.Explore(Memo("А3"));
        var best = result.Best!;

        Assert.Equal("3Aa,c,(b-2)", best.Code);
        Assert.Equal(
        [
            new FactoringStep("a", "a*b-2*a", "a*(b-2)"),
            new FactoringStep("c", "b*c-c*2", "c*(b-2)"),
            new FactoringStep("(b-2)", "a*(b-2)+c*(b-2)", "(b-2)*(a+c)"),
        ], best.Path);
        Assert.True(result.Edges.Count(e => e.To == best.Id) >= 2);
    }

    [Fact]
    public void A4_FactorsOutCommonDivisor()
    {
        var result = FactoringExplorer.Explore(Memo("А4"));

        Assert.Contains("(a-c+2)/b", Infixes(result));
        Assert.Contains("a/b-(c-2)/b", Infixes(result));
        Assert.Equal("/b", result.Best!.Path.Single().Factor);
    }

    [Fact]
    public void A7_LongExpressionUsesLimitedSearchAndStillImproves()
    {
        var result = FactoringExplorer.Explore(Memo("А7"));

        Assert.True(result.Truncated);
        Assert.InRange(result.Forms.Count, FactoringExplorer.FullSearchForms, FactoringExplorer.MaxForms);
        Assert.Equal(7, result.Forms[0].Tiers);
        Assert.Equal(47, result.Forms[0].Operations);
        Assert.Equal(6, result.Best!.Tiers);
        Assert.True(result.Best.Operations <= 22);
        Assert.Contains(result.Forms, f => f.Infix.StartsWith("a-b*(k-t+") && f.Operations <= 20);
    }

    public static TheoryData<string> Samples()
    {
        var data = new TheoryData<string>();
        foreach (var sample in MemoExpressions.Associative)
            data.Add(sample.Id);
        return data;
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void AllForms_AreEquivalentDistinctAndValid(string id)
    {
        var result = FactoringExplorer.Explore(Memo(id));
        var source = result.Original.Source!;

        Assert.Equal(result.Forms.Count, Infixes(result).Distinct().Count());
        foreach (var form in result.Forms)
        {
            Assert.True(ExpressionAnalyzer.Analyze(form.Infix).IsValid, form.Infix);
            ExpressionValue.AssertEqual(source, form.Tree);
        }
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void EveryFormIsReachableFromTheOriginal(string id)
    {
        var result = FactoringExplorer.Explore(Memo(id));

        foreach (var form in result.Forms)
        {
            var path = result.PathTo(form.Id);
            Assert.Equal(0, path[0]);
            Assert.Equal(form.Depth, path.Count - 1);
            Assert.Equal(form.Depth, form.Path.Count);
        }
        Assert.All(result.Edges, e => Assert.True(e.From < result.Forms.Count && e.To < result.Forms.Count));
    }

    [Theory]
    [InlineData("sin(a*b+a*c)", "sin(a*(b+c))")]
    [InlineData("x*(a*b+a*c)", "x*a*(b+c)")]
    [InlineData("2*a+2*b", "2*(a+b)")]
    [InlineData("a*b-a", "a*(b-1)")]
    [InlineData("a*b*c+a*b*d", "a*b*(c+d)")]
    [InlineData("a/(b+c)+d/(b+c)", "(a+d)/(b+c)")]
    public void FactorsOutAnywhereInTheExpression(string expression, string expected)
    {
        var result = FactoringExplorer.Explore(expression);

        Assert.Contains(expected, Infixes(result));
        Assert.All(result.Forms, f => ExpressionValue.AssertEqual(result.Original.Source!, f.Tree));
    }

    [Fact]
    public void NegativeGroup_KeepsSignOutsideBrackets()
    {
        var result = FactoringExplorer.Explore("d-a*b-a*c");

        Assert.Equal(["d-a*b-a*c", "d-a*(b+c)"], Infixes(result));
        Assert.Equal(new FactoringStep("a", "-a*b-a*c", "-a*(b+c)"), result.Forms[1].Path.Single());
    }

    [Fact]
    public void NoCommonFactors_GiveOnlyTheOriginalForm()
    {
        var result = FactoringExplorer.Explore("a+b*c-d/e");

        Assert.Single(result.Forms);
        Assert.Empty(result.Edges);
        Assert.Equal(0, result.Best!.Id);
    }

    [Theory]
    [InlineData("a*b+*a*c")]
    [InlineData("a/(b-b)+a*c")]
    public void InvalidExpressions_GiveNoForms(string expression)
    {
        var result = FactoringExplorer.Explore(expression);

        Assert.False(result.IsValid);
        Assert.Empty(result.Forms);
        Assert.Null(result.Best);
    }

    [Fact]
    public void Graph_ShowsAllFormsOrPathsToTheBest()
    {
        var small = FactoringExplorer.Explore(Memo("А3"));
        Assert.Equal(small.Forms.Count, FormGraphSvg.Nodes(small).Count);

        var large = FactoringExplorer.Explore(Memo("А6"));
        var nodes = FormGraphSvg.Nodes(large);
        Assert.InRange(nodes.Count, 1, FormGraphSvg.MaxNodes);
        Assert.Subset(nodes.ToHashSet(), large.PathTo(large.Best!.Id).ToHashSet());

        string svg = FormGraphSvg.Render(large);
        Assert.StartsWith("<svg", svg);
        Assert.Contains(large.Best.Infix, svg);
    }
}
