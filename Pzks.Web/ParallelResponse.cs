using Pzks.Core.Ast;
using Pzks.Core.Correction;
using Pzks.Core.Diagnostics;
using Pzks.Core.Parallelization;
using Pzks.Core.Samples;

namespace Pzks.Web;

public sealed record ParallelRequest(string? Expression);

public sealed record SampleResponse(string Id, string Expression, string Description, string Group)
{
    // Групи — кольори тестів у пам'ятці.
    public static SampleResponse From(SampleExpression sample) => new(sample.Id, sample.Expression, sample.Description,
        sample.Id[0] switch
        {
            'М' => "mandatory",
            'P' => "brackets",
            'E' => "constants",
            'А' => "associative",
            _ => "simplification",
        });
}

public sealed record Lr1Error(string Stage, int Position, int Length, string Message);

public sealed record CorrectionInfo(string Expression, bool Valid, IReadOnlyList<Correction> Corrections);

public sealed record StepInfo(string Kind, string Description);

public sealed record TreeInfo(
    string Infix,
    int Tiers,
    int Operations,
    int Width,
    IReadOnlyList<IReadOnlyList<NumberedOperation>> Levels,
    string Svg)
{
    public static TreeInfo From(ExprNode tree, string infix) => new(
        infix,
        TreeLevels.Tiers(tree),
        TreeLevels.OperationCount(tree),
        TreeLevels.Width(tree),
        TierDiagram.Operations(tree),
        TreeSvg.Render(tree));
}

public sealed record ParallelResponse(
    string Expression,
    bool Valid,
    IReadOnlyList<Lr1Error> Lr1Errors,
    CorrectionInfo? Correction,
    IReadOnlyList<StepInfo> Steps,
    IReadOnlyList<ParallelError> Errors,
    TreeInfo? Source,
    string? Simplified,
    TreeInfo? Parallel)
{
    public static ParallelResponse From(ParallelResult result)
    {
        var analysis = result.Analysis;
        var lr1Errors = analysis.Errors
            .Select(e => new Lr1Error(e.Stage == ErrorStage.Lexical ? "lexical" : "syntax", e.Position, e.Length, e.Message))
            .ToList();

        CorrectionInfo? correction = null;
        if (!analysis.IsValid && AutoCorrector.Correct(analysis.Expression) is { } fixedExpression)
            correction = new CorrectionInfo(fixedExpression.Expression, fixedExpression.Analysis.IsValid, fixedExpression.Corrections);

        return new ParallelResponse(
            analysis.Expression,
            result.IsValid,
            lr1Errors,
            correction,
            result.Steps.Select(s => new StepInfo(s.Kind.ToString().ToLowerInvariant(), s.Description)).ToList(),
            result.Errors,
            result.Source is { } source ? TreeInfo.From(source, ExprPrinter.ToInfix(source)) : null,
            result.Simplified is { } simplified ? ExprPrinter.ToInfix(simplified) : null,
            result.Tree is { } tree ? TreeInfo.From(tree, ExprPrinter.ToGroupedInfix(tree)) : null);
    }
}
