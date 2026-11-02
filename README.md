# Вязаная картинка

Hybrid-casual головоломка: игрок отправляет клубки на станок, а станок вяжет пиксельную картинку столбиками — клубки работают одновременно и открывают друг другу новые клетки. Движок — Unity 6 LTS, 2D. 15 уровней, 3 коллекции альбома, 4 бустера.

Полный дизайн игры — [docs/GDD.md](docs/GDD.md), исследование механик — [docs/research.md](docs/research.md), план на 10 версий вперёд — [docs/ROADMAP.md](docs/ROADMAP.md).

## Структура

| Путь | Что там |
|---|---|
| `Assets/Scripts/Core/` | Правила без Unity: станок, особые клубки, моль, бустеры, валидатор, решатель, экономика, прогресс |
| `Assets/Scripts/Game/` | Игра в Unity: экраны, анимации, звук, сохранения, заглушка рекламы — всё строится кодом |
| `Assets/Resources/Levels/` | Готовые уровни (генерируются, руками не править) |
| `levels-src/` | Исходники уровней: картинка буквами, палитра, параметры сложности |
| `dotnet/` | Тесты ядра, генератор уровней, проверка компиляции игры |
| `docs/GDD.md` | Дизайн игры |
| `docs/ROADMAP.md` | Дорожная карта: версии 1.1–2.0 |

## Запуск в Unity (Mac на M1)

1. Unity Hub → Installs → Unity 6 LTS (модули Android/iOS можно поставить позже).
2. Unity Hub → Projects → **Add** → **Add project from disk** → выбрать папку репозитория (ту, где лежит `Assets`).
3. Если Hub пишет, что версия редактора другая, — выбрать установленную Unity 6.
4. При первом открытии Unity скачает пакеты и создаст остальные настройки. Если спросит про новую систему ввода (Input System) — согласиться, редактор перезапустится.
5. Нажать **Play** — игра создаёт себя сама в любой сцене, даже в пустой.
6. В Game view выбрать размер телефона (например, 1170×2532 портрет) или включить Simulator.
7. Закоммитить файлы, которые создаст Unity (`.meta`, `ProjectSettings/*`, `Packages/packages-lock.json`).

Для сборки на телефон: Player Settings → Default Orientation = Portrait.

Языки: русский и английский. Новую строку для игрока пишите как `L.T("Текст")` или `L.F("Текст {0}", x)` и добавьте перевод в `Assets/Scripts/Core/Loc/En.cs`; без перевода упадёт `LocalizationTests`.

Звуки интерфейса: `Assets/Resources/Sounds` — записи из Kenney Interface Sounds (CC0, [лицензия](docs/licenses/Kenney-Interface-Sounds.txt)); победа, звёзды, комбо и тупик синтезируются кодом. Чтобы заменить звук, положите файл с тем же именем; удалите файл — зазвучит синтезированный вариант.

Шрифт: по умолчанию встроенный. Чтобы поставить Nunito, положите `Nunito-Black.ttf` как `Assets/Resources/Fonts/Main.ttf`.

## Иконка

Иконка рисуется скриптом `tools/icon/make_icon.py` (нужны `pip install pillow numpy`) и лежит в `Assets/Icons/`:

| Файл | Куда в Unity |
|---|---|
| `AppIcon.png` | Player Settings → Icon → Default Icon; Android → Legacy и Round |
| `AppIconBackground.png` | Android → Adaptive → Background |
| `AppIconForeground.png` | Android → Adaptive → Foreground (всё важное в безопасной зоне) |

## Уровни

Исходник уровня (`levels-src/level_001.json`):

```json
{
  "id": 1,
  "name": "Сердечко",
  "tip": "Тапни клубок — он поедет на станок и довяжет сердечко на ткани",
  "palette": [{ "key": "b", "hex": "#FFE3EC" }, { "key": "p", "hex": "#FF5C8A" }, { "key": "w", "hex": "#FFFFFF" }],
  "picture": ["bppbppb", "pwppppp", "ppppppp", "bpppppb", "bbpppbb", "bbbpbbb"],
  "fabric": "b",
  "slots": 4, "columns": 3, "spools": 4,
  "minWin": 0.9, "maxWin": 1.0, "seed": 11
}
```

- `picture` — ряды сверху вниз, буквы из `palette`.
- `fabric` — буквы ткани-основы: эти клетки связаны с начала, вяжется только рисунок.
- `band` — натяжение полотна: на сколько рядов фронт может обогнать самый нижний (0 — без ограничения).
- `junk` — чужая пряжа: клубки цвета ткани, распускаются через 6 шагов после посадки; ножницы освобождают место сразу.
- `spools`, `columns`, `slots` — сколько клубков, колонок и слотов.
- `swaps` — перестановки соседних клубков: чем больше, тем запутаннее очередь.
- `hidden`, `knots`, `links`, `locks`, `rainbow`, `thick`, `ice` — сколько особых клубков.
- `extra` — лишние клубки сверх картинки, их можно не брать.
- `gold` — сколько золотых петель, `mystery` — скрыть эскиз.
- `minWin`..`maxWin` — допустимая доля побед бота: так задаётся сложность.
- `maxForced`, `minDanger`..`maxDanger` — коридоры метрик решений: доля ходов без выбора и доля роковых ходов.
- `maxRandom` — сколько партий может выиграть игрок, который тапает наугад.

Подробное руководство по созданию и правке уровней — [docs/LEVELS.md](docs/LEVELS.md).

Пересобрать все уровни (или только часть: `-- --only 16-20`):

```bash
dotnet run --project dotnet/KnittedPicture.LevelTool
```

Генератор перебирает сиды, пока уровень не станет решаемым, бот не попадёт в диапазон, а метрики — в коридоры. Затем печатает таблицу: размер, петли, клубки, тапы и цель, долю побед бота, цветов на фронте, долю ходов без выбора и роковых ходов. Результат детерминирован, CI проверяет, что `Assets/Resources/Levels` совпадает с исходниками.

## Проверки

Нужен .NET 8 SDK (`brew install --cask dotnet-sdk`).

```bash
dotnet test dotnet/KnittedPicture.Core.Tests     # правила, экономика, все уровни решаемы
dotnet build dotnet/KnittedPicture.Game.Check    # код игры компилируется
dotnet build dotnet/KnittedPicture.Game.Check -p:UnityPlatform=UNITY_ANDROID   # и код под Android
dotnet build dotnet/KnittedPicture.Game.Check "-p:UnityPlatform=UNITY_ANDROID%3BKNITTED_ANDROIDJNI"   # Android с модулем JNI
dotnet build dotnet/KnittedPicture.Game.Check -p:UnityPlatform=UNITY_IOS       # и код под iOS
```

`KnittedPicture.Game.Check` компилирует `Assets/Scripts/Game` против справочных сборок Unity из NuGet и заглушек uGUI. Он ловит ошибки компиляции, но не заменяет запуск в редакторе. Код под `#if UNITY_ANDROID` / `#if UNITY_IOS` проверяется только с `-p:UnityPlatform=…`. Тонкой вибрации на Android нужен встроенный модуль `com.unity.modules.androidjni` (он в `Packages/manifest.json`). Вибрация лежит в своей сборке `KnittedPicture.Haptics` с define `KNITTED_ANDROIDJNI`: без модуля игра всё равно собирается, просто на Android остаётся только системная вибрация на победу и затор.
