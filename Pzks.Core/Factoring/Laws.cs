namespace Pzks.Core.Factoring;

/// <summary>
/// Закони перетворення у позначеннях пам'ятки: A — асоціативний (винесення за дужки, ЛР3),
/// D — дистрибутивний (розкриття дужок, ЛР4).
/// </summary>
[Flags]
public enum Laws
{
    Associative = 1,
    Distributive = 2,
}

public static class LawsExtensions
{
    public static char Letter(this Laws law) => law == Laws.Distributive ? 'D' : 'A';
}
