using System.Globalization;
using Pzks.Core.Ast;

namespace Pzks.Core.Parallelization;

internal enum ChainKind
{
    Sum,
    Product,
}

/// <summary>
/// Проміжна n-арна форма виразу. Source — запис вихідного підвиразу, з якого вона отримана
/// (для повідомлень про перетворення та помилки).
/// </summary>
internal abstract record Form(string Source);

internal sealed record NumberForm(decimal Value, string Text, string Source) : Form(Source)
{
    public static NumberForm Of(decimal value, string source) => new(value, Format(value), source);

    public static string Format(decimal value) =>
        value.ToString("0.############################", CultureInfo.InvariantCulture);
}

/// <summary>Змінна або число, яке не можна обчислювати точно (виходить за межі decimal).</summary>
internal sealed record LeafForm(ExprNode Node, string Source) : Form(Source);

internal sealed record CallForm(string Name, IReadOnlyList<Form> Arguments, string Source) : Form(Source);

/// <summary>
/// Ланцюжок однакового пріоритету: сума доданків (Inverse — доданок віднімається)
/// або добуток множників (Inverse — на множник ділять).
/// </summary>
internal sealed record ChainForm(ChainKind Kind, IReadOnlyList<Part> Parts, string Source) : Form(Source);

internal sealed record Part(bool Inverse, Form Item);

internal static class FormExtensions
{
    public static char Operator(this ChainKind kind, bool inverse) => (kind, inverse) switch
    {
        (ChainKind.Sum, false) => '+',
        (ChainKind.Sum, true) => '-',
        (ChainKind.Product, false) => '*',
        _ => '/',
    };

    /// <summary>Ключ для порівняння підвиразів на рівність (для взаємного знищення доданків).</summary>
    public static string Key(this Form form) => form switch
    {
        NumberForm n => NumberForm.Format(n.Value),
        LeafForm l => l.Source,
        CallForm c => $"{c.Name}({string.Join(",", c.Arguments.Select(Key))})",
        ChainForm ch => "(" + string.Concat(ch.Parts.Select((p, i) =>
            (i == 0 && !p.Inverse ? "" : ch.Kind.Operator(p.Inverse).ToString()) + p.Item.Key())) + ")",
        _ => throw new ArgumentOutOfRangeException(nameof(form)),
    };

    public static bool IsNumber(this Form form, decimal value) => form is NumberForm n && n.Value == value;
}
