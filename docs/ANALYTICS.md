# Аналитика: какие события отправляет игра

## Где код и как проверить

- Точка входа: `Assets/Scripts/Game/Services/Analytics/Analytics.cs`, вызов `Analytics.Event("level_win", ("level", 12), …)`.
  Отправка в AppMetrica: `AppMetricaSink.cs` (сборка для магазина с плагином, символ `KNITTED_APPMETRICA`).
- Шаги воронки первых минут: `Assets/Scripts/Core/Meta/FunnelStep.cs`, учёт отправленных шагов в сохранении:
  `ProgressService.Funnel.cs`, отправка `App.Funnel(шаг)`.
- Проверка в редакторе и Development-сборке: каждое событие печатается в Console как `[Analytics] имя {параметры}`.
  Запустить игру с чистым сохранением (первый запуск сразу ведёт в уровень 1), пройти его и увидеть подряд `app_open`, `ftue_01_first_open`, `ftue_02_first_level_shown`, `level_start`, `ftue_03_first_move`,
  `level_win`, `ftue_04_level_1_win`.
- Последние 200 событий хранит `Analytics.Last` (для отладки и автоигрока).

## Без личных данных

В событиях и в профиле AppMetrica нет личных данных: никаких email, имени, телефона, рекламного идентификатора.
Профиль пользователя не заполняем, `SetUserProfileID` не вызываем, геолокацию не собираем. Параметры события только числа,
короткие строки-перечисления и флаги. Новое событие с чем-то похожим на личное не добавлять.

## Общие параметры событий уровня

`level` (номер; 0 — узор дня), `pattern` (узор дня?), `attempt` (какая по счёту попытка этого уровня),
`seconds` (сколько длится партия), `progress` (сколько процентов картинки связано), `taps` (тапов сделано).

## События

| Событие | Когда | Параметры |
|---|---|---|
| `app_open` | запуск игры | `level`, `wins`, `coins`, `store`, `ads_removed`, `first` (первый запуск), `launch` (номер запуска) |
| `app_background` | игру свернули | `screen` (тип экрана), `popup` (тип верхнего окна или пусто), `level`, `in_level`, `progress` (если на уровне), `sec`, `launch` |
| `ftue_NN_имя` | шаги воронки первых минут, по одному разу за установку | `sec` (секунд с запуска приложения), `launch` |
| `intro_step` | показана сцена вступления (пока оно не пройдено) | `step`, `first` |
| `intro_finish` | закончено вступление «Как играть» | `first`, `skipped`, `step`, `sec` |
| `level_start` | начат уровень | `level`, `attempt`, `hard`, `timed`, `completed_before`, `warm` (подарок тёплой серии) |
| `level_win` / `pattern_win` | картинка довязана | общие + `stars`, `boosters`, `rescues`, `best_combo`, `rushes`, `gold`, `auto_finish` (сработал ли «финиш») |
| `level_fail` | затор или моль | общие + `reason` (`jam`/`time`), `rescue_offered` (Slot/Scissors/Time/none), `jam_advice` (причина затора), `left` (сколько петель не хватило) |
| `rescue` | игрок выбрал спасение | общие + `type`, `via` (`stock`/`coins`/`video`/`free`) |
| `booster` | использован бустер | общие + `type`, `free` (бесплатный от серии) |
| `level_quit` | ушёл с уровня | общие + `via` (`home`/`restart`), `after_fail` (только при `home`: уход из окна проигрыша) |
| `stuck_help` | подарена помощь «застрявшему» | `level`, `attempts` |
| `pattern_start` | начат узор дня | `streak` |
| `offer_view` | игроку показано предложение | `placement` (`rescue_*`, `booster_*`, `double_reward`, `no_ads`, `frames`); у спасения ещё `price`, `stock`, `video`; у бустера `price` |
| `ad_rewarded` | видео за награду закончилось | `placement` (`rescue_*`, `booster_*`, `double_reward`), `watched`, `result` |
| `ad_interstitial` | показана межуровневая реклама | `placement` |
| `ads_consent` | ответ про персональную рекламу | `allowed` |
| `purchase` | попытка покупки | `product`, `ok`, `result` |
| `frame_buy` | куплена рамка | `frame`, `price` |
| `replay_view` | просмотр «как вязалось» | `from` (`win`/`album`), `level` |
| `daily_gift`, `chest_open`, `missions_claim` | забрана награда | `day` / `big`, `rarity` / `level` (и `level` у подарка и сундука) |
| `season_claim`, `comeback_claim`, `collection_frame`, `yarn_equip`, `exhibition_submit` | прочие награды и выставка | `step`, `rarity` / `days`, `rarity` / `collection` / `source`, `yarn`, `perk`, `level` / `score`, `medal` |

## Шаги воронки первых минут (ftue)

Имя события: `ftue_` + номер шага (две цифры, с единицы) + `_` + имя в snake_case. Каждый шаг отправляется один раз за установку:
маска отправленных хранится в сохранении (`funnel`) и остаётся после «Сбросить прогресс», поэтому «первые» не шлются повторно.
`launch` в сохранении (число запусков) тоже переживает сброс.

| Номер | Событие | Когда |
|---|---|---|
| 1 | `ftue_01_first_open` | первый запуск |
| 2 | `ftue_02_first_level_shown` | показан уровень 1 |
| 3 | `ftue_03_first_move` | первый ход |
| 4 | `ftue_04_level_1_win` | победа на уровне 1 |
| 5 | `ftue_05_level_2_start` | начат уровень 2 |
| 6 | `ftue_06_level_2_win` | победа на уровне 2 |
| 7 | `ftue_07_level_3_win` | победа на уровне 3 |
| 8 | `ftue_08_first_fail` | первый проигрыш |
| 9 | `ftue_09_first_home` | первый показ главной |
| 10 | `ftue_10_level_5_win` | победа на уровне 5 |
| 11 | `ftue_11_level_10_win` | победа на уровне 10 |

В параметре `sec` лежит время с запуска приложения: разница между шагами даёт время до первого хода и до победы.

## Какие отчёты строить в AppMetrica

1. **Воронка первой сессии:** `ftue_01` → `ftue_02` → `ftue_03` → `ftue_04` → `ftue_06` → `ftue_07`. Провал между соседними шагами показывает, где теряют новичков.
2. **Время до первого хода и первой победы:** `sec` у `ftue_03_first_move` и `ftue_04_level_1_win`. Цель: первый ход через 10–15 секунд после запуска.
3. **Воронка уровней:** `level_win` по `level`. Резкий провал между соседними уровнями — место, где бросают игру.
4. **Попытки до победы:** средний `attempt` в `level_win` по уровням. Больше 3–4 на обычном уровне — уровень стоит облегчить (сверь с отчётом бота: `dotnet run --project dotnet/KnittedPicture.Playtest -- --report r.md`).
5. **Где сдаются:** `level_quit` по `level`, `progress`, `via`, `after_fail`. Уход на 10–20% — уровень отталкивает с первых ходов.
6. **Где уходят из игры:** `app_background` по `screen`, `popup`, `level` для новых игроков (`launch` = 1 или 2).
7. **Причины проигрышей и «почти»:** `level_fail` по `reason` и `jam_advice`; доля проигрышей с `left` ≤ 5 — игроки были рядом с победой. Если на уровне 80% заторов «Tension», игроки не понимают натяжение.
8. **Бустеры и спасения:** `booster` по `type` и `free`; `rescue` по `type` и `via` (запас, монеты, видео, подарок).
9. **Монетизация:** `offer_view` по `placement` → `ad_rewarded` (`watched`) / `purchase` / `frame_buy`. Доля досмотров показывает, какие предложения работают.
10. **Удержание:** стандартные отчёты AppMetrica «Удержание» (день 1, 7, 30) по `app_open`.

## Правила

События не переименовывать и не перенумеровывать: по именам строятся отчёты. Новые события и шаги воронки добавлять в конец,
новые параметры — в конец списка параметров события.
