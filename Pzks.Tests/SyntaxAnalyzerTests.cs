using Pzks.Core;
using Pzks.Core.Parsing;
using Pzks.Core.Samples;

namespace Pzks.Tests;

public class SyntaxAnalyzerTests
{
    private static int[] ErrorPositions(string expression) =>
        ExpressionAnalyzer.Analyze(expression).Errors.Select(e => e.Position).ToArray();

    public static TheoryData<string> ValidExpressions()
    {
        var data = new TheoryData<string>();
        foreach (var sample in MemoExpressions.AllValid)
            data.Add(sample.Expression);
        data.Add("a*b+c*d+g*(l+h+e+f)");
        data.Add("-(a + b2)*sin(x, 2.5)/c");
        data.Add("g() + f(a, -b, (c))");
        return data;
    }

    [Theory]
    [MemberData(nameof(ValidExpressions))]
    public void TestExpressionsOfLabs2To4_HaveNoErrors(string expression)
    {
        var result = ExpressionAnalyzer.Analyze(expression);

        Assert.Empty(result.Errors);
        Assert.NotNull(result.Tree);
        Assert.Equal(ParserState.End, result.Trace[^1].To);
    }

    [Fact]
    public void MemoExample_MatchesReferenceOutputFromMemo()
    {
        Assert.Equal([4, 10, 18, 24, 32, 37, 42, 44, 47, 49, 50], ErrorPositions(MemoExpressions.MemoExample.Expression));
    }

    [Theory]
    [InlineData("*a + nb -", new[] { 0, 9 })]
    [InlineData("a ++ nb /* k -+/ g", new[] { 3, 9, 14, 15 })]
    [InlineData("a^b$c - d#h + q%t + !b&(z|t)", new[] { 1, 3, 9, 15, 20, 22, 25 })]
    [InlineData("x + var1 + var_2 + _var_3 + var#4 + var!5 + 6var_ + $7 + ?8", new[] { 31, 39, 44, 52, 57 })]
    [InlineData("125 + 2nb - 0xAB * 0x0R + 0b010 * 0b20 + ABh * 0Rh + 010b*20b", new[] { 6, 22, 36, 48, 58 })]
    [InlineData("0.71/0.72.3 + .3 + 127.0.0.1*8. + 6.07ab - 9f.89hgt", new[] { 9, 24, 34, 43 })]
    [InlineData(")a+b( -(g+h)(g-k))*()) + (-b(t-2*x*(5) + A[7][2-x]", new[] { 0, 12, 19, 21, 25, 28, 42, 45 })]
    [InlineData("2(t) - f2(t) + g()/h(2, )*func(-t/q, f(4-t), -(x+2)*(y-2))", new[] { 0, 24 })]
    public void MemoSingleTests_FindExpectedErrors(string expression, int[] positions)
    {
        Assert.Equal(positions, ErrorPositions(expression));
    }

    [Fact]
    public void MemoGeneralTests_AllHaveErrors()
    {
        Assert.All(MemoExpressions.Lab1General, sample => Assert.NotEmpty(ErrorPositions(sample.Expression)));
    }

    [Theory]
    [InlineData("", "Порожній вираз")]
    [InlineData(")a", "Зайва закриваюча дужка")]
    [InlineData("(a", "не має парної закриваючої")]
    [InlineData("a+()", "Порожні дужки")]
    [InlineData("a b", "Пропущено оператор між 'a' і 'b'")]
    [InlineData("(a)(b)", "Пропущено оператор між ')' і '('")]
    [InlineData("2(t)", "не може бути назвою функції")]
    [InlineData("a*-b", "Неочікуваний оператор '-' після оператора '*'")]
    [InlineData("(*a)", "не може стояти одразу після '('")]
    [InlineData("f(*a)", "не може стояти одразу після '('")]
    [InlineData("f(a, *b)", "Аргумент функції не може починатися")]
    [InlineData("f(a, )", "Пропущено аргумент функції")]
    [InlineData("f(, a)", "Пропущено аргумент функції перед ','")]
    [InlineData("f(a,, b)", "Пропущено аргумент функції між двома ','")]
    [InlineData("a, b", "Кома ',' поза дужками функції")]
    [InlineData("(a+)", "Неочікувана ')' після оператора '+'")]
    [InlineData("a/", "Неочікуваний кінець виразу після оператора '/'")]
    [InlineData("(a]", "Дужка '(' з позиції 0 закрита дужкою ']', очікувалась ')'")]
    [InlineData("{a+[b)}", "Дужка '[' з позиції 3 закрита дужкою ')', очікувалась ']'")]
    [InlineData("[a", "Відкриваюча дужка '[' не має парної закриваючої ']'")]
    [InlineData("a+{}", "Порожні дужки '{}'")]
    [InlineData("A[i]", "аргументи функції записуються лише в круглих дужках")]
    [InlineData("(a)[b]", "Пропущено оператор між ')' і '['")]
    public void Analyze_ReportsSingleError(string expression, string expectedMessagePart)
    {
        var error = Assert.Single(ExpressionAnalyzer.Analyze(expression).Errors);

        Assert.Contains(expectedMessagePart, error.Message);
    }

    [Theory]
    [InlineData("[a+b]*{c-d}/(e)")]
    [InlineData("{[(a)]}")]
    [InlineData("x + 0x6AB4 + 6AB4h + 0b0110 + 0110b + .71 + 172. + ABh")]
    public void Analyze_AcceptsOptionalFormatsFromMemo(string expression)
    {
        Assert.Empty(ExpressionAnalyzer.Analyze(expression).Errors);
    }

    [Fact]
    public void Trace_ShowsStateTransitions()
    {
        var trace = ExpressionAnalyzer.Analyze("-f(x)").Trace;

        Assert.Equal(
            [
                (ParserState.Start, ParserState.Unary),
                (ParserState.Unary, ParserState.Identifier),
                (ParserState.Identifier, ParserState.FuncOpen),
                (ParserState.FuncOpen, ParserState.Identifier),
                (ParserState.Identifier, ParserState.Close),
                (ParserState.Close, ParserState.End),
            ],
            trace.Select(t => (t.From, t.To)));
    }
}
