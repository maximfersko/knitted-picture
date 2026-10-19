# Вязаная картинка: правила для Claude

Мобильная головоломка на Unity 6 (uGUI целиком в коде, New Input System). Ядро игры и мета — `Assets/Scripts/Core`,
экраны и вид — `Assets/Scripts/Game`, проверочные проекты — `dotnet/`. Подробности: `README.md`, `docs/GDD.md`,
`docs/ECONOMY.md`, `docs/LEVELS.md`. Текущее состояние работы и принятые решения: `docs/HANDOFF.md`.

## Общение и git
- Отвечать по-русски. Честно говорить, что Unity в этом окружении не запускается: визуальное проверяется сборкой и расчётом.
- Ветка `claude/focused-thompson-u3i4v7` и `main`. Перед push: `git pull -q --no-rebase origin main`, затем
  `git push -q origin HEAD:claude/focused-thompson-u3i4v7 HEAD:main`. Pull request без просьбы не создавать.
- В коммитах, документах и коде не упоминать названия и идентификаторы моделей.

## Правила кода и текстов
- Каждая строка для игрока: `L.T("...")` или `L.F("...", args)` и запись в `Assets/Scripts/Core/Loc/En.cs`
  (плейсхолдеры совпадают, без дубликатов ключей). Это проверяет `LocalizationTests`.
- В текстах для игрока нет длинных тире; формулировки гендерно-нейтральные.
- Цвета уровней только из `YarnColors`; цвета интерфейса из `Palette`.
- Не менять `ProjectSettings/` и `Packages/`. Файлы `.meta` не хранятся в git.
- Стиль: подробные русские XML-комментарии `/// <summary>`, `#nullable enable`, фигурные скобки всегда, как в соседнем коде.
- Анимации через `Tweens.Run`; при `Tweens.Calm` («Меньше движения») повторяющиеся твины замирают в `step(0)`:
  это состояние должно выглядеть спокойно.
- Решение дизайна: мотки пряжи и рамки меняют только награды и вид, а не правила головоломки (три звезды честные).

## Проверки перед коммитом
```
for p in UNITY_ANDROID UNITY_IOS KNITTED_QA "UNITY_ANDROID%3BKNITTED_ANDROIDJNI" "UNITY_ANDROID%3BSTORE_RUSTORE%3BKNITTED_YANDEX_ADS%3BKNITTED_RUSTORE_PAY%3BKNITTED_APPMETRICA%3BSDK_STUBS"; do dotnet build -c Release dotnet/KnittedPicture.Game.Check -p:UnityPlatform="$p"; done
dotnet test -c Release dotnet/KnittedPicture.Core.Tests
dotnet run -c Release --project dotnet/KnittedPicture.QA -- --quick
dotnet run -c Release --project dotnet/KnittedPicture.Playtest -- --runs 100            # выбивающиеся уровни: нет
dotnet run -c Release --project dotnet/KnittedPicture.Playtest -- --economy --players 200 --days 60   # если трогали экономику
dotnet run -c Release --project dotnet/KnittedPicture.ArtPreview -- <папка>             # если трогали растры
dotnet run -c Release --project dotnet/KnittedPicture.LevelTool -- --only N-N           # пересборка уровня из levels-src
```

## Порядок работы (роли в `.claude/agents/`)
1. `architect` (Opus) — анализ и план: файлы, решения, риски, критерии готовности. План показывается пользователю.
2. После подтверждения пользователя `implementer` (Sonnet) реализует и гоняет проверки, не коммитит.
3. `reviewer` (Opus) ревьюит дифф: вердикт и замечания по важности.
4. `implementer` исправляет, `reviewer` проверяет повторно.
5. Главная сессия коммитит и пушит, коротко отчитывается пользователю.
