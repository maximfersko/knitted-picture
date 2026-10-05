# Вязаная картинка

Hybrid-casual головоломка: игрок отправляет клубки на станок, станок сам вяжет пиксельную картинку ряд за рядом. Движок — Unity 6 LTS, 2D. 15 уровней, 3 коллекции альбома, 4 бустера.

Полный дизайн игры — [docs/GDD.md](docs/GDD.md).

## Структура

| Путь | Что там |
|---|---|
| `Assets/Scripts/Core/` | Правила без Unity: станок, особые клубки, моль, бустеры, валидатор, решатель, экономика, прогресс |
| `Assets/Scripts/Game/` | Игра в Unity: экраны, анимации, звук, сохранения, заглушка рекламы — всё строится кодом |
| `Assets/Resources/Levels/` | Готовые уровни (генерируются, руками не править) |
| `levels-src/` | Исходники уровней: картинка буквами, палитра, параметры сложности |
| `dotnet/` | Тесты ядра, генератор уровней, проверка компиляции игры |
| `docs/GDD.md` | Дизайн игры |

## Запуск в Unity (Mac на M1)

1. Unity Hub → Installs → Unity 6 LTS с модулями Android Build Support и iOS Build Support.
2. New project → шаблон **Universal 2D** → создать во временной папке.
3. Скопировать из неё в корень репозитория папки `Packages/` и `ProjectSettings/`.
4. Unity Hub → Add → Add project from disk → папка репозитория.
5. Открыть любую сцену (например, `Assets/Scenes/SampleScene` из шаблона) и нажать Play. Игра создаёт себя сама — префабы и сцены настраивать не нужно.
6. В Game view выбрать размер телефона (например, 1170×2532 портрет) или включить Simulator.
7. Unity создаст `.meta`-файлы для скриптов и уровней — их нужно закоммитить.

Для сборки на телефон: Player Settings → Default Orientation = Portrait.

Шрифт: по умолчанию встроенный. Чтобы поставить Nunito, положите `Nunito-Black.ttf` как `Assets/Resources/Fonts/Main.ttf`.

## Уровни

Исходник уровня (`levels-src/level_001.json`):

```json
{
  "id": 1,
  "name": "Сердечко",
  "tip": "Тапни клубок — он поедет на станок и начнёт вязать картинку",
  "palette": [{ "key": "b", "hex": "#6CB8FF" }, { "key": "p", "hex": "#FF5C93" }],
  "picture": ["ppbbpp", "pppppp", "pppppp", "bppppb", "bbppbb", "bbbbbb"],
  "slots": 5, "columns": 4, "spools": 4,
  "swaps": 0, "hidden": 0, "knots": 0, "links": 0, "locks": 0,
  "hard": false, "timeLimit": 0,
  "minWin": 0.9, "maxWin": 1.0, "seed": 11
}
```

- `picture` — ряды сверху вниз, буквы из `palette`.
- `spools`, `columns`, `slots` — сколько клубков, колонок и слотов.
- `swaps` — перестановки соседних клубков: чем больше, тем запутаннее очередь.
- `hidden`, `knots`, `links`, `locks` — сколько особых клубков.
- `minWin`..`maxWin` — допустимая доля побед бота: так задаётся сложность.

Пересобрать все уровни:

```bash
dotnet run --project dotnet/KnittedPicture.LevelTool
```

Генератор перебирает сиды, пока уровень не станет решаемым и бот не попадёт в диапазон, и печатает таблицу: размер, клубки, длину решения, долю побед бота. Результат детерминирован, CI проверяет, что `Assets/Resources/Levels` совпадает с исходниками.

## Проверки

Нужен .NET 8 SDK (`brew install --cask dotnet-sdk`).

```bash
dotnet test dotnet/KnittedPicture.Core.Tests     # правила, экономика, все уровни решаемы
dotnet build dotnet/KnittedPicture.Game.Check    # код игры компилируется
```

`KnittedPicture.Game.Check` компилирует `Assets/Scripts/Game` против справочных сборок Unity из NuGet и заглушек uGUI. Он ловит ошибки компиляции, но не заменяет запуск в редакторе.
