namespace Pzks.Core.Simulation;

/// <summary>
/// ЛР5: модель роботи VLIW-системи зі спільною пам'яттю з заданою кількістю банків.
/// Правила виконання задачі такі самі, як у класі VLIW симулятора Eucalyptus:
/// 1. Процесор читає з пам'яті вхідні дані (лише початкова задача) і результати задач-операндів,
///    яких у нього немає (свої попередні результати процесор не перечитує).
/// 2. Обчислює операцію (тривалість залежить від її типу).
/// 3. Записує результат у банк, який звільняється раніше (за рівності у менш заповнений).
/// Банк за один такт обслуговує лише одне звернення, тому інші процесори чекають (конфлікт пам'яті).
/// Вхідні дані до старту лежать у банках по черзі: дані початкової задачі 1 у банку 1, задачі 2 у банку 2 тощо.
///
/// Синхронізація VLIW: команди одного слова стартують в один такт, наступне слово починається
/// після завершення найдовшої команди (такт визначається найскладнішою командою).
/// Пакування слів (роль компілятора VLIW): готові задачі впорядковуються за пріоритетом, тобто довжиною
/// шляху до кореня (критичний шлях), у слово беруться перші p з них; перебираються порядки розміщення,
/// задача йде на процесор, у якого вже є її операнди, і обирається варіант з найранішим кінцем слова.
/// </summary>
public static class VliwSimulator
{
    public static VliwSchedule Run(TaskGraph graph, VliwParameters parameters)
    {
        var state = new State(graph, parameters);
        var done = new HashSet<int>();
        var words = new List<InstructionWord>();
        int start = 1;

        while (done.Count < graph.Tasks.Count)
        {
            var chosen = graph.Tasks
                .Where(t => !done.Contains(t.Id) && t.Inputs.All(done.Contains))
                .OrderByDescending(t => t.Priority)
                .ThenBy(t => t.Id)
                .Take(parameters.Processors)
                .ToList();

            (State State, List<Command> Commands, int End, int Waits)? best = null;
            foreach (var order in Permutations(chosen))
            {
                var trial = state.Clone();
                var used = new bool[parameters.Processors + 1];
                var commands = new List<Command>();
                foreach (var task in order)
                {
                    int processor = trial.ChooseProcessor(task, used);
                    used[processor] = true;
                    commands.Add(trial.Place(task, processor, start, commands.Count + 1));
                }

                int end = commands.Max(c => c.End);
                int waits = commands.Sum(c => c.Waits);
                if (best is null || end < best.Value.End || (end == best.Value.End && waits < best.Value.Waits))
                    best = (trial, commands, end, waits);
            }

            state = best!.Value.State;
            words.Add(new InstructionWord(words.Count + 1, start, best.Value.End,
                best.Value.Commands.OrderBy(c => c.Processor).ToList()));
            foreach (var task in chosen)
                done.Add(task.Id);
            start = best.Value.End + 1;
        }

        int total = words.Count == 0 ? 0 : words[^1].End;
        return new VliwSchedule(parameters, graph, words, state.ProcessorTimelines(total), state.BankTimelines(total));
    }

    private static IEnumerable<IReadOnlyList<VliwTask>> Permutations(IReadOnlyList<VliwTask> items)
    {
        if (items.Count <= 1)
        {
            yield return items;
            yield break;
        }
        for (int i = 0; i < items.Count; i++)
        {
            var rest = items.Where((_, j) => j != i).ToList();
            foreach (var tail in Permutations(rest))
                yield return [items[i], .. tail];
        }
    }

    private sealed class State
    {
        private readonly TaskGraph _graph;
        private readonly VliwParameters _parameters;
        private readonly List<ProcessorCell>[] _processors;
        private readonly HashSet<int>[] _local;
        private readonly List<BankCell>[] _banks;
        private readonly int[] _bankContents;
        private readonly Dictionary<int, int> _inputBank;
        private readonly Dictionary<int, int> _resultBank;

        public State(TaskGraph graph, VliwParameters parameters)
        {
            _graph = graph;
            _parameters = parameters;
            _processors = Enumerable.Range(0, parameters.Processors).Select(_ => new List<ProcessorCell>()).ToArray();
            _local = Enumerable.Range(0, parameters.Processors).Select(_ => new HashSet<int>()).ToArray();
            _banks = Enumerable.Range(0, parameters.Banks).Select(_ => new List<BankCell>()).ToArray();
            _bankContents = new int[parameters.Banks];
            _inputBank = [];
            _resultBank = [];

            int bank = 0;
            foreach (var task in graph.Tasks.Where(t => t.IsInitial))
            {
                _inputBank[task.Id] = bank + 1;
                _bankContents[bank]++;
                bank = (bank + 1) % parameters.Banks;
            }
        }

        private State(State other)
        {
            _graph = other._graph;
            _parameters = other._parameters;
            _processors = other._processors.Select(p => new List<ProcessorCell>(p)).ToArray();
            _local = other._local.Select(l => new HashSet<int>(l)).ToArray();
            _banks = other._banks.Select(b => new List<BankCell>(b)).ToArray();
            _bankContents = (int[])other._bankContents.Clone();
            _inputBank = other._inputBank;
            _resultBank = new Dictionary<int, int>(other._resultBank);
        }

        public State Clone() => new(this);

        /// <summary>Вільний процесор, у якого вже є найбільше операндів задачі; за рівності той, що має менший номер.</summary>
        public int ChooseProcessor(VliwTask task, bool[] used)
        {
            int best = 0, bestLocal = -1;
            for (int p = 1; p <= _parameters.Processors; p++)
            {
                if (used[p])
                    continue;
                int local = task.Inputs.Count(_local[p - 1].Contains);
                if (local > bestLocal)
                    (best, bestLocal) = (p, local);
            }
            return best;
        }

        public Command Place(VliwTask task, int processor, int start, int order)
        {
            var cells = _processors[processor - 1];
            int time = start, reads = 0, waits = 0;
            int memory = _parameters.Memory;

            if (task.IsInitial)
                Access(CellKind.Read, _inputBank[task.Id], 0);

            var toRead = task.Inputs.Where(i => !_local[processor - 1].Contains(i)).ToList();
            while (toRead.Count > 0)
            {
                int source = toRead.MinBy(i => WhenFree(_resultBank[i], time, memory));
                toRead.Remove(source);
                Access(CellKind.Read, _resultBank[source], source);
            }

            for (int i = 0; i < task.Duration; i++)
                Set(cells, time++, new ProcessorCell(CellKind.Compute, task.Id));

            int bank = BestBank(time, memory);
            Access(CellKind.Write, bank, task.Id);
            _resultBank[task.Id] = bank;
            _bankContents[bank - 1]++;
            _local[processor - 1].Add(task.Id);
            return new Command(task.Id, processor, order, start, time - 1, reads, waits);

            void Access(CellKind kind, int bankId, int source)
            {
                int at = WhenFree(bankId, time, memory);
                for (; time < at; time++, waits++)
                    Set(cells, time, new ProcessorCell(CellKind.Wait, task.Id));
                for (int i = 0; i < memory; i++, time++)
                {
                    int data = kind == CellKind.Read && source == 0 ? task.Id : source;
                    Set(cells, time, new ProcessorCell(kind, task.Id, source, bankId));
                    Set(_banks[bankId - 1], time, new BankCell(kind, data, kind == CellKind.Read && source == 0, processor));
                }
                if (kind == CellKind.Read)
                    reads++;
            }
        }

        /// <summary>Перший такт не раніше time, з якого банк вільний duration тактів поспіль.</summary>
        private int WhenFree(int bank, int time, int duration)
        {
            var cells = _banks[bank - 1];
            for (int t = time; ; t++)
            {
                bool free = true;
                for (int i = 0; i < duration && free; i++)
                    free = At(cells, t + i).Kind == CellKind.Idle;
                if (free)
                    return t;
            }
        }

        private int BestBank(int time, int duration)
        {
            int best = 1;
            for (int b = 2; b <= _parameters.Banks; b++)
            {
                int free = WhenFree(b, time, duration), bestFree = WhenFree(best, time, duration);
                if (free < bestFree || (free == bestFree && _bankContents[b - 1] < _bankContents[best - 1]))
                    best = b;
            }
            return best;
        }

        public IReadOnlyList<IReadOnlyList<ProcessorCell>> ProcessorTimelines(int length) =>
            _processors.Select(cells => (IReadOnlyList<ProcessorCell>)Pad(cells, length)).ToList();

        public IReadOnlyList<IReadOnlyList<BankCell>> BankTimelines(int length) =>
            _banks.Select(cells => (IReadOnlyList<BankCell>)Pad(cells, length)).ToList();

        private static List<T> Pad<T>(List<T> cells, int length) where T : struct
        {
            var result = cells.Take(length).ToList();
            while (result.Count < length)
                result.Add(default);
            return result;
        }

        private static T At<T>(List<T> cells, int time) where T : struct => time <= cells.Count ? cells[time - 1] : default;

        private static void Set<T>(List<T> cells, int time, T value) where T : struct
        {
            while (cells.Count < time)
                cells.Add(default);
            cells[time - 1] = value;
        }
    }
}
