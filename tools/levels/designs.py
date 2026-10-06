"""
Исходники уровней 16–50 из рисунков (pictures.py) и таблицы механик.

Запуск из корня репозитория:
    python3 tools/levels/designs.py            # перезаписать levels-src/level_016..050.json
    python3 tools/levels/designs.py 23 24      # только выбранные уровни

Потом собрать готовые уровни:
    dotnet run --project dotnet/KnittedPicture.LevelTool -c Release -- --only 16-50

Внимание: скрипт перезаписывает исходники целиком. Если исходник правили руками,
перенесите правку сюда (в P или M), иначе она пропадёт при следующем запуске.
"""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pictures import P  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))

# Механики по уровням: только то, что отличает уровень. Остальное — из тира.
M = {
16: dict(links=1, tip="Новая коллекция «Вкусняшки»: картинки крупнее, чужой пряжи больше"),
17: dict(rainbow=1),
18: dict(mystery=True, knots=2),
19: dict(ice=1, locks=2),
20: dict(hard=True, thick=1, hidden=3),
21: dict(hidden=3, knots=2),
22: dict(thick=2, links=1),
23: dict(ice=2),
24: dict(locks=2, gold=4),
25: dict(hard=True, moth=True, hidden=2, knots=2, links=1),
26: dict(rainbow=2, links=1),
27: dict(mystery=True, knots=3),
28: dict(thick=2, ice=1),
29: dict(locks=3, hidden=3),
30: dict(hard=True, moth=True, ice=2, knots=2),
31: dict(knots=3, ice=2),
32: dict(rainbow=1, links=2),
33: dict(mystery=True, hidden=3),
34: dict(thick=1, locks=2),
35: dict(hard=True, hidden=2, knots=2),
36: dict(links=2, ice=2),
37: dict(thick=2, rainbow=2, junk=0, swaps=2),
38: dict(knots=3, hidden=3),
39: dict(mystery=True, locks=2),
40: dict(hard=True, moth=True, ice=2, thick=1, knots=2),
41: dict(ice=3, links=1),
42: dict(thick=2, knots=2),
43: dict(mystery=True, rainbow=2),
44: dict(locks=3, hidden=3),
45: dict(hard=True, moth=True, ice=1, knots=2),
46: dict(knots=3, ice=2, links=1),
47: dict(thick=2, locks=2, hidden=2),
48: dict(rainbow=2, ice=2),
49: dict(mystery=True, knots=2, links=1, hidden=2),
50: dict(hard=True, moth=True, ice=3, thick=1, rainbow=1, knots=2, links=1, hidden=2,
         tip="Финал: всё, чему ты научилась. Удачи!"),
}

def slots_for(colors):
    """Слотов на станке по числу цветов рисунка: больше цветов — больше места, иначе тупик почти неизбежен."""
    if colors <= 3:
        return 3
    if colors <= 5:
        return 4
    if colors <= 7:
        return 5
    return 6


def tier(i):
    # Требования к уровню растут с коллекцией: меньше шансов у тапа наугад.
    c = (i - 16) // 5  # 0..6
    # Чужая пряжа — по одной: каждая взятая навсегда отнимает слот, и уже две-три превращают уровень
    # в лотерею (проверено ботами: доля побед падала до 0%). Сложность дают слои цветов и натяжение.
    return dict(maxRandom=round(0.2 - 0.02 * c, 2), junk=1, extra=2 if c < 4 else 3,
                gold=3 if c < 3 else 4, swaps=4 + c // 2)

order = ["id","name","tip","hard","timeLimit","palette","picture","fabric","band","mystery","gold","slots","columns","spools","swaps",
         "hidden","knots","links","locks","rainbow","thick","ice","extra","junk","parSlack","minWin","maxWin","maxRandom",
         "maxForced","minDanger","maxDanger","seed"]

only = {int(a) for a in sys.argv[1:]}
for i, (name, pal, rows) in sorted(P.items()):
    if only and i not in only:
        continue
    m = dict(M[i])
    t = tier(i)
    motif = sum(ch != "." for r in rows for ch in r)
    colors = len(set("".join(rows)) - {"."})
    hard = m.pop("hard", False)
    moth = m.pop("moth", False)
    d = dict(id=i, name=name, tip=m.pop("tip", ""), hard=hard,
             timeLimit=int(round((motif * 0.8 + 45) / 5) * 5) if moth else 0,
             palette=[{"key": k, "hex": v} for k, v in pal.items()],
             picture=rows, fabric=".", band=3 if colors >= 6 else 2, mystery=m.pop("mystery", False),
             gold=m.pop("gold", t["gold"]), slots=m.pop("slots", slots_for(colors)), columns=5,
             spools=max(10, min(24, round(motif / 6))), swaps=m.pop("swaps", t["swaps"]),
             hidden=0, knots=0, links=0, locks=0, rainbow=0, thick=0, ice=0,
             extra=t["extra"], junk=m.pop("junk", t["junk"]), parSlack=1,
             minWin=0.15 if hard else 0.4, maxWin=0.45 if hard else 0.78,
             maxRandom=0.05 if hard else t["maxRandom"],
             maxForced=0.55, minDanger=0.05, maxDanger=0.35, seed=i * 10 + 1)
    d.update(m)
    d = {k: d[k] for k in order}
    with open(os.path.join(ROOT, "levels-src", f"level_{i:03d}.json"), "w", encoding="utf-8") as f:
        f.write(json.dumps(d, ensure_ascii=False, indent=2) + "\n")
print("записано:", len(only) if only else len(P))
