namespace Pzks.Core.Parsing;

/// <summary>Стани кінцевого автомата синтаксичного аналізу.</summary>
public enum ParserState
{
    /// <summary>Початок виразу.</summary>
    Start,
    /// <summary>Після унарного + або -.</summary>
    Unary,
    /// <summary>Після числа.</summary>
    Number,
    /// <summary>Після ідентифікатора (змінна або назва функції).</summary>
    Identifier,
    /// <summary>Після бінарного оператора + - * /.</summary>
    Operator,
    /// <summary>Після відкриваючої дужки звичайної групи.</summary>
    Open,
    /// <summary>Після відкриваючої дужки виклику функції.</summary>
    FuncOpen,
    /// <summary>Після коми між аргументами функції.</summary>
    Comma,
    /// <summary>Після закриваючої дужки.</summary>
    Close,
    /// <summary>Вираз завершено коректно.</summary>
    End,
}

public static class ParserStateExtensions
{
    public static string ToDisplayName(this ParserState state) => state switch
    {
        ParserState.Start => "Start (початок)",
        ParserState.Unary => "Unary (унарний знак)",
        ParserState.Number => "Number (число)",
        ParserState.Identifier => "Identifier (ім'я)",
        ParserState.Operator => "Operator (бінарна операція)",
        ParserState.Open => "Open (відкр. дужка)",
        ParserState.FuncOpen => "FuncOpen (дужка функції)",
        ParserState.Comma => "Comma (кома)",
        ParserState.Close => "Close (закр. дужка)",
        ParserState.End => "End (кінець)",
        _ => state.ToString(),
    };

    /// <summary>Стани, після яких закінчився операнд: далі очікується операція, ')' , ',' або кінець.</summary>
    public static bool IsOperandEnd(this ParserState state) =>
        state is ParserState.Number or ParserState.Identifier or ParserState.Close;

    /// <summary>Стани, у яких дозволений унарний + або -.</summary>
    public static bool AllowsUnary(this ParserState state) =>
        state is ParserState.Start or ParserState.Open or ParserState.FuncOpen or ParserState.Comma;
}
