using Pzks.Core.Ast;
using Pzks.Core.Diagnostics;
using Pzks.Core.Lexing;
using Pzks.Core.Parsing;

namespace Pzks.Core;

/// <summary>Результат аналізу виразу (ЛР1).</summary>
public sealed record AnalysisResult(
    string Expression,
    IReadOnlyList<Token> Tokens,
    IReadOnlyList<AnalysisError> Errors,
    IReadOnlyList<StateTransition> Trace,
    ExprNode? Tree)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>Повний аналіз виразу: лексичний аналіз → кінцевий автомат → дерево (якщо помилок немає).</summary>
public static class ExpressionAnalyzer
{
    public static AnalysisResult Analyze(string expression)
    {
        var lexer = Lexer.Tokenize(expression);
        var syntax = SyntaxAnalyzer.Analyze(lexer.Tokens);

        var errors = lexer.Errors
            .Concat(syntax.Errors)
            .OrderBy(e => e.Position)
            .ThenBy(e => e.Stage)
            .ToList();

        var tree = errors.Count == 0 ? AstBuilder.Build(lexer.Tokens) : null;
        return new AnalysisResult(expression, lexer.Tokens, errors, syntax.Trace, tree);
    }
}
