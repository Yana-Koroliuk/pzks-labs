namespace Pzks.Core.Simulation;

public enum CellKind
{
    /// <summary>Процесор не зайнятий: команди в поточному слові для нього немає або вона вже завершилась.</summary>
    Idle,

    /// <summary>Процесор чекає, поки звільниться банк пам'яті (конфлікт звернень до пам'яті).</summary>
    Wait,

    Read,
    Compute,
    Write,
}

/// <summary>
/// Такт процесора. Task: задача, яку виконує процесор; Source: чий результат читається
/// (0 означає вхідні дані задачі, «*» в Eucalyptus); Bank: номер банку для читання і запису.
/// </summary>
public readonly record struct ProcessorCell(CellKind Kind, int Task = 0, int Source = 0, int Bank = 0);

/// <summary>Такт банку пам'яті: читання або запис результату задачі Data процесором Processor.</summary>
public readonly record struct BankCell(CellKind Kind, int Data = 0, bool Input = false, int Processor = 0);

/// <summary>Команда довгого слова: задача на процесорі; Order: порядок розміщення (впливає на конфлікти пам'яті).</summary>
public sealed record Command(int Task, int Processor, int Order, int Start, int End, int Reads, int Waits)
{
    public int Length => End - Start + 1;
}

/// <summary>Наддовге командне слово: команди всіх процесорів, що стартують одночасно. Слово триває до завершення найдовшої з них.</summary>
public sealed record InstructionWord(int Index, int Start, int End, IReadOnlyList<Command> Commands)
{
    public int Length => End - Start + 1;
}

public sealed record VliwSchedule(
    VliwParameters Parameters,
    TaskGraph Graph,
    IReadOnlyList<InstructionWord> Words,
    IReadOnlyList<IReadOnlyList<ProcessorCell>> Processors,
    IReadOnlyList<IReadOnlyList<BankCell>> Banks)
{
    /// <summary>Час паралельного виконання Tp: такт завершення останнього слова.</summary>
    public int ParallelTime => Words.Count == 0 ? 0 : Words[^1].End;

    /// <summary>
    /// Час послідовного виконання T1 (так само рахує Eucalyptus): на одному процесорі всі проміжні результати
    /// вже в нього, тому для кожної задачі рахуються читання вхідних даних (лише початкові задачі), обчислення і запис результату.
    /// </summary>
    public int SequentialTime => Graph.Tasks.Sum(t => (t.IsInitial ? Parameters.Memory : 0) + t.Duration + Parameters.Memory);

    public double Speedup => ParallelTime == 0 ? 0 : (double)SequentialTime / ParallelTime;

    public double Efficiency => Speedup / Parameters.Processors;

    /// <summary>Ефективність так, як її показує Eucalyptus: округлений до сотих Ky ділиться на p і знову округлюється.</summary>
    public double EucalyptusEfficiency => (double)Math.Round(
        Math.Round((decimal)Speedup, 2, MidpointRounding.AwayFromZero) / Parameters.Processors, 2, MidpointRounding.AwayFromZero);

    /// <summary>Процесори, що виконали хоча б одну задачу.</summary>
    public int ActiveProcessors => Processors.Count(cells => cells.Any(c => c.Kind == CellKind.Compute));

    public double ActiveEfficiency => ActiveProcessors == 0 ? 0 : Speedup / ActiveProcessors;

    /// <summary>Такти очікування банку пам'яті на всіх процесорах.</summary>
    public int MemoryWaits => Processors.Sum(cells => cells.Count(c => c.Kind == CellKind.Wait));

    /// <summary>Корисні такти процесора: читання, обчислення і запис (як у Eucalyptus).</summary>
    public int UsefulCells(int processor) => Processors[processor - 1].Count(c => c.Kind is CellKind.Read or CellKind.Compute or CellKind.Write);

    public int Count(int processor, CellKind kind) => Processors[processor - 1].Count(c => c.Kind == kind);

    public double ProcessorEfficiency(int processor) => ParallelTime == 0 ? 0 : (double)UsefulCells(processor) / ParallelTime;

    public int BankBusy(int bank) => Banks[bank - 1].Count(c => c.Kind != CellKind.Idle);

    public Command CommandOf(int task) => Words.SelectMany(w => w.Commands).First(c => c.Task == task);

    /// <summary>Команди в порядку розміщення, для перевірки в Eucalyptus.</summary>
    public IEnumerable<(InstructionWord Word, Command Command)> Placement =>
        Words.SelectMany(w => w.Commands.OrderBy(c => c.Order).Select(c => (w, c)));
}
