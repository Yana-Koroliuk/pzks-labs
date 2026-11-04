using System.Text;
using Pzks.Core.Lexing;
using Pzks.Core.Parsing;

namespace Pzks.Core.Correction;

/// <summary>Одне виправлення: позиція в початковому виразі та що саме зроблено.</summary>
public sealed record Correction(int Position, string Description);

public sealed record CorrectionResult(string Expression, IReadOnlyList<Correction> Corrections, AnalysisResult Analysis);

/// <summary>
/// Автовиправлення виразу. Іде по лексемах тим самим кінцевим автоматом, що й
/// <see cref="SyntaxAnalyzer"/>, але замість того, щоб лише повідомити про помилку,
/// застосовує до неї одне з правил:
/// <list type="bullet">
/// <item>невідомий символ — видаляється;</item>
/// <item>некоректне число — залишається найдовша коректна частина; якщо решта є ім'ям, це неявне множення: 2nb → 2*nb;</item>
/// <item>кілька операторів підряд — залишається перший; оператор на початку виразу — видаляється;</item>
/// <item>два операнди підряд — між ними вставляється '*';</item>
/// <item>оператор або кома перед ')' чи в кінці виразу — видаляються;</item>
/// <item>порожній аргумент функції, кома поза функцією, зайва ')' та порожні дужки — видаляються;</item>
/// <item>незакрита дужка — закривається; дужка іншого типу — замінюється на парну.</item>
/// </list>
/// </summary>
public sealed class AutoCorrector
{
    private sealed record Piece(TokenType Type, string Text, int Position);

    private sealed record Emitted(Piece Piece, ParserState StateAfter, Correction? Insertion);

    private sealed record OpenBracket(int OutputIndex, Piece Piece, bool IsFunction);

    private readonly List<Correction> _corrections = [];
    private readonly List<Emitted> _output = [];
    private readonly Stack<OpenBracket> _brackets = new();

    // Як і в SyntaxAnalyzer: ім'я, відділене від '(' видаленим символом, — не виклик функції.
    private bool _separatorRemoved;

    private ParserState State => _output.Count == 0 ? ParserState.Start : _output[^1].StateAfter;

    private string PreviousText => _output.Count == 0 ? "" : _output[^1].Piece.Text;

    /// <summary>Виправлений вираз і його повторний аналіз або null, якщо від виразу нічого не лишилося.</summary>
    public static CorrectionResult? Correct(string expression)
    {
        var corrector = new AutoCorrector();
        foreach (var piece in corrector.FixLexemes(Lexer.Tokenize(expression).Tokens))
            corrector.Step(piece);
        corrector.Finish(expression.Length);

        if (corrector._output.Count == 0)
            return null;

        string corrected = corrector.Render();
        var corrections = corrector._corrections.OrderBy(c => c.Position).ToList();
        return new CorrectionResult(corrected, corrections, ExpressionAnalyzer.Analyze(corrected));
    }

    // ---- лексичні виправлення --------------------------------------------------------------

    private IEnumerable<Piece> FixLexemes(IReadOnlyList<Token> tokens)
    {
        foreach (var token in tokens)
        {
            if (token.Type == TokenType.Unknown)
            {
                Add(token.Position, $"видалено невідомий символ '{token.Text}'");
                yield return new Piece(TokenType.Unknown, token.Text, token.Position);
                continue;
            }

            if (token.Type == TokenType.Number && NumberLiteral.Validate(token.Text) is not null)
            {
                foreach (var piece in SplitNumber(token))
                    yield return piece;
                continue;
            }

            yield return new Piece(token.Type, token.Text, token.Position);
        }
    }

    private IEnumerable<Piece> SplitNumber(Token token)
    {
        string word = token.Text;
        int length = ValidNumberPrefixLength(word);
        string number = word[..length];
        string rest = word[length..];

        yield return new Piece(TokenType.Number, number, token.Position);

        if (IsIdentifier(rest))
        {
            Add(token.Position, $"'{word}' → '{number}*{rest}' (неявне множення)");
            yield return new Piece(TokenType.Star, "*", token.Position + length);
            yield return new Piece(TokenType.Identifier, rest, token.Position + length);
        }
        else
        {
            Add(token.Position, $"'{word}' → '{number}' (відкинуто некоректну частину '{rest}')");
        }
    }

    /// <summary>Довжина найдовшого коректного числа на початку слова.</summary>
    private static int ValidNumberPrefixLength(string word)
    {
        if (word.Length > 2 && word[0] == '0' && word[1] is 'x' or 'X' or 'b' or 'B')
        {
            Func<char, bool> isDigit = word[1] is 'x' or 'X' ? Uri.IsHexDigit : ch => ch is '0' or '1';
            int end = 2;
            while (end < word.Length && isDigit(word[end]))
                end++;
            if (end > 2)
                return end;
        }

        int i = 0;
        while (i < word.Length && NumberLiteral.IsDigit(word[i]))
            i++;

        if (i < word.Length && word[i] == '.')
        {
            int j = i + 1;
            while (j < word.Length && NumberLiteral.IsDigit(word[j]))
                j++;
            if (j > i + 1)
                return j;
        }
        return i;
    }

    private static bool IsIdentifier(string s) =>
        s.Length > 0
        && (NumberLiteral.IsAsciiLetter(s[0]) || s[0] == '_')
        && s.All(ch => NumberLiteral.IsAsciiLetter(ch) || NumberLiteral.IsDigit(ch) || ch == '_');

    // ---- синтаксичні виправлення (той самий автомат) ---------------------------------------

    private void Step(Piece piece)
    {
        bool separatorBefore = _separatorRemoved;
        _separatorRemoved = false;

        switch (piece.Type)
        {
            case TokenType.Unknown:
                _separatorRemoved = true;
                break;

            case TokenType.Number:
            case TokenType.Identifier:
                if (State.IsOperandEnd())
                    InsertMultiplication(piece);
                Emit(piece, piece.Type == TokenType.Number ? ParserState.Number : ParserState.Identifier);
                break;

            case TokenType.LeftParen:
                if (State == ParserState.Identifier && piece.Text == "(" && !separatorBefore)
                {
                    EmitOpen(piece, isFunction: true);
                    break;
                }
                if (State.IsOperandEnd())
                    InsertMultiplication(piece);
                EmitOpen(piece, isFunction: false);
                break;

            case TokenType.RightParen:
                OnClose(piece);
                break;

            case TokenType.Comma:
                OnComma(piece);
                break;

            case TokenType.Plus:
            case TokenType.Minus:
                if (State.IsOperandEnd())
                    Emit(piece, ParserState.Operator);
                else if (State.AllowsUnary())
                    Emit(piece, ParserState.Unary);
                else
                    Add(piece.Position, $"видалено зайвий оператор '{piece.Text}' після '{PreviousText}' (залишається перший)");
                break;

            case TokenType.Star:
            case TokenType.Slash:
                if (State.IsOperandEnd())
                    Emit(piece, ParserState.Operator);
                else if (_output.Count == 0)
                    Add(piece.Position, $"видалено '{piece.Text}' на початку виразу");
                else
                    Add(piece.Position, $"видалено зайвий оператор '{piece.Text}' після '{PreviousText}' (залишається перший)");
                break;
        }
    }

    private void OnClose(Piece piece)
    {
        if (_brackets.Count == 0)
        {
            Add(piece.Position, $"видалено зайву закриваючу дужку '{piece.Text}'");
            return;
        }

        DropDangling(includeComma: true, $"перед '{piece.Text}'");

        if (State == ParserState.Open)
        {
            var open = _brackets.Peek();
            PopOutput();
            Add(open.Piece.Position, $"видалено порожні дужки '{open.Piece.Text}{piece.Text}'");
            return;
        }

        var top = _brackets.Pop();
        string closing = Lexer.ClosingFor(top.Piece.Text);
        if (piece.Text != closing)
            Add(piece.Position, $"'{piece.Text}' замінено на '{closing}' (пара до '{top.Piece.Text}' з позиції {top.Piece.Position})");
        Emit(piece with { Text = closing }, ParserState.Close);
    }

    private void OnComma(Piece piece)
    {
        if (!_brackets.Any(b => b.IsFunction))
        {
            Add(piece.Position, "видалено кому поза дужками функції");
            _separatorRemoved = true;
            return;
        }

        while (!_brackets.Peek().IsFunction)
            CloseInnermost(piece.Position);

        DropDangling(includeComma: false, "перед ','");

        if (State is ParserState.FuncOpen or ParserState.Comma)
        {
            Add(piece.Position, "видалено зайву ',' (пропущений аргумент функції)");
            return;
        }

        Emit(piece, ParserState.Comma);
    }

    private void Finish(int endPosition)
    {
        DropDangling(includeComma: true, "у кінці виразу");
        while (_brackets.Count > 0)
            CloseInnermost(endPosition);
        DropDangling(includeComma: true, "у кінці виразу");
    }

    /// <summary>Закриває найглибшу відкриту дужку (або видаляє її, якщо вона порожня).</summary>
    private void CloseInnermost(int position)
    {
        DropDangling(includeComma: true, "перед вставленою дужкою");

        var top = _brackets.Peek();
        if (State == ParserState.Open)
        {
            PopOutput();
            Add(top.Piece.Position, $"видалено незакриту порожню дужку '{top.Piece.Text}'");
            return;
        }

        _brackets.Pop();
        string closing = Lexer.ClosingFor(top.Piece.Text);
        var correction = Add(position, $"додано '{closing}' для дужки '{top.Piece.Text}' з позиції {top.Piece.Position}");
        Emit(new Piece(TokenType.RightParen, closing, position), ParserState.Close, correction);
    }

    /// <summary>Прибирає «висячі» оператори (і коми), після яких не настав операнд.</summary>
    private void DropDangling(bool includeComma, string context)
    {
        while (State is ParserState.Operator or ParserState.Unary || (includeComma && State == ParserState.Comma))
        {
            var last = _output[^1];
            PopOutput();
            if (last.Insertion is null)
                Add(last.Piece.Position, $"видалено '{last.Piece.Text}' {context}");
        }
    }

    private void InsertMultiplication(Piece before)
    {
        var correction = Add(before.Position, $"вставлено '*' між '{PreviousText}' і '{before.Text}'");
        Emit(new Piece(TokenType.Star, "*", before.Position), ParserState.Operator, correction);
    }

    private void EmitOpen(Piece piece, bool isFunction)
    {
        _brackets.Push(new OpenBracket(_output.Count, piece, isFunction));
        Emit(piece, isFunction ? ParserState.FuncOpen : ParserState.Open);
    }

    private void Emit(Piece piece, ParserState stateAfter, Correction? insertion = null) =>
        _output.Add(new Emitted(piece, stateAfter, insertion));

    /// <summary>Знімає останню лексему результату; вставлена програмою лексема забирає й своє виправлення.</summary>
    private void PopOutput()
    {
        var last = _output[^1];
        if (_brackets.Count > 0 && _brackets.Peek().OutputIndex == _output.Count - 1)
            _brackets.Pop();
        _output.RemoveAt(_output.Count - 1);
        if (last.Insertion is not null)
            _corrections.Remove(last.Insertion);
    }

    private Correction Add(int position, string description)
    {
        var correction = new Correction(position, description);
        _corrections.Add(correction);
        return correction;
    }

    private string Render()
    {
        var text = new StringBuilder();
        foreach (var (piece, stateAfter, _) in _output)
        {
            if (stateAfter == ParserState.Operator && piece.Type is TokenType.Plus or TokenType.Minus)
                text.Append($" {piece.Text} ");
            else if (piece.Type == TokenType.Comma)
                text.Append(", ");
            else
                text.Append(piece.Text);
        }
        return text.ToString();
    }
}
