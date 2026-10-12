using System.Collections.Generic;

namespace KnittedPicture.Playtest
{
    /// <summary>
    /// Что нашёл бот: нарушения правил (с примером и командой, чтобы повторить партию) и счётчики.
    /// Одинаковые нарушения сворачиваются в одну строку со счётом.
    /// </summary>
    public sealed class Findings
    {
        public readonly Dictionary<string, int> Problems = new Dictionary<string, int>();
        public readonly Dictionary<string, string> Examples = new Dictionary<string, string>();
        public readonly Dictionary<string, string> Repro = new Dictionary<string, string>();
        public readonly Dictionary<string, long> Counters = new Dictionary<string, long>();

        public int Total
        {
            get
            {
                var sum = 0;
                foreach (var count in Problems.Values)
                {
                    sum += count;
                }

                return sum;
            }
        }

        public void Fail(string kind, string detail, string repro = "")
        {
            Problems[kind] = Problems.TryGetValue(kind, out var n) ? n + 1 : 1;
            if (!Examples.ContainsKey(kind))
            {
                Examples[kind] = detail;
                Repro[kind] = repro;
            }
        }

        public void Count(string kind, long add = 1) => Counters[kind] = (Counters.TryGetValue(kind, out var n) ? n : 0) + add;
    }
}
