namespace Pzks.Core.Simulation;

/// <summary>
/// Параметри VLIW-системи (система з наддовгим командним словом). Тривалості задаються в тактах.
/// За замовчуванням стоїть варіант 66 (2 процесори, 1 банк пам'яті) і тривалості операцій
/// симулятора Eucalyptus: + 2, - 3, * 4, / 8; одне звернення до пам'яті (читання або запис) займає 1 такт.
/// </summary>
public sealed record VliwParameters(
    int Processors = 2,
    int Banks = 1,
    int Add = 2,
    int Subtract = 3,
    int Multiply = 4,
    int Divide = 8,
    int Memory = 1)
{
    public const int MaxProcessors = 6;
    public const int MaxBanks = 6;
    public const int MaxDuration = 20;
    public const int MaxMemory = 10;

    public int Duration(char operation) => operation switch
    {
        '+' => Add,
        '-' => Subtract,
        '*' => Multiply,
        '/' => Divide,
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "VLIW виконує лише + - * /"),
    };

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        Check(Processors, 1, MaxProcessors, "кількість процесорів");
        Check(Banks, 1, MaxBanks, "кількість банків пам'яті");
        Check(Add, 1, MaxDuration, "тривалість +");
        Check(Subtract, 1, MaxDuration, "тривалість -");
        Check(Multiply, 1, MaxDuration, "тривалість *");
        Check(Divide, 1, MaxDuration, "тривалість /");
        Check(Memory, 1, MaxMemory, "тривалість звернення до пам'яті");
        return errors;

        void Check(int value, int min, int max, string name)
        {
            if (value < min || value > max)
                errors.Add($"{name}: {value}, допустимо від {min} до {max}");
        }
    }

    public override string ToString() =>
        $"{Processors} {Plural(Processors, "процесор", "процесори", "процесорів")}, "
        + $"{Banks} {Plural(Banks, "банк", "банки", "банків")} пам'яті; "
        + $"+ {Add}, - {Subtract}, * {Multiply}, / {Divide}, пам'ять {Memory}";

    private static string Plural(int n, string one, string few, string many) =>
        (n % 10, n % 100) switch
        {
            (1, not 11) => one,
            ( >= 2 and <= 4, < 12 or > 14) => few,
            _ => many,
        };
}
