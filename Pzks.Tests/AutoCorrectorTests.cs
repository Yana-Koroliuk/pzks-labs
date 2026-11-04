using Pzks.Core.Correction;
using Pzks.Core.Samples;

namespace Pzks.Tests;

public class AutoCorrectorTests
{
    public static TheoryData<string> MemoExpressionsWithErrors()
    {
        var data = new TheoryData<string>();
        foreach (var sample in MemoExpressions.Lab1Single.Concat(MemoExpressions.Lab1General).Append(MemoExpressions.MemoExample))
            data.Add(sample.Expression);
        return data;
    }

    [Theory]
    [MemberData(nameof(MemoExpressionsWithErrors))]
    public void Correct_TurnsEveryMemoTestIntoValidExpression(string expression)
    {
        var correction = AutoCorrector.Correct(expression);

        Assert.NotNull(correction);
        Assert.NotEmpty(correction.Corrections);
        Assert.Empty(correction.Analysis.Errors);
        Assert.NotNull(correction.Analysis.Tree);
    }

    [Theory]
    [InlineData("*a + nb -", "a + nb")]
    [InlineData("a ++ nb /* k -+/ g", "a + nb/k - g")]
    [InlineData("a b", "a*b")]
    [InlineData("2(t)", "2*(t)")]
    [InlineData("2nb + 6.07ab", "2*nb + 6.07*ab")]
    [InlineData("0.72.3", "0.72")]
    [InlineData("(a+b", "(a + b)")]
    [InlineData(")a(", "a()")]
    [InlineData("a + ()", "a")]
    [InlineData("h(2, )", "h(2)")]
    [InlineData("f(,a)", "f(a)")]
    [InlineData("f((a, b)", "f((a), b)")]
    [InlineData("(a]", "(a)")]
    [InlineData("b&(z|t)", "b*(z*t)")]
    [InlineData("8(-)**", "8")]
    public void Correct_AppliesRules(string expression, string expected)
    {
        var correction = AutoCorrector.Correct(expression);

        Assert.NotNull(correction);
        Assert.Equal(expected, correction.Expression);
        Assert.Empty(correction.Analysis.Errors);
    }

    [Fact]
    public void Correct_DoesNothingForValidExpression()
    {
        var correction = AutoCorrector.Correct("-(a+b)*sin(x, 2)");

        Assert.NotNull(correction);
        Assert.Empty(correction.Corrections);
    }

    [Theory]
    [InlineData("*/")]
    [InlineData("()")]
    [InlineData("$#")]
    public void Correct_ReturnsNull_WhenNothingIsLeft(string expression)
    {
        Assert.Null(AutoCorrector.Correct(expression));
    }

    [Fact]
    public void Correct_ReportsPositionsInOriginalExpression()
    {
        var correction = AutoCorrector.Correct("*a + nb -");

        Assert.NotNull(correction);
        Assert.Equal([0, 8], correction.Corrections.Select(c => c.Position));
    }
}
