using Pzks.Core.Ast;
using Pzks.Core.Parallelization;
using Pzks.Core.Samples;

namespace Pzks.Tests;

public class ParallelTreeBuilderTests
{
    private static ParallelResult Build(string expression) => ParallelTreeBuilder.Build(expression);

    private static ExprNode Tree(string expression) =>
        Build(expression).Tree ?? throw new InvalidOperationException("Дерево паралельної форми не побудовано");

    private static string Memo(string id) => MemoExpressions.Lab2.Single(s => s.Id == id).Expression;

    [Theory]
    [InlineData("М1", "((a+b)+(c+d))+((e+f)+(g+h))")]
    [InlineData("М2", "((a-b)-(c+d))-((e+f)+(g+h))")]
    [InlineData("М3", "((a/b)/(c*d))/((e*f)*(g*h))")]
    [InlineData("P1", "((a+b)+(c+d))+((e+f)+(g+h))")]
    [InlineData("P2", "((a-b)+(c+d))+((e-f)+(g-h))")]
    public void EightOperands_GiveBalancedTreeOfThreeTiersAndWidthFour(string id, string expected)
    {
        var tree = Tree(Memo(id));

        Assert.Equal(expected, ExprPrinter.ToGroupedInfix(tree));
        Assert.Equal(3, TreeLevels.Tiers(tree));
        Assert.Equal([4, 2, 1], TreeLevels.Operations(tree).Select(level => level.Count));
    }

    [Fact]
    public void M4_KeepsPrioritiesAndNeedsFiveTiers()
    {
        var result = Build(Memo("М4"));

        Assert.Equal(10, TreeLevels.Tiers(result.Source!));
        Assert.Equal(5, TreeLevels.Tiers(result.Tree!));
        Assert.Equal(17, TreeLevels.OperationCount(result.Tree!));
        Assert.Equal("((a*(b-4)-(2*b)*c)-c*d)-(((a*c)*d)/((e*f)*g)+((g+h)+(i+j)))",
            ExprPrinter.ToGroupedInfix(result.Tree!));
    }

    [Fact]
    public void P2_OpensBracketsWithMinusAsInMemo()
    {
        var result = Build(Memo("P2"));

        Assert.Equal("a-b+c+d+e-f+g-h", ExprPrinter.ToInfix(result.Simplified!));
        Assert.Contains(result.Steps, s => s.Kind == StepKind.Brackets);
    }

    [Fact]
    public void MemoExample_SplitsSubtractionAndDivisionChains()
    {
        var tree = Tree("a+b*c + k - x - d - e - f/g/h/q");

        Assert.Equal(4, TreeLevels.Tiers(tree));
        Assert.Contains("(f/g)/(h*q)", ExprPrinter.ToGroupedInfix(tree));
    }

    [Theory]
    [InlineData("E1", "0.125")]
    [InlineData("E2", "-35")]
    [InlineData("E3", "39")]
    public void ConstantExpressions_AreFullyEvaluated(string id, string expected)
    {
        var tree = Tree(Memo(id));

        Assert.Equal(expected, ExprPrinter.ToInfix(tree));
        Assert.Equal(0, TreeLevels.Tiers(tree));
    }

    [Fact]
    public void S1_IsSimplified()
    {
        var result = Build(Memo("S1"));

        Assert.Equal("i-0.4+e/2", ExprPrinter.ToInfix(result.Simplified!));
        Assert.Equal("(i-0.4)+e/2", ExprPrinter.ToGroupedInfix(result.Tree!));
        Assert.Equal(2, TreeLevels.Tiers(result.Tree!));
    }

    [Fact]
    public void S2_ReportsEveryTermWithDivisionByZero()
    {
        var result = Build(Memo("S2"));

        Assert.False(result.IsValid);
        Assert.Null(result.Tree);
        Assert.Equal(["a*2/0", "b/(b+b*0-1*b)", "1/(c*2*4.76*(1-2+1))"], result.Errors.Select(e => e.Term));
    }

    [Theory]
    [InlineData("a*1*b/1/c", "(a*b)/c")]
    [InlineData("x-x+y", "y")]
    [InlineData("0/a+b", "b")]
    [InlineData("-(-i)", "i")]
    [InlineData("a-(-b)", "a+b")]
    [InlineData("(-a)*(-b)", "a*b")]
    [InlineData("2*3*a", "6*a")]
    [InlineData("1/a/1", "1/a")]
    [InlineData("a*(-b)+c", "c-a*b")]
    [InlineData("-a-b", "-(a+b)")]
    [InlineData("sin(a+b+c+d)", "sin((a+b)+(c+d))")]
    public void Simplification(string expression, string expected)
    {
        Assert.Equal(expected, ExprPrinter.ToGroupedInfix(Tree(expression)));
    }

    [Fact]
    public void Svg_DrawsOneNodePerOperationAndEveryOperand()
    {
        string svg = TreeSvg.Render(Tree(Memo("М1")));

        Assert.Equal(7, System.Text.RegularExpressions.Regex.Count(svg, "<ellipse"));
        Assert.Equal(8, System.Text.RegularExpressions.Regex.Count(svg, "font-style=\"italic\""));
    }

    [Fact]
    public void InvalidExpression_IsNotParallelized()
    {
        var result = Build("a + * b");

        Assert.False(result.Analysis.IsValid);
        Assert.Null(result.Tree);
    }

    public static TheoryData<string> ValidExpressions()
    {
        var data = new TheoryData<string>();
        foreach (var sample in MemoExpressions.AllValid.Where(s => s.Id != "S2"))
            data.Add(sample.Expression);
        return data;
    }

    [Theory]
    [MemberData(nameof(ValidExpressions))]
    public void ParallelTree_HasSameValueAndNoMoreTiers(string expression)
    {
        var result = Build(expression);
        var source = result.Source!;
        var tree = result.Tree!;

        Assert.True(TreeLevels.Tiers(tree) <= TreeLevels.Tiers(source));
        ExpressionValue.AssertEqual(source, tree);
    }

    [Theory]
    [MemberData(nameof(ValidExpressions))]
    public void ParallelTree_PrintsAsValidExpressionWithSameStructure(string expression)
    {
        string grouped = ExprPrinter.ToGroupedInfix(Tree(expression));

        Assert.Equal(grouped, ExprPrinter.ToGroupedInfix(Pzks.Core.ExpressionAnalyzer.Analyze(grouped).Tree!));
    }
}
