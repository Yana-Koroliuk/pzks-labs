namespace Pzks.Core.Diagnostics;

public enum ErrorStage
{
    Lexical,
    Syntax,
}

/// <summary>
/// Помилка у виразі: етап, на якому знайдена, позиція (з 0), довжина фрагмента і опис.
/// </summary>
public sealed record AnalysisError(ErrorStage Stage, int Position, int Length, string Message);
