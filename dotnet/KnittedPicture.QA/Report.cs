using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using KnittedPicture.Core;

namespace KnittedPicture.QA
{
    /// <summary>Итог прогона для отчёта.</summary>
    public sealed class RunSummary
    {
        public int Seed;
        public int Levels;
        public int EdgeLevels;
        public int EdgeRejected;
        public long Episodes;
        public long Actions;
        public double Seconds;
        public string Mode = string.Empty;
        public readonly Dictionary<string, (long Episodes, long Wins)> Personas = new Dictionary<string, (long, long)>();
        public readonly List<(string Level, double StepMs, string What, double HintMs)> Slowest = new List<(string, double, string, double)>();
    }

    /// <summary>Отчёт: HTML со «скриншотами» для людей, JSON для CI и машин.</summary>
    public static class Report
    {
        public static void Write(string dir, RunSummary run, Findings findings, Coverage coverage)
        {
            File.WriteAllText(Path.Combine(dir, "report.html"), Html(run, findings, coverage), new UTF8Encoding(false));
            var json = new
            {
                run.Seed,
                run.Mode,
                run.Levels,
                run.EdgeLevels,
                run.Episodes,
                run.Actions,
                run.Seconds,
                findings = findings.All.Select(f => new
                {
                    f.Kind,
                    severity = f.Severity.ToString(),
                    title = f.First.Title,
                    detail = f.Detail,
                    f.Count,
                    levels = f.Levels,
                    personas = f.Personas,
                    persona = f.Persona,
                    f.Seed,
                    level = f.Level.id,
                    minimal = f.Minimal.Select(a => a.ToString()),
                    original = f.Actions.Count,
                    folder = f.Folder,
                }),
                coverage = coverage.Hits.OrderBy(k => k.Key).ToDictionary(k => k.Key, k => k.Value),
                missing = coverage.Missing,
            };
            File.WriteAllText(Path.Combine(dir, "report.json"), JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        }

        private static string E(string s) => WebUtility.HtmlEncode(s);

        private static string Badge(Severity s) => s switch
        {
            Severity.Crash => "<span class=b style='background:#d0021b'>ПАДЕНИЕ</span>",
            Severity.Bug => "<span class=b style='background:#ff2d55'>ОШИБКА</span>",
            Severity.Robustness => "<span class=b style='background:#f5a623'>ЗАЩИТА</span>",
            Severity.Warning => "<span class=b style='background:#8b5cf6'>ВНИМАНИЕ</span>",
            _ => "<span class=b style='background:#888'>ИНФО</span>",
        };

        private static string Html(RunSummary run, Findings findings, Coverage coverage)
        {
            var sb = new StringBuilder();
            sb.Append(@"<!doctype html><html lang=ru><head><meta charset=utf-8><meta name=viewport content='width=device-width,initial-scale=1'>
<title>QA-отчёт</title><style>
:root{--bg:#f7f3ff;--card:#fff;--ink:#2b2350;--ink2:#6b6390;--line:#e3dcf5}
@media (prefers-color-scheme:dark){:root{--bg:#17132a;--card:#211b3a;--ink:#eee9ff;--ink2:#a9a1cc;--line:#3a3160}}
body{margin:0;background:var(--bg);color:var(--ink);font:15px/1.45 system-ui,sans-serif}
main{max-width:1100px;margin:0 auto;padding:16px}
h1{font-size:24px}h2{font-size:19px;margin-top:28px}
.card{background:var(--card);border:1px solid var(--line);border-radius:14px;padding:14px 16px;margin:12px 0}
.b{color:#fff;border-radius:6px;padding:2px 7px;font-size:12px;font-weight:700;margin-right:8px}
.k{color:var(--ink2);font-size:13px}
pre{white-space:pre-wrap;background:var(--bg);border-radius:8px;padding:8px;font-size:12.5px;overflow-x:auto}
img{max-width:100%;border-radius:8px;border:1px solid var(--line);image-rendering:pixelated}
table{border-collapse:collapse;width:100%}td,th{border-bottom:1px solid var(--line);padding:5px 8px;text-align:left;font-size:14px}
.miss{color:#ff2d55;font-weight:600}.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(230px,1fr));gap:4px 16px}
ol{padding-left:22px}
</style></head><body><main>");
            sb.Append("<h1>QA-отчёт: глубокий плейтест</h1>");
            sb.Append($"<p class=k>Зерно {run.Seed} · режим {E(run.Mode)} · {run.Levels} настоящих уровней + {run.EdgeLevels} пограничных (валидатор отбросил {run.EdgeRejected}) · " +
                      $"{run.Episodes:N0} партий · {run.Actions:N0} действий · {run.Seconds:0} с</p>");

            var crash = findings.Count(Severity.Crash);
            var bug = findings.Count(Severity.Bug);
            sb.Append("<div class=card>");
            sb.Append(crash + bug == 0
                ? "<b>Ошибок в правилах не найдено.</b> "
                : $"<b>Найдено: падений {crash}, ошибок {bug}.</b> ");
            sb.Append($"Защита ядра: {findings.Count(Severity.Robustness)} · внимание: {findings.Count(Severity.Warning)} · инфо: {findings.Count(Severity.Info)}. ");
            var missing = coverage.Missing.ToList();
            sb.Append(missing.Count == 0 ? "Покрытие: все ожидаемые ситуации встретились." : $"Покрытие: не встретилось {missing.Count} ситуаций (ниже).");
            sb.Append("</div>");

            sb.Append("<h2>Находки</h2>");
            if (!findings.All.Any())
            {
                sb.Append("<p>Нет.</p>");
            }

            var n = 0;
            foreach (var f in findings.All)
            {
                n++;
                sb.Append("<div class=card>");
                sb.Append($"<div>{Badge(f.Severity)}<b>{E(f.First.Title)}</b> <span class=k>×{f.Count} · ключ <code>{E(f.Kind)}</code></span></div>");
                sb.Append($"<p class=k>Уровни: {E(string.Join(", ", f.Levels.Take(12)))}{(f.Levels.Count > 12 ? " …" : "")} · личности: {E(string.Join(", ", f.Personas))}</p>");
                sb.Append($"<p>Пример: уровень {f.Level.id} «{E(f.Level.name)}», личность «{E(f.Persona)}», зерно {f.Seed}. " +
                          $"Партия ужата с {f.Actions.Count} до {f.Minimal.Count} действий ({f.ShrinkTests} повторов).</p>");
                sb.Append("<b>Шаги воспроизведения</b><ol>");
                foreach (var a in f.Minimal)
                {
                    sb.Append($"<li><code>{E(a.ToString())}</code></li>");
                }

                sb.Append("</ol>");
                sb.Append($"<pre>{E(string.IsNullOrEmpty(f.Detail) ? f.First.Detail : f.Detail)}</pre>");
                if (!string.IsNullOrEmpty(f.Folder))
                {
                    sb.Append($"<p><b>Последние кадры</b> (красная рамка — где сработала проверка)</p><img src='{f.Folder}/film.png' alt='кадры'>");
                    sb.Append($"<p class=k>Повторить: <code>dotnet run -c Release --project dotnet/KnittedPicture.QA -- --replay {E(f.Folder)}/finding.json</code></p>");
                }

                sb.Append("</div>");
            }

            sb.Append("<h2>Покрытие ситуаций</h2><div class=card><div class=grid>");
            foreach (var e in Coverage.Expected)
            {
                var hits = coverage.Hits.TryGetValue(e, out var v) ? v : 0;
                sb.Append(hits == 0 ? $"<div class=miss>✗ {E(e)}</div>" : $"<div>✓ {E(e)} <span class=k>{hits:N0}</span></div>");
            }

            sb.Append("</div></div>");

            sb.Append("<h2>Личности</h2><div class=card><table><tr><th>Личность</th><th>Партий</th><th>Побед</th></tr>");
            foreach (var p in run.Personas.OrderBy(p => p.Key))
            {
                sb.Append($"<tr><td>{E(p.Key)}</td><td>{p.Value.Episodes:N0}</td><td>{(p.Value.Episodes == 0 ? 0 : 100.0 * p.Value.Wins / p.Value.Episodes):0}%</td></tr>");
            }

            sb.Append("</table></div>");

            sb.Append("<h2>Самые медленные действия</h2><div class=card><table><tr><th>Уровень</th><th>Шаг/ход, мс</th><th>Что</th><th>Подсказка, мс</th></tr>");
            foreach (var s in run.Slowest.OrderByDescending(s => Math.Max(s.StepMs, s.HintMs)).Take(12))
            {
                sb.Append($"<tr><td>{E(s.Level)}</td><td>{s.StepMs:0.0}</td><td><code>{E(s.What)}</code></td><td>{s.HintMs:0}</td></tr>");
            }

            sb.Append("</table><p class=k>Замер на машине прогона; слабый телефон медленнее примерно в 5–10 раз.</p></div>");
            sb.Append("</main></body></html>");
            return sb.ToString();
        }
    }
}
