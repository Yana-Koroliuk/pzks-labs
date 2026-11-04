using Pzks.Core.Ast;

namespace Pzks.Core.Parallelization;

public enum StepKind
{
    /// <summary>Розкриття дужок із + та -.</summary>
    Brackets,

    /// <summary>Спрощення: множення/ділення на 1, додавання 0, множення на 0, подвійний мінус.</summary>
    Simplification,

    /// <summary>Обчислення константних підвиразів.</summary>
    Constants,

    /// <summary>Перебудова ланцюжка операцій у збалансоване дерево.</summary>
    Regrouping,
}

public sealed record Transformation(StepKind Kind, string Description);

/// <summary>Недопустима операція, через яку дерево паралельної форми не будується.</summary>
public sealed record ParallelError(string Term, string Message);

public sealed record ParallelResult(
    AnalysisResult Analysis,
    IReadOnlyList<Transformation> Steps,
    IReadOnlyList<ParallelError> Errors,
    ExprNode? Simplified,
    ExprNode? Tree)
{
    /// <summary>Дерево виразу з ЛР1 (послідовна форма).</summary>
    public ExprNode? Source => Analysis.Tree;

    public bool IsValid => Analysis.IsValid && Errors.Count == 0 && Tree is not null;
}
