using Pzks.Core.Ast;

namespace Pzks.Core.Parallelization;

/// <summary>
/// ЛР2: побудова дерева паралельної форми мінімальної висоти та максимальної ширини.
/// 1. Дерево з ЛР1 переводиться в ланцюжки сум і добутків; дужки з + та - розкриваються.
/// 2. Спрощення: обчислення констант, прибирання 0 і 1, множення на 0, подвійний мінус,
///    взаємне знищення однакових доданків; ділення на 0 зупиняє побудову.
/// 3. Кожен ланцюжок будується як дерево мінімальної висоти без зміни порядку операндів;
///    віднімання і ділення групуються через додавання і множення:
///    a-b-c-d = (a-b)-(c+d), a/b/c/d = (a/b)/(c*d).
/// </summary>
public static class ParallelTreeBuilder
{
    public static ParallelResult Build(string expression)
    {
        var analysis = ExpressionAnalyzer.Analyze(expression);
        if (analysis.Tree is null)
            return new ParallelResult(analysis, [], [], null, null);

        var session = new Session();
        var form = session.Simplify(session.Lower(analysis.Tree));
        if (session.Errors.Count > 0)
            return new ParallelResult(analysis, session.Steps, session.Errors, null, null);

        var simplified = Sequential(form);
        var tree = session.Balance(form);
        return new ParallelResult(analysis, session.Steps, session.Errors, simplified, tree);
    }

    private sealed class Session
    {
        public List<Transformation> Steps { get; } = [];
        public List<ParallelError> Errors { get; } = [];

        private void Log(StepKind kind, string description) => Steps.Add(new Transformation(kind, description));

        // ---------- 1. Дерево ЛР1 → ланцюжки ----------

        public Form Lower(ExprNode node) => node switch
        {
            NumberNode n => LowerNumber(n),
            VariableNode v => new LeafForm(v, v.Name),
            FunctionNode f => new CallForm(f.Name, f.Arguments.Select(Lower).ToList(), Src(f)),
            UnaryNode { Operator: '+' } u => LowerUnaryPlus(u),
            UnaryNode u => Negate(Lower(u.Operand), u),
            BinaryNode b => LowerChain(b),
            _ => throw new ArgumentOutOfRangeException(nameof(node)),
        };

        private static Form LowerNumber(NumberNode number)
        {
            try
            {
                return new NumberForm((decimal)number.Value, number.Text, number.Text);
            }
            catch (OverflowException)
            {
                return new LeafForm(number, number.Text);
            }
        }

        private Form LowerUnaryPlus(UnaryNode node)
        {
            Log(StepKind.Simplification, $"прибрано унарний плюс: {Quote(Src(node))}");
            return Lower(node.Operand);
        }

        private Form Negate(Form operand, UnaryNode node)
        {
            if (operand is not ChainForm { Kind: ChainKind.Sum } sum)
                return new ChainForm(ChainKind.Sum, [new Part(true, operand)], Src(node));

            var flipped = sum.Parts.Select(p => p with { Inverse = !p.Inverse }).ToList();
            if (sum.Parts.All(p => p.Inverse))
                Log(StepKind.Simplification, $"подвійний мінус: {Src(node)} = {Join(ChainKind.Sum, flipped)}");
            return new ChainForm(ChainKind.Sum, flipped, Src(node));
        }

        private Form LowerChain(BinaryNode node)
        {
            var kind = node.Operator is '+' or '-' ? ChainKind.Sum : ChainKind.Product;
            var parts = new List<Part>();
            Append(node.Left, inverse: false, isRight: false);
            Append(node.Right, inverse: node.Operator is '-' or '/', isRight: true);
            return new ChainForm(kind, parts, Src(node));

            void Append(ExprNode child, bool inverse, bool isRight)
            {
                var form = Lower(child);
                if (form is not ChainForm chain || chain.Kind != kind)
                {
                    parts.Add(new Part(inverse, form));
                    return;
                }

                var moved = chain.Parts.Select(p => p with { Inverse = p.Inverse ^ inverse }).ToList();
                // Правий операнд-сума в дереві ЛР1 буває лише тоді, коли у виразі були дужки;
                // унарний мінус перед сумою — це завжди -( ... ).
                bool hadBrackets = (isRight && child is BinaryNode) || (child is UnaryNode && chain.Parts.Count > 1);
                if (kind == ChainKind.Sum && hadBrackets)
                {
                    string written = isRight ? $"{node.Operator}({Src(child)})" : Src(child);
                    Log(StepKind.Brackets, $"розкрито дужки: {written} = {Join(kind, moved, leadingSign: isRight)}");
                }
                parts.AddRange(moved);
            }
        }

        // ---------- 2. Спрощення ----------

        public Form Simplify(Form form) => form switch
        {
            ChainForm { Kind: ChainKind.Sum } sum => SimplifySum(sum),
            ChainForm product => SimplifyProduct(product),
            CallForm call => call with { Arguments = call.Arguments.Select(Simplify).ToList() },
            _ => form,
        };

        private Form SimplifySum(ChainForm sum)
        {
            var parts = new List<Part>();
            foreach (var part in sum.Parts)
            {
                var item = Simplify(part.Item);
                if (item is ChainForm { Kind: ChainKind.Sum } inner)
                    parts.AddRange(inner.Parts.Select(p => p with { Inverse = p.Inverse ^ part.Inverse }));
                else
                    parts.Add(part with { Item = item });
            }

            var zeros = parts.Where(p => p.Item.IsNumber(0)).ToList();
            if (zeros.Count > 0)
            {
                Log(StepKind.Simplification,
                    $"прибрано нульові доданки: {string.Join(", ", zeros.Select(p => Quote(p.Item.Source)))}");
                parts.RemoveAll(p => p.Item.IsNumber(0));
            }

            for (int i = 0; i < parts.Count; i++)
            {
                var current = parts[i];
                if (current.Item is NumberForm)
                    continue;
                int j = parts.FindIndex(i + 1, p => p.Inverse != current.Inverse && p.Item.Key() == current.Item.Key());
                if (j < 0)
                    continue;

                Log(StepKind.Simplification,
                    $"доданки {Quote(Signed(current))} і {Quote(Signed(parts[j]))} взаємно знищуються");
                parts.RemoveAt(j);
                parts.RemoveAt(i);
                i--;
            }

            bool foldedToZero = false;
            var constants = parts.Where(p => p.Item is NumberForm).ToList();
            if (constants.Count >= 2 && TrySum(constants, out decimal total))
            {
                string text = Join(ChainKind.Sum, constants);
                Log(StepKind.Constants, $"обчислено: {text} = {NumberForm.Format(total)}");
                int at = parts.IndexOf(constants[0]);
                parts.RemoveAll(p => p.Item is NumberForm);
                if (total != 0)
                    parts.Insert(at, new Part(total < 0, NumberForm.Of(Math.Abs(total), text)));
                foldedToZero = total == 0;
            }

            if (parts.Count == 0)
            {
                if (!foldedToZero)
                    Log(StepKind.Simplification, $"{Quote(sum.Source)} = 0");
                return NumberForm.Of(0, sum.Source);
            }
            if (parts is [{ Inverse: false } single])
                return single.Item;
            if (parts is [{ Item: NumberForm negative }])
                return NumberForm.Of(-negative.Value, sum.Source);
            return new ChainForm(ChainKind.Sum, parts, sum.Source);
        }

        private Form SimplifyProduct(ChainForm product)
        {
            int extractedSigns = 0;
            var parts = new List<Part>();
            foreach (var part in product.Parts)
                Add(part.Inverse, Simplify(part.Item));

            void Add(bool inverse, Form item)
            {
                switch (item)
                {
                    case ChainForm { Kind: ChainKind.Product } inner:
                        foreach (var p in inner.Parts)
                            Add(p.Inverse ^ inverse, p.Item);
                        break;
                    case ChainForm { Kind: ChainKind.Sum, Parts: [{ Inverse: true } negated] }:
                        extractedSigns++;
                        Add(inverse, negated.Item);
                        break;
                    case NumberForm { Value: < 0 } number:
                        extractedSigns++;
                        parts.Add(new Part(inverse, NumberForm.Of(-number.Value, number.Source)));
                        break;
                    default:
                        parts.Add(new Part(inverse, item));
                        break;
                }
            }

            bool negative = extractedSigns % 2 == 1;
            if (extractedSigns > 0)
                Log(StepKind.Simplification, negative
                    ? $"знак мінус винесено за добуток {Quote(product.Source)}"
                    : $"знаки мінус у добутку {Quote(product.Source)} взаємно знищуються");

            var zeroDivisors = parts.Where(p => p.Inverse && p.Item.IsNumber(0)).ToList();
            if (zeroDivisors.Count > 0)
            {
                foreach (var divisor in zeroDivisors)
                {
                    bool literal = divisor.Item is NumberForm n && n.Source == n.Text;
                    Errors.Add(new ParallelError(product.Source, literal
                        ? $"ділення на нуль: дільник {Quote(divisor.Item.Source)}"
                        : $"ділення на нуль: дільник {Quote(divisor.Item.Source)} після спрощення дорівнює 0"));
                }
                return product;
            }

            if (parts.Any(p => !p.Inverse && p.Item.IsNumber(0)))
            {
                Log(StepKind.Simplification, parts[0].Item.IsNumber(0) && parts.Skip(1).All(p => p.Inverse)
                    ? $"{Quote(product.Source)} = 0, бо ділене дорівнює 0"
                    : $"{Quote(product.Source)} = 0, бо є множник 0");
                return NumberForm.Of(0, product.Source);
            }

            if (parts.All(p => p.Item is NumberForm) && TryDivide(parts, out decimal quotient))
            {
                decimal value = negative ? -quotient : quotient;
                Log(StepKind.Constants, $"обчислено: {Join(ChainKind.Product, parts)} = {NumberForm.Format(value)}");
                return NumberForm.Of(value, product.Source);
            }

            RemoveOnes(parts, product.Source);
            FoldConstants(parts, inverse: false);
            FoldConstants(parts, inverse: true);

            Form result = parts is [{ Inverse: false } single]
                ? single.Item
                : new ChainForm(ChainKind.Product, parts, product.Source);
            if (!negative)
                return result;
            return result is NumberForm number
                ? NumberForm.Of(-number.Value, product.Source)
                : new ChainForm(ChainKind.Sum, [new Part(true, result)], product.Source);
        }

        private void RemoveOnes(List<Part> parts, string source)
        {
            // Якщо інших множників у чисельнику немає, одна одиниця лишається: 1/a.
            bool hasNumerator = parts.Any(p => !p.Inverse && !p.Item.IsNumber(1));
            var keep = hasNumerator ? null : parts.FirstOrDefault(p => !p.Inverse);
            var ones = parts.Where(p => p.Item.IsNumber(1) && !ReferenceEquals(p, keep)).ToList();
            if (ones.Count == 0)
                return;

            string removed = string.Join(", ", ones.Select(p => $"{(p.Inverse ? "ділення на" : "множення на")} {p.Item.Source}"));
            Log(StepKind.Simplification, $"прибрано {removed} у {Quote(source)}");
            parts.RemoveAll(p => ones.Any(o => ReferenceEquals(o, p)));
        }

        private void FoldConstants(List<Part> parts, bool inverse)
        {
            var constants = parts.Where(p => p.Inverse == inverse && p.Item is NumberForm).ToList();
            if (constants.Count < 2)
                return;

            decimal product = 1;
            try
            {
                foreach (var part in constants)
                    product *= ((NumberForm)part.Item).Value;
            }
            catch (OverflowException)
            {
                return;
            }

            string text = string.Join("*", constants.Select(p => Operand(ChainKind.Product, p.Item.Source, first: false)));
            Log(StepKind.Constants, $"обчислено: {text} = {NumberForm.Format(product)}");
            int at = parts.FindIndex(p => ReferenceEquals(p, constants[0]));
            parts.RemoveAll(p => constants.Any(c => ReferenceEquals(c, p)));
            parts.Insert(at, new Part(inverse, NumberForm.Of(product, text)));
        }

        private static bool TrySum(IEnumerable<Part> constants, out decimal total)
        {
            total = 0;
            try
            {
                foreach (var part in constants)
                {
                    decimal value = ((NumberForm)part.Item).Value;
                    total += part.Inverse ? -value : value;
                }
                return true;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        /// <summary>Добуток констант; false, якщо частка не ділиться точно (1/3) або виходить за межі.</summary>
        private static bool TryDivide(IEnumerable<Part> constants, out decimal quotient)
        {
            quotient = 0;
            try
            {
                decimal numerator = 1, denominator = 1;
                foreach (var part in constants)
                {
                    decimal value = ((NumberForm)part.Item).Value;
                    if (part.Inverse)
                        denominator *= value;
                    else
                        numerator *= value;
                }
                quotient = numerator / denominator;
                return quotient * denominator == numerator;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        // ---------- 3. Дерево мінімальної висоти ----------

        public ExprNode Balance(Form form) => form switch
        {
            NumberForm n => ToNumber(n),
            LeafForm l => l.Node,
            CallForm c => new FunctionNode(c.Name, c.Arguments.Select(Balance).ToList()),
            ChainForm chain => BalanceChain(chain),
            _ => throw new ArgumentOutOfRangeException(nameof(form)),
        };

        /// <summary>
        /// Ланцюжок операндів o1 ∘ o2 ∘ … ∘ on розбивається на дві частини там, де висота
        /// max(висота лівої, висота правої) + 1 мінімальна (динамічне програмування за відрізками);
        /// з однакових за висотою розбиттів обирається найрівніше — так дерево виходить ширшим.
        /// Група пам'ятає, чи вона входить у результат з мінусом (діленням):
        /// -x-y = -(x+y), -x+y = -(x-y), тому всередині групи віднімання стає додаванням.
        /// </summary>
        private ExprNode BalanceChain(ChainForm chain)
        {
            var parts = chain.Parts.ToList();
            int firstPositive = parts.FindIndex(p => !p.Inverse);
            if (chain.Kind == ChainKind.Sum && firstPositive > 0)
            {
                var positive = parts[firstPositive];
                parts.RemoveAt(firstPositive);
                parts.Insert(0, positive);
                Log(StepKind.Regrouping,
                    $"перший доданок від'ємний: {Quote(Signed(positive))} переставлено на початок, щоб не потрібен був унарний мінус");
            }

            var items = parts.Select(p => new Group(Balance(p.Item), p.Inverse)).ToList();
            int n = items.Count;
            var height = new int[n, n];
            var split = new int[n, n];
            for (int i = 0; i < n; i++)
                height[i, i] = TreeLevels.Tiers(items[i].Node);

            for (int length = 2; length <= n; length++)
            {
                for (int i = 0; i + length <= n; i++)
                {
                    int j = i + length - 1;
                    height[i, j] = int.MaxValue;
                    for (int k = i; k < j; k++)
                    {
                        int h = 1 + Math.Max(height[i, k], height[k + 1, j]);
                        bool better = h < height[i, j]
                            || (h == height[i, j] && Imbalance(i, k, j) <= Imbalance(i, split[i, j], j));
                        if (better)
                        {
                            height[i, j] = h;
                            split[i, j] = k;
                        }
                    }
                }
            }

            var root = Make(0, n - 1);
            var tree = Chain(chain.Kind, [(root.Node, root.Inverse)]);

            if (n >= 3)
            {
                var sequential = Sequential(chain);
                string after = ExprPrinter.ToGroupedInfix(tree);
                if (ExprPrinter.ToGroupedInfix(sequential) != after)
                    Log(StepKind.Regrouping,
                        $"{ExprPrinter.ToInfix(sequential)}  →  {after}   (ярусів: {TreeLevels.Tiers(sequential)} → {TreeLevels.Tiers(tree)})");
            }
            return tree;

            Group Make(int i, int j)
            {
                if (i == j)
                    return items[i];
                var left = Make(i, split[i, j]);
                var right = Make(split[i, j] + 1, j);
                char op = chain.Kind.Operator(left.Inverse != right.Inverse);
                return new Group(new BinaryNode(op, left.Node, right.Node), left.Inverse);
            }
        }

        private static int Imbalance(int i, int k, int j) => Math.Abs((k - i + 1) - (j - k));

        private sealed record Group(ExprNode Node, bool Inverse);
    }

    // ---------- Допоміжне ----------

    /// <summary>Ланцюжок у звичайному лівоасоціативному вигляді (так, як його будує ЛР1).</summary>
    private static ExprNode Sequential(Form form) => form switch
    {
        NumberForm n => ToNumber(n),
        LeafForm l => l.Node,
        CallForm c => new FunctionNode(c.Name, c.Arguments.Select(Sequential).ToList()),
        ChainForm chain => Chain(chain.Kind, chain.Parts.Select(p => (Sequential(p.Item), p.Inverse))),
        _ => throw new ArgumentOutOfRangeException(nameof(form)),
    };

    private static ExprNode Chain(ChainKind kind, IEnumerable<(ExprNode Node, bool Inverse)> parts)
    {
        ExprNode? result = null;
        foreach (var (node, inverse) in parts)
        {
            if (result is not null)
                result = new BinaryNode(kind.Operator(inverse), result, node);
            else if (!inverse)
                result = node;
            else
                result = kind == ChainKind.Sum
                    ? new UnaryNode('-', node)
                    : new BinaryNode('/', new NumberNode("1", 1), node);
        }
        return result ?? throw new InvalidOperationException("Порожній ланцюжок");
    }

    private static NumberNode ToNumber(NumberForm number) => new(number.Text, (double)number.Value);

    private static string Src(ExprNode node) => ExprPrinter.ToInfix(node);

    private static string Quote(string text) => $"«{text}»";

    private static string Signed(Part part) => (part.Inverse ? "-" : "+") + part.Item.Source;

    /// <summary>Запис частин ланцюжка за їхнім вихідним текстом: «2-4.8/2», «-b+c+d».</summary>
    private static string Join(ChainKind kind, IReadOnlyList<Part> parts, bool leadingSign = false) =>
        string.Concat(parts.Select((p, i) =>
        {
            bool first = i == 0 && !leadingSign;
            string sign = first && !p.Inverse ? "" : kind.Operator(p.Inverse).ToString();
            return sign + Operand(kind, p.Item.Source, first && !p.Inverse);
        }));

    private static string Operand(ChainKind kind, string text, bool first)
    {
        bool needsParens = (kind == ChainKind.Product && HasTopLevelSum(text)) || (!first && text.StartsWith('-'));
        return needsParens ? $"({text})" : text;
    }

    private static bool HasTopLevelSum(string text)
    {
        int depth = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '(')
                depth++;
            else if (c == ')')
                depth--;
            else if (depth == 0 && i > 0 && c is '+' or '-')
                return true;
        }
        return false;
    }
}
