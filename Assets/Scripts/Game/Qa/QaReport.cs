#if UNITY_EDITOR || DEVELOPMENT_BUILD || KNITTED_QA
#nullable enable

using System.Linq;
using System.Net;
using System.Text;
using UnityEngine;

namespace KnittedPicture.Game.Qa
{
    /// <summary>Отчёт внутриигрового бота: находки со скриншотами и действиями перед ними, путь по экранам, кадры, память.</summary>
    public static class QaReport
    {
        private static string E(string s) => WebUtility.HtmlEncode(s);

        public static string Html(QaBot bot)
        {
            var sb = new StringBuilder();
            sb.Append(@"<!doctype html><html lang=ru><head><meta charset=utf-8><meta name=viewport content='width=device-width,initial-scale=1'>
<title>QA в игре</title><style>
:root{--bg:#f7f3ff;--card:#fff;--ink:#2b2350;--ink2:#6b6390;--line:#e3dcf5}
@media (prefers-color-scheme:dark){:root{--bg:#17132a;--card:#211b3a;--ink:#eee9ff;--ink2:#a9a1cc;--line:#3a3160}}
body{margin:0;background:var(--bg);color:var(--ink);font:15px/1.45 system-ui,sans-serif}main{max-width:1100px;margin:0 auto;padding:16px}
.card{background:var(--card);border:1px solid var(--line);border-radius:14px;padding:14px 16px;margin:12px 0}
.b{color:#fff;border-radius:6px;padding:2px 7px;font-size:12px;font-weight:700;margin-right:8px;background:#8b5cf6}
.k{color:var(--ink2);font-size:13px}pre{white-space:pre-wrap;background:var(--bg);border-radius:8px;padding:8px;font-size:12.5px}
img{max-width:100%;max-height:560px;border-radius:8px;border:1px solid var(--line)}
.row{display:flex;gap:16px;flex-wrap:wrap}.row>div{flex:1 1 300px;min-width:0}
.journey{display:grid;grid-template-columns:repeat(auto-fill,minmax(150px,1fr));gap:8px}.journey img{max-height:300px}
table{border-collapse:collapse;width:100%}td,th{border-bottom:1px solid var(--line);padding:5px 8px;text-align:left;font-size:14px}
</style></head><body><main>");
            sb.Append("<h1>QA-бот в игре</h1>");
            sb.Append($"<p class=k>{E(SystemInfo.deviceModel)} · {Screen.width}×{Screen.height} · {bot.Duration / 60f:0.0} мин · действий {bot.Journal.Count} · находок {bot.Issues.Count}</p>");
            foreach (var issue in bot.Issues.OrderBy(i => i.Severity == "ПАДЕНИЕ" ? 0 : i.Severity == "ОШИБКА" ? 1 : 2).ThenByDescending(i => i.Count))
            {
                var color = issue.Severity == "ПАДЕНИЕ" ? "#d0021b" : issue.Severity == "ОШИБКА" ? "#ff2d55" : "#8b5cf6";
                sb.Append($"<div class=card><div><span class=b style='background:{color}'>{E(issue.Severity)}</span><b>{E(issue.Title)}</b> " +
                          $"<span class=k>×{issue.Count} · {E(issue.Screen)} · {issue.Time:0.0} с · <code>{E(issue.Kind)}</code></span></div><div class=row>");
                if (!string.IsNullOrEmpty(issue.Shot))
                {
                    sb.Append($"<div><img src='{issue.Shot}' alt='скриншот'></div>");
                }

                sb.Append($"<div><pre>{E(issue.Detail)}</pre><b>Перед этим</b><pre>{E(string.Join("\n", issue.Before))}</pre></div></div></div>");
            }

            sb.Append("<h2>Путь по экранам</h2><div class='card journey'>");
            foreach (var shot in bot.Journey)
            {
                sb.Append($"<div><img src='{shot}' alt=''><div class=k>{E(shot)}</div></div>");
            }

            sb.Append("</div><h2>Кадры по экранам</h2><div class=card><table><tr><th>Экран</th><th>Кадров</th><th>Средний, мс</th><th>Худший, мс</th></tr>");
            foreach (var kv in bot.Frames.OrderBy(k => k.Key))
            {
                sb.Append($"<tr><td>{E(kv.Key)}</td><td>{kv.Value.Frames}</td><td>{kv.Value.Sum / Mathf.Max(1, kv.Value.Frames) * 1000f:0.0}</td><td>{kv.Value.Max * 1000f:0}</td></tr>");
            }

            sb.Append("</table></div><h2>Память</h2><div class=card><table><tr><th>Время, с</th><th>Выделено, МБ</th><th>Текстур</th></tr>");
            foreach (var m in bot.Memory)
            {
                sb.Append($"<tr><td>{m.Time:0}</td><td>{m.Bytes / 1048576}</td><td>{m.Textures}</td></tr>");
            }

            sb.Append("</table></div><p class=k>Полный журнал действий — journal.txt рядом.</p></main></body></html>");
            return sb.ToString();
        }

        public static string Json(QaBot bot)
        {
            var issues = bot.Issues.Select(i => new Item
            {
                kind = i.Kind, severity = i.Severity, title = i.Title, detail = i.Detail, screen = i.Screen, time = i.Time, count = i.Count, shot = i.Shot,
                before = i.Before.ToArray(),
            }).ToArray();
            return JsonUtility.ToJson(new Root { device = SystemInfo.deviceModel, minutes = bot.Duration / 60f, issues = issues }, true);
        }

        [System.Serializable]
        private sealed class Root
        {
            public string device = string.Empty;
            public float minutes;
            public Item[] issues = new Item[0];
        }

        [System.Serializable]
        private sealed class Item
        {
            public string kind = string.Empty;
            public string severity = string.Empty;
            public string title = string.Empty;
            public string detail = string.Empty;
            public string screen = string.Empty;
            public float time;
            public int count;
            public string shot = string.Empty;
            public string[] before = new string[0];
        }
    }
}
#endif
