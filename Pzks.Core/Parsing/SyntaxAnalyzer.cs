using Pzks.Core.Diagnostics;
using Pzks.Core.Lexing;

namespace Pzks.Core.Parsing;

/// <summary>Один крок автомата: лексема (null — кінець виразу), стан до і після, примітка про помилку.</summary>
public sealed record StateTransition(Token? Token, ParserState From, ParserState To, string? Note);

public sealed record SyntaxResult(IReadOnlyList<AnalysisError> Errors, IReadOnlyList<StateTransition> Trace);

/// <summary>
/// Синтаксичний аналізатор на основі кінцевого автомата. Іде по лексемах і в кожному стані
/// перевіряє, чи дозволена наступна лексема. Неочікувана лексема фіксується як помилка і
/// пропускається без зміни стану, тому за один прохід знаходяться всі помилки.
/// Парність і тип дужок ( [ { перевіряються окремим стеком.
/// </summary>
public sealed class SyntaxAnalyzer
{
    private sealed record OpenBracket(Token Token, bool IsFunction);

    private readonly IReadOnlyList<Token> _tokens;
    private readonly List<AnalysisError> _errors = [];
    private readonly List<StateTransition> _trace = [];
    private readonly Stack<OpenBracket> _brackets = new();

    private ParserState _state = ParserState.Start;
    private Token? _previous;

    // Невідомий символ або кома поза функцією між двома операндами вже дали помилку,
    // тому наступний операнд не повинен додатково повідомляти «пропущено оператор».
    private bool _separatorSkipped;

    private SyntaxAnalyzer(IReadOnlyList<Token> tokens) => _tokens = tokens;

    public static SyntaxResult Analyze(IReadOnlyList<Token> tokens)
    {
        var analyzer = new SyntaxAnalyzer(tokens);
        analyzer.Run();
        return new SyntaxResult(analyzer._errors, analyzer._trace);
    }

    private void Run()
    {
        foreach (var token in _tokens)
        {
            var from = _state;
            int errorsBefore = _errors.Count;

            Step(token);

            string? note = token.Type == TokenType.Unknown
                ? "невідомий символ — пропущено"
                : _errors.Count > errorsBefore ? _errors[^1].Message : null;
            _trace.Add(new StateTransition(token, from, _state, note));
        }

        Finish();
    }

    private void Step(Token token)
    {
        bool separatorBefore = _separatorSkipped;
        _separatorSkipped = false;

        switch (token.Type)
        {
            case TokenType.Number:
            case TokenType.Identifier:
                OnOperand(token, separatorBefore);
                break;
            case TokenType.LeftParen:
                OnLeftParen(token, separatorBefore);
                break;
            case TokenType.RightParen:
                OnRightParen(token);
                break;
            case TokenType.Comma:
                OnComma(token);
                break;
            case TokenType.Plus:
            case TokenType.Minus:
                OnPlusMinus(token);
                break;
            case TokenType.Star:
            case TokenType.Slash:
                OnStarSlash(token);
                break;
            case TokenType.Unknown:
                _separatorSkipped = true;
                break;
        }
    }

    private void OnOperand(Token token, bool separatorBefore)
    {
        if (_state.IsOperandEnd() && !separatorBefore)
            Error(token, $"Пропущено оператор між '{_previous!.Text}' і '{token.Text}'");

        Accept(token, token.Type == TokenType.Number ? ParserState.Number : ParserState.Identifier);
    }

    private void OnLeftParen(Token token, bool separatorBefore)
    {
        bool round = token.Text == "(";
        if (_state == ParserState.Identifier && round && !separatorBefore)
        {
            _brackets.Push(new OpenBracket(token, IsFunction: true));
            Accept(token, ParserState.FuncOpen);
            return;
        }

        if (_state.IsOperandEnd() && !separatorBefore)
        {
            if (_state == ParserState.Number && round)
                Error(_previous!, $"Число '{_previous!.Text}' не може бути назвою функції: пропущено оператор перед '('");
            else if (_state == ParserState.Identifier)
                Error(token, $"Пропущено оператор між '{_previous!.Text}' і '{token.Text}': аргументи функції записуються лише в круглих дужках");
            else
                Error(token, $"Пропущено оператор між '{_previous!.Text}' і '{token.Text}'");
        }

        _brackets.Push(new OpenBracket(token, IsFunction: false));
        Accept(token, ParserState.Open);
    }

    private void OnRightParen(Token token)
    {
        if (_brackets.Count == 0)
        {
            Error(token, $"Зайва закриваюча дужка '{token.Text}': немає відповідної відкриваючої");
            return;
        }

        var open = _brackets.Pop();
        string expectedClosing = Lexer.ClosingFor(open.Token.Text);
        if (token.Text != expectedClosing)
        {
            Error(token, $"Дужка '{open.Token.Text}' з позиції {open.Token.Position} закрита дужкою '{token.Text}', " +
                         $"очікувалась '{expectedClosing}'");
        }

        switch (_state)
        {
            case ParserState.Open:
                Error(open.Token.Position, token.Position - open.Token.Position + 1,
                    $"Порожні дужки '{open.Token.Text}{token.Text}': всередині має бути вираз");
                break;
            case ParserState.Comma:
                Error(token, $"Пропущено аргумент функції: після ',' знайдено '{token.Text}'");
                break;
            case ParserState.Operator:
            case ParserState.Unary:
                Error(token, $"Неочікувана '{token.Text}' після оператора '{_previous!.Text}'");
                break;
        }

        Accept(token, ParserState.Close);
    }

    private void OnComma(Token token)
    {
        if (!_brackets.Any(b => b.IsFunction))
        {
            Error(token, "Кома ',' поза дужками функції: кома розділяє лише аргументи функції");
            _separatorSkipped = true;
            return;
        }

        // Кома завершує аргумент функції, тому всі звичайні дужки, відкриті всередині
        // цього аргументу і не закриті до коми, — незакриті: f((a+b, c).
        while (!_brackets.Peek().IsFunction)
            Unclosed(_brackets.Pop().Token);

        switch (_state)
        {
            case ParserState.FuncOpen:
                Error(token, "Пропущено аргумент функції перед ','");
                break;
            case ParserState.Comma:
                Error(token, "Пропущено аргумент функції між двома ','");
                break;
            case ParserState.Operator:
            case ParserState.Unary:
                Error(token, $"Неочікувана ',' після оператора '{_previous!.Text}'");
                break;
        }

        Accept(token, ParserState.Comma);
    }

    private void OnPlusMinus(Token token)
    {
        if (_state.IsOperandEnd())
            Accept(token, ParserState.Operator);
        else if (_state.AllowsUnary())
            Accept(token, ParserState.Unary);
        else
            Error(token, $"Неочікуваний оператор '{token.Text}' після оператора '{_previous!.Text}'");
    }

    private void OnStarSlash(Token token)
    {
        if (_state.IsOperandEnd())
        {
            Accept(token, ParserState.Operator);
            return;
        }

        Error(token, _state switch
        {
            ParserState.Start => $"Вираз не може починатися з оператора '{token.Text}'",
            ParserState.Open or ParserState.FuncOpen => $"Оператор '{token.Text}' не може стояти одразу після '{_previous!.Text}'",
            ParserState.Comma => $"Аргумент функції не може починатися з оператора '{token.Text}'",
            _ => $"Неочікуваний оператор '{token.Text}' після оператора '{_previous!.Text}'",
        });
    }

    private void Finish()
    {
        var from = _state;
        int errorsBefore = _errors.Count;

        if (_state.IsOperandEnd())
        {
            _state = ParserState.End;
        }
        else if (_previous is null)
        {
            if (_tokens.Count == 0)
                Error(0, 0, "Порожній вираз");
            else
                Error(_tokens[^1], "Вираз не містить жодного операнда");
        }
        else
        {
            var last = _tokens[^1];
            Error(last.Position + last.Length, 1, _state switch
            {
                ParserState.Operator or ParserState.Unary =>
                    $"Неочікуваний кінець виразу після оператора '{_previous.Text}'",
                ParserState.Comma => "Неочікуваний кінець виразу після ','",
                _ => $"Неочікуваний кінець виразу після '{_previous.Text}'",
            });
        }

        foreach (var open in _brackets)
            Unclosed(open.Token);

        _trace.Add(new StateTransition(null, from, _state,
            _errors.Count > errorsBefore ? string.Join("; ", _errors.Skip(errorsBefore).Select(e => e.Message)) : null));
    }

    private void Accept(Token token, ParserState next)
    {
        _state = next;
        _previous = token;
    }

    private void Unclosed(Token open) =>
        Error(open, $"Відкриваюча дужка '{open.Text}' не має парної закриваючої '{Lexer.ClosingFor(open.Text)}'");

    private void Error(Token token, string message) => Error(token.Position, token.Length, message);

    private void Error(int position, int length, string message) =>
        _errors.Add(new AnalysisError(ErrorStage.Syntax, position, length, message));
}
