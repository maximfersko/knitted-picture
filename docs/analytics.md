# Аналитика: какие события отправляет игра

Код: `Assets/Scripts/Game/Services/Analytics/`. Вызов в игре: `Analytics.Event("level_win", ("level", 12), …)`.
В сборке для магазина с плагином AppMetrica события уходят в AppMetrica; в редакторе и Development-сборке
печатаются в Console как `[Analytics] имя {параметры}`. Личных данных в параметрах нет.

## Общие параметры событий уровня

`level` (номер; 0 — узор дня), `pattern` (узор дня?), `attempt` (какая по счёту попытка этого уровня),
`seconds` (сколько длится партия), `progress` (сколько процентов картинки связано), `taps` (тапов сделано).

## События

| Событие | Когда | Параметры |
|---|---|---|
| `app_open` | запуск игры | `level`, `wins`, `coins`, `store`, `ads_removed`, `first` (первый запуск) |
| `intro_finish` | закончено вступление «Как играть» | `first` |
| `level_start` | начат уровень | `level`, `attempt`, `hard`, `timed`, `completed_before`, `warm` (подарок тёплой серии) |
| `level_win` / `pattern_win` | картинка довязана | общие + `stars`, `boosters`, `rescues`, `best_combo`, `rushes`, `gold`, `auto_finish` (сработал ли «финиш»: победа решилась раньше конца вязки) |
| `level_fail` | затор или моль | общие + `reason` (`jam`/`time`), `rescue_offered` (что предложили: Slot/Scissors/Time/none), `jam_advice` (причина затора) |
| `rescue` | игрок выбрал спасение | общие + `type` |
| `booster` | использован бустер | общие + `type`, `free` (бесплатный от серии) |
| `level_quit` | ушёл домой посреди уровня | общие |
| `pattern_start` | начат узор дня | `streak` |
| `ad_rewarded` | видео за награду закончилось | `placement` (`rescue_*`, `booster_*`, `double_reward`), `watched` |
| `ad_interstitial` | показана межуровневая реклама | `placement` |
| `ads_consent` | ответ про персональную рекламу | `allowed` |
| `purchase` | попытка покупки | `product`, `ok` |
| `daily_gift`, `chest_open`, `missions_claim` | забрана награда | `day` / `big` / `level` |

## Какие отчёты строить в AppMetrica

1. **Воронка уровней:** `level_win` по `level` (сколько игроков дошли до каждого уровня). Резкий провал между
   соседними уровнями — место, где бросают игру.
2. **Попытки до победы:** средний `attempt` в `level_win` по уровням. Больше 3–4 на обычном уровне — уровень
   стоит облегчить (сверь с отчётом бота: `dotnet run --project dotnet/KnittedPicture.Playtest -- --report r.md`).
3. **Причины проигрышей:** `level_fail` по `reason` и `jam_advice`. Если на уровне 80% заторов «Tension» —
   игроки не понимают натяжение, нужна подсказка.
4. **Уход посреди уровня:** `level_quit` по `level` и `progress`. Уход на 10–20% — уровень отталкивает с первых ходов.
5. **Реклама:** доля `watched = true` в `ad_rewarded` по `placement` — какие предложения видео работают.
6. **Удержание:** стандартные отчёты AppMetrica «Удержание» (день 1, 7, 30) по `app_open`.
