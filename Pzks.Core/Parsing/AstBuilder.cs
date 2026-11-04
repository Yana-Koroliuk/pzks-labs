using Pzks.Core.Ast;
using Pzks.Core.Lexing;

namespace Pzks.Core.Parsing;

/// <summary>
/// Будує дерево виразу методом рекурсивного спуску з урахуванням пріоритетів:
/// <code>
/// expr    ::= term (('+' | '-') term)*
/// term    ::= unary (('*' | '/') unary)*
/// unary   ::= ('+' | '-') unary | primary
/// primary ::= number | identifier | identifier '(' [expr (',' expr)*] ')' | '(' expr ')' | '[' expr ']' | '{' expr '}'
/// </code>
/// Викликається лише для лексем, у яких автомат не знайшов помилок.
/// </summary>
public sealed class AstBuilder
{
    private readonly IReadOnlyList<Token> _tokens;
    private int _index;

    private AstBuilder(IReadOnlyList<Token> tokens) => _tokens = tokens;

    public static ExprNode Build(IReadOnlyList<Token> tokens)
    {
        var builder = new AstBuilder(tokens);
        var root = builder.ParseExpression();
        if (builder.Current is { } extra)
            throw new InvalidOperationException($"Зайва лексема '{extra.Text}' на позиції {extra.Position}");
        return root;
    }

    private Token? Current => _index < _tokens.Count ? _tokens[_index] : null;

    private Token Next()
    {
        var token = Current ?? throw new InvalidOperationException("Неочікуваний кінець виразу");
        _index++;
        return token;
    }

    private bool Match(TokenType type)
    {
        if (Current?.Type != type)
            return false;
        _index++;
        return true;
    }

    private void Expect(TokenType type)
    {
        if (!Match(type))
            throw new InvalidOperationException($"Очікувалась лексема {type.ToDisplayName()}");
    }

    private ExprNode ParseExpression()
    {
        var left = ParseTerm();
        while (Current?.Type is TokenType.Plus or TokenType.Minus)
        {
            char op = Next().Text[0];
            left = new BinaryNode(op, left, ParseTerm());
        }
        return left;
    }

    private ExprNode ParseTerm()
    {
        var left = ParseUnary();
        while (Current?.Type is TokenType.Star or TokenType.Slash)
        {
            char op = Next().Text[0];
            left = new BinaryNode(op, left, ParseUnary());
        }
        return left;
    }

    private ExprNode ParseUnary()
    {
        if (Current?.Type is TokenType.Plus or TokenType.Minus)
        {
            char op = Next().Text[0];
            return new UnaryNode(op, ParseUnary());
        }
        return ParsePrimary();
    }

    private ExprNode ParsePrimary()
    {
        var token = Next();
        switch (token.Type)
        {
            case TokenType.Number:
                return new NumberNode(token.Text, NumberLiteral.Value(token.Text));

            case TokenType.Identifier when Current is { Type: TokenType.LeftParen, Text: "(" }:
                _index++;
                var arguments = new List<ExprNode>();
                if (!Match(TokenType.RightParen))
                {
                    do
                    {
                        arguments.Add(ParseExpression());
                    } while (Match(TokenType.Comma));
                    Expect(TokenType.RightParen);
                }
                return new FunctionNode(token.Text, arguments);

            case TokenType.Identifier:
                return new VariableNode(token.Text);

            case TokenType.LeftParen:
                var inner = ParseExpression();
                Expect(TokenType.RightParen);
                return inner;

            default:
                throw new InvalidOperationException($"Неочікувана лексема '{token.Text}' на позиції {token.Position}");
        }
    }
}
