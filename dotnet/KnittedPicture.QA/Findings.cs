using System.Collections.Generic;
using System.Linq;
using KnittedPicture.Core;

namespace KnittedPicture.QA
{
    /// <summary>
    /// Находка: одна поломка (ключ оракула), сколько раз встретилась, где именно, и пример для повтора:
    /// уровень целиком (пограничные уровни существуют только в прогоне), личность, зерно, партия и её ужатая версия.
    /// </summary>
    public sealed class Finding
    {
        public Finding(Violation first, LevelData level, string persona, int seed, List<QaAction> actions, int index)
        {
            First = first;
            Level = level;
            Persona = persona;
            Seed = seed;
            Actions = actions.Take(index + 1).ToList();
            Minimal = Actions;
        }

        public Violation First { get; private set; }

        public string Kind => First.Kind;

        public Severity Severity => First.Severity;

        public LevelData Level { get; private set; }

        public string Persona { get; private set; }

        public int Seed { get; private set; }

        /// <summary>Партия до первого срабатывания включительно.</summary>
        public List<QaAction> Actions { get; private set; }

        /// <summary>Пример короче — берём его: ужимать быстрее, читать проще.</summary>
        public void Prefer(Violation v, LevelData level, string persona, int seed, List<QaAction> actions, int index)
        {
            if (index + 1 >= Actions.Count)
            {
                return;
            }

            First = v;
            Level = level;
            Persona = persona;
            Seed = seed;
            Actions = actions.Take(index + 1).ToList();
            Minimal = Actions;
        }

        /// <summary>Ужатая партия: каждое действие нужно, без него поломка пропадает.</summary>
        public List<QaAction> Minimal { get; set; }

        public int ShrinkTests { get; set; }

        public int Count { get; set; } = 1;

        public readonly SortedSet<string> Levels = new SortedSet<string>();
        public readonly SortedSet<string> Personas = new SortedSet<string>();

        /// <summary>Папка находки в отчёте (относительно корня отчёта).</summary>
        public string Folder { get; set; } = string.Empty;

        /// <summary>Описание на момент повтора ужатой партии (может уточнять первое).</summary>
        public string Detail { get; set; } = string.Empty;
    }

    /// <summary>Все находки прогона, свёрнутые по ключу.</summary>
    public sealed class Findings
    {
        private readonly Dictionary<string, Finding> _byKind = new Dictionary<string, Finding>();
        private readonly object _lock = new object();

        public IEnumerable<Finding> All => _byKind.Values.OrderBy(f => f.Severity).ThenByDescending(f => f.Count);

        public void Add(Violation v, LevelData level, string persona, int seed, List<QaAction> actions, int index)
        {
            lock (_lock)
            {
                if (!_byKind.TryGetValue(v.Kind, out var f))
                {
                    f = new Finding(v, level, persona, seed, actions, index);
                    _byKind[v.Kind] = f;
                }
                else
                {
                    f.Count++;
                    f.Prefer(v, level, persona, seed, actions, index);
                }

                f.Levels.Add(level.id >= 5000 ? $"{level.id} «{level.name}»" : level.id.ToString());
                f.Personas.Add(persona);
            }
        }

        public int Count(Severity s) => _byKind.Values.Count(f => f.Severity == s);
    }
}
