using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KnittedPicture.Playtest
{
    /// <summary>Отчёт бота в Markdown: нарушения с командами повтора, счётчики и таблица сложности уровней.</summary>
    public static class Report
    {
        public static string Markdown(Findings f, List<LevelProfile> profiles, Options o)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Отчёт бота-тестировщика").AppendLine();
            sb.AppendLine($"Зерно {o.Seed}; хаос-партий на уровень {o.Runs}; сгенерированных уровней {o.Generated}; " +
                          $"мета-игра {o.Players} игроков × {o.Days} дней; партий на бота для сложности {o.DifficultyRuns}.").AppendLine();

            sb.AppendLine("## Нарушения правил").AppendLine();
            if (f.Problems.Count == 0)
            {
                sb.AppendLine("Нет.").AppendLine();
            }

            foreach (var kv in f.Problems.OrderByDescending(k => k.Value))
            {
                sb.AppendLine($"- **{kv.Key}** × {kv.Value}  ").AppendLine($"  пример: {f.Examples[kv.Key]}  ");
                if (f.Repro[kv.Key].Length > 0)
                {
                    sb.AppendLine($"  повторить: `dotnet run -c Release --project dotnet/KnittedPicture.Playtest -- {f.Repro[kv.Key]}`");
                }
            }

            sb.AppendLine().AppendLine("## Счётчики").AppendLine();
            foreach (var kv in f.Counters.OrderBy(k => k.Key))
            {
                sb.AppendLine($"- {kv.Key}: {kv.Value}");
            }

            if (profiles.Count > 0)
            {
                sb.AppendLine().AppendLine("## Сложность уровней").AppendLine();
                sb.AppendLine("Доля побед: наугад / «средний» / по подсветке / знающий; ранний затор — у «среднего».").AppendLine();
                sb.AppendLine("| № | Картинка | Наугад | Средний | По подсветке | Знающий | Ранний затор | Заметки |");
                sb.AppendLine("|---|---|---|---|---|---|---|---|");
                foreach (var p in profiles)
                {
                    var name = p.Name + (p.Hard ? " ★" : "") + (p.Timed ? " ⏱" : "");
                    sb.AppendLine($"| {p.Id} | {name} | {p.Random:P0} | {p.Average:P0} | {p.Follower:P0} | {p.Skilled:P0} | {p.EarlyJam:P0} | {string.Join("; ", p.Flags)} |");
                }

                sb.AppendLine().AppendLine("★ — сложный уровень, ⏱ — с молью (таймер).");
            }

            return sb.ToString();
        }
    }
}
