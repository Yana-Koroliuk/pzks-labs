using Pzks.Core;
using Pzks.Core.Ast;
using Pzks.Core.Samples;

namespace Pzks.Tests;

public class AstBuilderTests
{
    private static ExprNode Tree(string expression) =>
        ExpressionAnalyzer.Analyze(expression).Tree ?? throw new InvalidOperationException("Вираз некоректний");

    [Fact]
    public void MultiplicationHasHigherPriorityThanAddition()
    {
        var tree = Tree("a+b*c");

        var plus = Assert.IsType<BinaryNode>(tree);
        Assert.Equal('+', plus.Operator);
        Assert.Equal(new VariableNode("a"), plus.Left);
        Assert.Equal('*', Assert.IsType<BinaryNode>(plus.Right).Operator);
    }

    [Fact]
    public void SubtractionIsLeftAssociative()
    {
        var tree = Assert.IsType<BinaryNode>(Tree("a-b-c"));

        Assert.Equal(new VariableNode("c"), tree.Right);
        Assert.Equal(new BinaryNode('-', new VariableNode("a"), new VariableNode("b")), tree.Left);
    }

    [Fact]
    public void FunctionCallKeepsAllArguments()
    {
        var function = Assert.IsType<FunctionNode>(Tree("f(a, 2, g())"));

        Assert.Equal("f", function.Name);
        Assert.Equal(3, function.Arguments.Count);
        Assert.Empty(Assert.IsType<FunctionNode>(function.Arguments[2]).Arguments);
    }

    [Fact]
    public void NumbersKeepTextAndValue()
    {
        var plus = Assert.IsType<BinaryNode>(Tree("0x1F + .5"));

        Assert.Equal(new NumberNode("0x1F", 31), plus.Left);
        Assert.Equal(new NumberNode(".5", 0.5), plus.Right);
    }

    [Theory]
    [InlineData("a-(b-c)", "a-(b-c)")]
    [InlineData("a/(b*c)", "a/(b*c)")]
    [InlineData("(a+b)*c", "(a+b)*c")]
    [InlineData("((a))+(b*c)", "a+b*c")]
    [InlineData("-(-i)/1.0", "-(-i)/1.0")]
    [InlineData("-(a + b2)*sin(x, -2.5)/c", "-(a+b2)*sin(x, -2.5)/c")]
    [InlineData("a*(-b)", "a*(-b)")]
    [InlineData("a-(-b+c)", "a-(-b+c)")]
    [InlineData("[a+b]*{c-d}", "(a+b)*(c-d)")]
    public void ToInfix_KeepsOnlyNecessaryParentheses(string expression, string expected)
    {
        Assert.Equal(expected, ExprPrinter.ToInfix(Tree(expression)));
    }

    public static TheoryData<string> ValidExpressions()
    {
        var data = new TheoryData<string>();
        foreach (var sample in MemoExpressions.AllValid)
            data.Add(sample.Expression);
        return data;
    }

    [Theory]
    [MemberData(nameof(ValidExpressions))]
    public void ToInfix_ProducesValidExpressionThatPrintsTheSame(string expression)
    {
        string infix = ExprPrinter.ToInfix(Tree(expression));

        Assert.Equal(infix, ExprPrinter.ToInfix(Tree(infix)));
    }
}
