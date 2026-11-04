# Вязаная картинка: правила для Claude

Мобильная головоломка на Unity 6 (uGUI целиком в коде, New Input System). Ядро игры и мета — `Assets/Scripts/Core`,
экраны и вид — `Assets/Scripts/Game`, проверочные проекты — `dotnet/`. Подробности: `README.md`, `docs/GDD.md`,
`docs/ECONOMY.md`, `docs/LEVELS.md`. Текущее состояние работы и принятые решения: `docs/HANDOFF.md`.

## Общение и git
- Отвечать по-русски. Честно говорить, что Unity в этом окружении не запускается: визуальное проверяется сборкой и расчётом.
- Коммит, push и pull request выполнять только по явной просьбе пользователя.
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

## Порядок работы
Канонический порядок ролей и согласования — в `AGENTS.md`.

## Полировка интерфейса: скилл `make-interfaces-feel-better`
Скилл лежит в `.claude/skills/make-interfaces-feel-better/` (MIT, источник github.com/jakubkrehel/make-interfaces-feel-better,
файлы не правим, чтобы обновлять копированием). Он написан для веба; в этом проекте его правила читаются так:
- CSS transition (прерываемая) → `Tweens.Run` с `Tweens.Kill(owner, channel)` перед новым движением, от текущего значения;
  keyframes → одноразовые последовательности; `transition: all` → твин меняет только нужное свойство.
- `scale(0.96)` при нажатии → пресс в `Tap`; не меньше 0.95; для частых действий без лишнего движения.
- box-shadow вместо border → мягкие тени-спрайты из `Art`; разделители и состояния (выбрано, фокус) остаются обводкой.
- Концентрические радиусы: внешний = внутренний + отступ (скругления `Art.Round` и подложки карточек).
- tabular-nums → у `Text` нет моноширинных цифр: для меняющихся чисел фиксированная ширина и выравнивание по краю,
  чтобы соседи не прыгали; balance/pretty → ручные переносы и ширины без висячих слов (русский и английский).
- Зона касания не меньше 44×44 точек, зоны соседних элементов не пересекаются.
- Иконки: один спрайт, состояния цветом и прозрачностью; смена иконки → масштаб 0.25→1 и прозрачность 0→1 за ~0.3 с
  без отскока (размытия в uGUI нет, его пропускаем).
- Сдержанность: частые действия без своей анимации, движение никогда не единственный сигнал;
  при «Меньше движения» статичный признак (цвет, значок, подпись) остаётся.
Ревью интерфейса оформляется по разделу Review Output Format скилла (режимы quick и full).
