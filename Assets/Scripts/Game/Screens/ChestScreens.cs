#nullable enable

using System;
using System.Collections.Generic;
using KnittedPicture.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Открытие сундука на весь экран, спокойно и по шагам:
    /// сундук мягко опускается и дышит → тап → покачивание, крышка откидывается, тёплый свет и пушинки →
    /// монеты вылетают в «лоток» → выбор бустера (1 из 3) → карточки мотков ложатся рубашкой
    /// с цветом редкости и по очереди переворачиваются → выбор мотка (1 из 3) → итог.
    /// Награда записывается только в самом конце: закрыл посередине — сундук остался тем же.
    /// </summary>
    public sealed class ChestOpening : Popup
    {
        private enum Step
        {
            Closed,
            Opening,
            Boosters,
            Yarns,
            Done,
        }

        private const float CardH = 150f;

        private readonly ChestContents _contents;
        private readonly Action _finished;
        private readonly RectTransform _stage;
        private readonly float _w;
        private readonly float _h;
        private readonly ChestView _chest;
        private readonly Text _hint;
        private readonly Text _stepTitle;
        private readonly Text _description;
        private readonly RectTransform _button;
        private readonly Text _buttonLabel;
        private readonly CanvasGroup _buttonGroup;
        private readonly List<RectTransform> _tray = new List<RectTransform>();
        private readonly List<(RectTransform root, RectTransform visual, CanvasGroup group, Image outline)> _cards =
            new List<(RectTransform, RectTransform, CanvasGroup, Image)>();

        private Step _step;
        private int _pick = -1;
        private int _booster = -1;
        private bool _claimed;
        private bool _selectable;
        private Action? _confirm;

        public ChestOpening(App app, Action finished)
            : base(app)
        {
            _finished = finished;
            _contents = app.Progress.PeekChest();
            Card.gameObject.SetActive(false);
            var dim = Root.Find("Dim").GetComponent<Image>();
            dim.color = new Color(0.12f, 0.1f, 0.22f, 0.88f);

            // Сцена совпадает с колонкой экрана: те же координаты, что и у остальных экранов.
            _stage = Ui.Node(Root, "Stage");
            var frame = app.Frame;
            _stage.anchorMin = frame.anchorMin;
            _stage.anchorMax = frame.anchorMax;
            _stage.pivot = frame.pivot;
            _stage.anchoredPosition = frame.anchoredPosition;
            _stage.sizeDelta = frame.sizeDelta;
            _w = app.Width;
            _h = app.Height;

            var catcher = Ui.Image(_stage, "Hit", null, Color.clear, true);
            Ui.Stretch(catcher.rectTransform);
            var catcherTap = catcher.gameObject.AddComponent<Tap>();
            catcherTap.PressFeedback = false;
            catcherTap.ClickSound = false;
            catcherTap.OnClick(OnStageTap);

            Ui.Label(_stage, _contents.Big ? "Большой сундук" : "Звёздный сундук", 26, Color.white, 16, _h * 0.06f, _w - 32, 36);
            _chest = ChestView.Create(_stage, _w / 2f, _h * 0.42f, 210f, _contents.Big);
            _hint = Ui.Label(_stage, "Тапни, чтобы открыть", 16, Palette.WithAlpha(Color.white, 0.8f), 16, _h * 0.42f + 120f, _w - 32, 24, false);
            _stepTitle = Ui.Label(_stage, string.Empty, 21, Color.white, 16, StepTitleY, _w - 32, 30);
            _description = Ui.Label(_stage, string.Empty, 14, Palette.WithAlpha(Color.white, 0.82f), 24, CardsTop + CardH + 14f, _w - 48, 44, false);
            _description.lineSpacing = 0.95f;
            Ui.Fit(_description, 11);

            var tap = Ui.Button(_stage, "Взять", 24, _h - 96f, _w - 48f, 60f, Palette.Primary, Palette.PrimaryShadow, Color.white, 20);
            _button = (RectTransform)tap.transform;
            _buttonLabel = _button.GetComponentInChildren<Text>();
            _buttonGroup = _button.gameObject.AddComponent<CanvasGroup>();
            _buttonGroup.alpha = 0f;
            _buttonGroup.blocksRaycasts = false;
            tap.OnClick(() => _confirm?.Invoke());

            // Сундук мягко опускается на место и начинает «дышать».
            var chest = _chest.Root;
            var rest = chest.anchoredPosition;
            Tweens.Run(chest, 0.7f, t => chest.anchoredPosition = rest + new Vector2(0, 260f * (1f - t)), Ease.OutCubic, () =>
            {
                Tweens.Run(chest, 0.25f, t => chest.localScale = new Vector3(1f + 0.03f * Ease.Bump(t), 1f - 0.04f * Ease.Bump(t), 1f));
                _chest.Breathe();
                _chest.Shine(0.28f, 0.06f);
            });
            var hint = _hint;
            Tweens.Run(hint, 1.8f, t => hint.color = Palette.WithAlpha(Color.white, 0.45f + 0.4f * Ease.Bump(t)), null, null, 0.8f, true);
        }

        /// <summary>Назад можно, пока награда не записана: сундук останется тем же.</summary>
        public override bool Closable => !_claimed && _step != Step.Opening;

        protected override bool CloseOnDim => false;

        private float ChestTopY => Mathf.Max(_h * 0.19f, 132f);

        private float TrayY => ChestTopY + 96f;

        private float StepTitleY => TrayY + 34f;

        private float CardsTop => StepTitleY + 44f;

        private float CardW => Mathf.Min(104f, (_w - 32f - 20f) / 3f);

        private void OnStageTap()
        {
            if (_step != Step.Closed)
            {
                return;
            }

            _step = Step.Opening;
            Tweens.Fade(_hint.gameObject.AddComponent<CanvasGroup>(), 1f, 0f, 0.2f);
            _chest.Open(() =>
            {
                _chest.Fluff(_stage, new[] { Palette.CoinSoft, Palette.Hex("#F3D9C4"), Palette.Hex("#E2D8FF"), Color.white }, 16);
                var chest = _chest.Root;
                var from = chest.anchoredPosition;
                var to = new Vector2(from.x, -ChestTopY);
                Tweens.Run(chest, 0.6f, t =>
                {
                    chest.anchoredPosition = Vector2.Lerp(from, to, t);
                    chest.localScale = Vector3.one * Mathf.Lerp(1f, 0.66f, t);
                }, Ease.InOutCubic, () =>
                {
                    ShowCoins();
                    Tweens.Delay(_stage, 0.7f, ShowBoosters);
                });
            });
        }

        // ---------- Лоток: что уже твоё ----------

        private RectTransform AddTrayChip(Action<RectTransform> icon, string text)
        {
            const float w = 104f;
            var chip = Ui.Card(_stage, "Chip", 0, TrayY, w, 40, 20, Palette.Card, Palette.CardShadow, 2f);
            chip.pivot = new Vector2(0.5f, 0.5f);
            icon(chip);
            Ui.Fit(Ui.Label(chip, text, 15, Palette.Ink, 40, 0, w - 46, 40, true, TextAnchor.MiddleLeft), 10);
            _tray.Add(chip);
            chip.localScale = Vector3.zero;
            Tweens.Scale(chip, 0.3f, 1f, 0.35f, Ease.OutBack);
            LayoutTray();
            return chip;
        }

        /// <summary>Фишки лотка — ровным рядом по центру; новая сдвигает остальные плавно.</summary>
        private void LayoutTray()
        {
            const float w = 104f;
            const float gap = 8f;
            var x0 = _w / 2f - (_tray.Count * w + (_tray.Count - 1) * gap) / 2f;
            for (var i = 0; i < _tray.Count; i++)
            {
                var chip = _tray[i];
                var from = chip.anchoredPosition;
                var to = new Vector2(x0 + i * (w + gap) + w / 2f, -(TrayY + 20f));
                if (i == _tray.Count - 1)
                {
                    chip.anchoredPosition = to;
                    continue;
                }

                Tweens.Run(chip, 0.35f, t => chip.anchoredPosition = Vector2.Lerp(from, to, t), Ease.OutCubic);
            }
        }

        private void ShowCoins()
        {
            Text? label = null;
            var chip = AddTrayChip(c => Ui.Coin(c, 22, 20, 24), "0");
            label = chip.GetComponentInChildren<Text>();
            var total = _contents.Coins;
            var target = Ui.WorldCenter(chip);
            var from = _chest.Mouth;
            var unit = _stage.lossyScale.x;
            const int flyers = 7;
            for (var i = 0; i < flyers; i++)
            {
                var coin = Ui.Coin(_stage, 0, 0, 20);
                coin.position = from;
                var control = (from + target) / 2f + new Vector3((i - flyers / 2f) * 14f * unit, 60f * unit, 0);
                var index = i;
                Tweens.Run(coin, 0.55f, t =>
                {
                    var u = 1f - t;
                    coin.position = u * u * from + 2f * u * t * control + t * t * target;
                    coin.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, Ease.Bump(t) + t * 0.5f);
                }, Ease.InOutCubic, () =>
                {
                    UnityEngine.Object.Destroy(coin.gameObject);
                    Sfx.Play(Sound.Coin, 0.35f);
                    if (label != null)
                    {
                        label.text = "+" + Mathf.RoundToInt(total * (index + 1f) / flyers);
                    }

                    Tweens.Punch(chip, 0.08f, 0.16f);
                }, i * 0.07f);
            }
        }

        // ---------- Карточки выбора ----------

        /// <summary>Пустая карточка в слоте index; видимая часть вылетает из сундука и встаёт на место.</summary>
        private (RectTransform root, RectTransform visual, CanvasGroup group, Image outline) AddCard(int index, int count, float delay)
        {
            var w = CardW;
            const float gap = 10f;
            var x0 = _w / 2f - (count * w + (count - 1) * gap) / 2f;
            var root = Ui.Place(Ui.Node(_stage, "Choice" + index), x0 + index * (w + gap), CardsTop, w, CardH);
            var hit = Ui.Image(root, "Hit", null, Color.clear, true);
            Ui.Stretch(hit.rectTransform);
            var visual = Ui.PlaceCenter(Ui.Node(root, "Visual"), w / 2f, CardH / 2f, w, CardH);
            var group = visual.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            var shadow = Ui.Box(visual, "Shadow", 20, Palette.WithAlpha(Palette.Ink, 0.35f));
            Ui.Place(shadow.rectTransform, 0, 4, w, CardH);
            var face = Ui.Box(visual, "Face", 20, Palette.Card);
            Ui.Place(face.rectTransform, 0, 0, w, CardH);
            var outline = Ui.Outline(visual, 20, Palette.Primary, 3f);
            outline.enabled = false;

            var rest = visual.anchoredPosition;
            var start = (Vector2)root.InverseTransformPoint(_chest.Mouth);
            visual.anchoredPosition = start;
            visual.localScale = Vector3.one * 0.3f;
            group.alpha = 0f;
            Tweens.Run(visual, 0.55f, t =>
            {
                visual.anchoredPosition = Vector2.LerpUnclamped(start, rest, t);
                visual.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, t);
                group.alpha = Mathf.Clamp01(t * 3f);
            }, Ease.OutCubic, () => Sfx.Play(Sound.Pick, 0.4f), delay);

            var card = (root, visual, group, outline);
            _cards.Add(card);
            var tap = root.gameObject.AddComponent<Tap>();
            tap.PressFeedback = false;
            tap.OnClick(() => Select(index));
            return card;
        }

        /// <summary>Выбор: карточка приподнимается с зелёной рамкой, остальные отступают в тень.</summary>
        private void Select(int index)
        {
            if (!_selectable || (_step != Step.Boosters && _step != Step.Yarns))
            {
                return;
            }

            _pick = index;
            for (var i = 0; i < _cards.Count; i++)
            {
                var (_, visual, group, outline) = _cards[i];
                var chosen = i == index;
                outline.enabled = chosen;
                var fromScale = visual.localScale.x;
                var fromAlpha = group.alpha;
                var fromY = visual.anchoredPosition.y;
                var rest = -CardH / 2f;
                Tweens.Run(visual, 0.22f, t =>
                {
                    visual.localScale = Vector3.one * Mathf.Lerp(fromScale, chosen ? 1.05f : 0.94f, t);
                    group.alpha = Mathf.Lerp(fromAlpha, chosen ? 1f : 0.55f, t);
                    visual.anchoredPosition = new Vector2(visual.anchoredPosition.x, Mathf.Lerp(fromY, rest + (chosen ? 10f : 0f), t));
                }, Ease.OutCubic);
            }

            if (_step == Step.Boosters)
            {
                var type = _contents.Boosters[index];
                _description.text = BoosterInfo.Description(type);
            }
            else
            {
                var yarn = YarnCatalog.All[_contents.Yarns[index]];
                var set = SetOf(yarn.Id);
                _description.text = App.Progress.HasYarn(yarn.Id)
                    ? $"Такой уже есть: повтор даст {YarnCatalog.DuplicateCoins(yarn.Rarity)} монет"
                    : $"{yarn.Description}. Набор «{set}»";
            }

            ShowButton("Взять");
        }

        private void ShowButton(string text)
        {
            _buttonLabel.text = text;
            if (_buttonGroup.alpha < 0.99f)
            {
                _buttonGroup.blocksRaycasts = true;
                Tweens.Fade(_buttonGroup, _buttonGroup.alpha, 1f, 0.2f);
            }
        }

        private void HideButton()
        {
            _buttonGroup.blocksRaycasts = false;
            Tweens.Fade(_buttonGroup, _buttonGroup.alpha, 0f, 0.15f);
        }

        /// <summary>Выбранная карточка улетает в лоток, остальные тают.</summary>
        private void ClearCards(int chosen, Vector3 to)
        {
            for (var i = 0; i < _cards.Count; i++)
            {
                var (root, visual, group, _) = _cards[i];
                root.GetComponent<Tap>().Interactable = false;
                if (i == chosen)
                {
                    var from = visual.position;
                    Tweens.Run(visual, 0.4f, t =>
                    {
                        visual.position = Vector3.Lerp(from, to, t);
                        visual.localScale = Vector3.one * Mathf.Lerp(1.05f, 0.25f, t);
                        group.alpha = 1f - t * t;
                    }, Ease.InOutCubic, () => UnityEngine.Object.Destroy(root.gameObject));
                }
                else
                {
                    Tweens.Run(visual, 0.25f, t => group.alpha = Mathf.Lerp(0.55f, 0f, t), Ease.OutCubic,
                        () => UnityEngine.Object.Destroy(root.gameObject));
                }
            }

            _cards.Clear();
            _pick = -1;
            _selectable = false;
            _description.text = string.Empty;
        }

        // ---------- Шаг 1: бустер ----------

        private void ShowBoosters()
        {
            _step = Step.Boosters;
            _stepTitle.text = "Выбери бустер";
            var count = _contents.BoosterCount;
            _description.text = count > 1 ? "Большой сундук: возьмёшь сразу 2 штуки" : "Один из трёх. Нажми, чтобы узнать, что он делает";
            var options = _contents.Boosters;
            Tweens.Delay(_stage, 0.1f * (options.Length - 1) + 0.6f, () => _selectable = true);
            for (var i = 0; i < options.Length; i++)
            {
                var type = options[i];
                var (_, visual, _, _) = AddCard(i, options.Length, i * 0.1f);
                var w = CardW;
                var circle = Ui.Image(visual, "Back", Art.Circle, Palette.PrimarySoft);
                Ui.PlaceCenter(circle.rectTransform, w / 2f, 46, 60, 60);
                Ui.IconImage(visual, BoosterInfo.Icon(type), Palette.Primary, w / 2f, 46, 32);
                Ui.Fit(Ui.Label(visual, BoosterInfo.Name(type), 15, Palette.Ink, 6, 82, w - 12, 22), 11);
                var chip = Ui.Box(visual, "Count", 11, Palette.Surface).rectTransform;
                Ui.PlaceCenter(chip, w / 2f, 116, 44, 22);
                Ui.Label(chip, "×" + count, 13, Palette.Ink, 0, 0, 44, 22);
                Ui.Fit(Ui.Label(visual, $"в запасе {App.Progress.Stock(type)}", 11, Palette.Ink3, 4, 130, w - 8, 16, false), 9);
            }

            _confirm = () =>
            {
                if (_pick < 0)
                {
                    return;
                }

                _booster = _pick;
                var type = _contents.Boosters[_booster];
                HideButton();
                var chip = AddTrayChip(c =>
                {
                    var circle = Ui.Image(c, "Back", Art.Circle, Palette.PrimarySoft);
                    Ui.PlaceCenter(circle.rectTransform, 22, 20, 30, 30);
                    Ui.IconImage(c, BoosterInfo.Icon(type), Palette.Primary, 22, 20, 18);
                }, "×" + _contents.BoosterCount);
                ClearCards(_booster, Ui.WorldCenter(chip));
                Tweens.Delay(_stage, 0.45f, ShowYarns);
            };
        }

        // ---------- Шаг 2: моток ----------

        private void ShowYarns()
        {
            _step = Step.Yarns;
            _stepTitle.text = "Выбери моток";
            _description.text = "Рубашка подсказывает редкость";
            var options = _contents.Yarns;
            Tweens.Delay(_stage, 0.75f + 0.4f * (options.Length - 1) + 0.45f, () => _selectable = true);
            for (var i = 0; i < options.Length; i++)
            {
                var yarn = YarnCatalog.All[options[i]];
                var card = AddCard(i, options.Length, i * 0.1f);
                var visual = card.visual;
                var w = CardW;

                // Рубашка: цвет редкости и «?».
                var back = Ui.Node(visual, "Back");
                Ui.Place(back, 0, 0, w, CardH);
                var (tint, ink) = RarityTint(yarn.Rarity);
                var backFace = Ui.Box(back, "Face", 20, tint);
                Ui.Place(backFace.rectTransform, 0, 0, w, CardH);
                Ui.Outline(back, 20, Palette.WithAlpha(ink, 0.5f), 2f);
                for (var k = 0; k < 3; k++)
                {
                    var stitch = Ui.Image(back, "Stitch", Art.Icon(Icon.Check), Palette.WithAlpha(ink, 0.18f));
                    Ui.PlaceCenter(stitch.rectTransform, w / 2f, 34 + k * 16, 26, 20);
                }

                Ui.Label(back, "?", 34, ink, 0, 62, w, 44);
                CollectionsScreen.RarityChip(back, yarn.Rarity, w / 2f, 128f);

                // Лицо: моток, название, новый или повтор.
                var front = Ui.Node(visual, "Front");
                Ui.Place(front, 0, 0, w, CardH);
                YarnView.Create(front, yarn, w / 2f, 46, 62, true);
                var name = Ui.Label(front, yarn.Name, 13, Palette.Ink, 6, 80, w - 12, 34);
                name.lineSpacing = 0.85f;
                Ui.Fit(name, 10);
                var isNew = !App.Progress.HasYarn(yarn.Id);
                var chipText = isNew ? "новый!" : $"+{YarnCatalog.DuplicateCoins(yarn.Rarity)} монет";
                var chip = Ui.Box(front, "State", 11, isNew ? Palette.DangerSoft : Palette.CoinSoft).rectTransform;
                Ui.PlaceCenter(chip, w / 2f, 130, w - 16, 22);
                Ui.Fit(Ui.Label(chip, chipText, 12, isNew ? Palette.Danger : Palette.CoinShadow, 0, 0, w - 16, 22), 9);
                front.gameObject.SetActive(false);

                // Переворот по очереди, когда все карточки легли.
                var rare = yarn.Rarity != Rarity.Common;
                var flipDelay = 0.75f + i * 0.4f;
                Tweens.Delay(visual, flipDelay, () => Flip(visual, back, front, rare ? ink : Palette.Ink3));
            }

            _confirm = () =>
            {
                if (_pick < 0)
                {
                    return;
                }

                Claim(_pick);
            };
        }

        private void Flip(RectTransform visual, RectTransform back, RectTransform front, Color sparkle)
        {
            var baseScale = visual.localScale.x;
            Tweens.Run(back, 0.16f, t => visual.localScale = new Vector3(baseScale * (1f - t), baseScale, 1f), Ease.InOutCubic, () =>
            {
                back.gameObject.SetActive(false);
                front.gameObject.SetActive(true);
                Sfx.Play(Sound.Star, 0.35f);
                Tweens.Run(front, 0.2f, t =>
                {
                    var s = visual.localScale.y;
                    visual.localScale = new Vector3(s * t, s, 1f);
                }, Ease.OutCubic, () => Fx.Sparkle(_stage, Ui.WorldCenter(visual), sparkle, 6, 46f));
            });
        }

        private static (Color tint, Color ink) RarityTint(Rarity rarity) =>
            rarity == Rarity.Common ? (Palette.Surface, Palette.Ink3)
            : rarity == Rarity.Rare ? (Palette.Hex("#EDE4FF"), Palette.Link)
            : (Palette.CoinSoft, Palette.CoinShadow);

        private static string SetOf(int yarn)
        {
            foreach (var set in YarnCatalog.Sets)
            {
                if (set.Contains(yarn))
                {
                    return set.Name;
                }
            }

            return string.Empty;
        }

        // ---------- Итог ----------

        private void Claim(int yarnChoice)
        {
            var granted = App.Progress.ClaimChest(_booster, yarnChoice);
            App.Save();
            _claimed = true;
            _step = Step.Done;
            HideButton();
            var yarn = YarnCatalog.All[_contents.Yarns[yarnChoice]];
            var duplicate = granted != null && !granted.Value.NewYarn;
            var chip = AddTrayChip(c => YarnView.Create(c, yarn, 22, 20, 28, true), duplicate ? "+" + granted!.Value.DuplicateCoins : yarn.Name);
            ClearCards(yarnChoice, Ui.WorldCenter(chip));

            _stepTitle.text = "Всё в корзинке";
            _description.text = duplicate
                ? $"«{yarn.Name}» уже был: повтор стал монетами"
                : $"Новый моток «{yarn.Name}» в коллекции";
            _description.rectTransform.anchoredPosition = new Vector2(24, -(CardsTop + 10f));
            Fx.Sparkle(_stage, Ui.WorldCenter(chip), Palette.Coin, 10, 60f);
            Sfx.Play(Sound.Win, 0.5f);
            Tweens.Delay(_stage, 0.5f, () => ShowButton("Готово"));
            _confirm = () =>
            {
                Close();
                _finished();
            };
        }
    }

    /// <summary>
    /// «Сундук и запасы»: сундук с прогрессом и циклом (каждый 4-й большой), запас бустеров
    /// с описаниями и корзинка пряжи. Здесь видно, что у тебя есть и что будет дальше.
    /// </summary>
    public sealed class ChestScreen : ScreenView
    {
        public ChestScreen(App app)
            : base(app, "ChestScreen")
        {
            Ui.RoundButton(Root, Icon.Back, 16, 8).OnClick(app.ShowHome);
            Ui.Fit(Ui.Label(Root, "Сундук и запасы", 22, Palette.Ink, 66, 8, W - 66 - 136, 44), 15);
            Ui.CoinPill(Root, app.Progress.Coins, W - 16, 8);
            var h = H - 64f;
            var content = Ui.Scroll(Root, 0, 64, W, h);
            var y = 6f;
            y = BuildChest(content, y) + 16f;
            y = BuildStock(content, y) + 16f;
            y = BuildBasket(content, y) + 16f;
            content.sizeDelta = new Vector2(W, Mathf.Max(y + 24f, h));
        }

        private float BuildChest(RectTransform content, float y)
        {
            var progress = App.Progress;
            var cardW = W - 32f;
            const float cardH = 380f;
            var card = Ui.Card(content, "ChestCard", 16, y, cardW, cardH, 24, Palette.Card, Palette.CardShadow, 3f);
            Ui.InfoButton(card, cardW - 30, 28, "Как работает сундук",
                $"Звёзды за уровни копятся в сундук: {ProgressService.ChestEvery} ★, и он готов. " +
                "Внутри монеты, а ещё ты сам выбираешь один бустер из трёх и один моток из трёх. " +
                $"Каждый {Chest.Cycle}-й сундук большой: больше монет, бустеры по 2 штуки и точно редкий моток.", 26);

            var big = progress.NextChestBig;
            var ready = progress.CanOpenChest;
            // Свечение готового сундука не выходит за карточку.
            var area = Ui.Place(Ui.Node(card, "ChestArea"), 0, 0, cardW, 168);
            area.gameObject.AddComponent<RectMask2D>();
            var chest = ChestView.Create(area, cardW / 2f, 96, 150f, big);
            chest.Breathe();
            if (ready)
            {
                chest.Shine(0.35f, 0.1f);
            }

            Ui.Label(card, big ? "Большой сундук" : "Звёздный сундук", 20, Palette.Ink, 16, 172, cardW - 32, 28);
            if (ready)
            {
                Ui.Button(card, "Открыть", 24, 208, cardW - 48, 56, Palette.Primary, Palette.PrimaryShadow, Color.white, 20)
                    .OnClick(() => App.ShowPopup(new ChestOpening(App, App.ShowChest)));
            }
            else
            {
                var value = Mathf.Min(progress.ChestProgress, ProgressService.ChestEvery);
                Ui.IconImage(card, Icon.Star, Palette.Coin, 36, 222, 22);
                Ui.ProgressBar(card, 56, 218, cardW - 56 - 76, 9, (float)value / ProgressService.ChestEvery, Palette.Coin);
                Ui.Label(card, $"{value}/{ProgressService.ChestEvery}", 15, Palette.Ink, cardW - 72, 210, 56, 24, true, TextAnchor.MiddleRight);
                Ui.Fit(Ui.Label(card, $"Ещё {ProgressService.ChestEvery - value} ★: проходи уровни на три звезды", 13, Palette.Ink2,
                    16, 238, cardW - 32, 20, false), 10);
            }

            var line = Ui.Box(card, "Divider", 1, Palette.Line);
            Ui.Place(line.rectTransform, 16, 282, cardW - 32, 1.5f);
            BuildCycle(card, cardW, 296f);
            return y + cardH;
        }

        /// <summary>Цикл из четырёх сундуков: открытые с галочкой, текущий в рамке, большой — золотой.</summary>
        private void BuildCycle(RectTransform card, float cardW, float y)
        {
            var opened = App.Progress.ChestsOpened;
            var first = opened - opened % Chest.Cycle;
            const float step = 64f;
            var x0 = cardW / 2f - step * (Chest.Cycle - 1) / 2f;
            var track = Ui.Box(card, "Track", 2, Palette.Track);
            Ui.Place(track.rectTransform, x0, y + 23, step * (Chest.Cycle - 1), 4);
            for (var i = 0; i < Chest.Cycle; i++)
            {
                var number = first + i;
                var big = Chest.IsBig(number);
                var done = number < opened;
                var current = number == opened;
                var cx = x0 + i * step;
                var size = big ? 48f : 40f;
                var back = done ? Palette.PrimarySoft : big ? Palette.CoinSoft : Palette.Surface;
                var circle = Ui.Image(card, "Cycle" + i, Art.Circle, back);
                Ui.PlaceCenter(circle.rectTransform, cx, y + 25, size, size);
                if (current)
                {
                    var ring = Ui.Image(card, "Current", Art.Ring, Palette.Primary);
                    Ui.PlaceCenter(ring.rectTransform, cx, y + 25, size + 10, size + 10);
                    var rt = ring.rectTransform;
                    Tweens.Run(rt, 1.6f, t => rt.localScale = Vector3.one * (1f + 0.06f * Ease.Bump(t)), null, null, 0f, true);
                }

                Ui.IconImage(card, done ? Icon.Check : Icon.Chest, done ? Palette.Primary : big ? Palette.CoinShadow : Palette.Ink2, cx, y + 25,
                    big ? 26 : 22);
                Ui.Label(card, current ? "сейчас" : big ? "большой" : done ? "открыт" : (i + 1).ToString(), 11,
                    current ? Palette.Primary : big ? Palette.CoinShadow : Palette.Ink3, cx - 32, y + 54, 64, 16, current || big);
            }
        }

        private float BuildStock(RectTransform content, float y)
        {
            var progress = App.Progress;
            var cardW = W - 32f;
            var types = (BoosterType[])Enum.GetValues(typeof(BoosterType));
            const float rowH = 72f;
            var cardH = 66f + types.Length * rowH + 6f;
            var card = Ui.Card(content, "Stock", 16, y, cardW, cardH, 24, Palette.Card, Palette.CardShadow, 3f);
            Ui.Label(card, "Запас бустеров", 18, Palette.Ink, 16, 12, cardW - 32, 26, true, TextAnchor.MiddleLeft);
            Ui.Label(card, "Выручат в трудном уровне", 13, Palette.Ink2, 16, 36, cardW - 32, 20, false, TextAnchor.MiddleLeft);
            for (var i = 0; i < types.Length; i++)
            {
                var type = types[i];
                var unlocked = progress.IsUnlocked(type, progress.CurrentLevel);
                var rowW = cardW - 24f;
                var row = Ui.Card(card, type.ToString(), 12, 66 + i * rowH, rowW, rowH - 8, 18, Palette.Surface, Palette.Surface, 0f);
                var circle = Ui.Image(row, "Back", Art.Circle, unlocked ? Palette.PrimarySoft : Palette.Track);
                Ui.PlaceCenter(circle.rectTransform, 34, (rowH - 8) / 2f, 44, 44);
                Ui.IconImage(row, BoosterInfo.Icon(type), unlocked ? Palette.Primary : Palette.Ink3, 34, (rowH - 8) / 2f, 24);
                Ui.Label(row, BoosterInfo.Name(type), 15, unlocked ? Palette.Ink : Palette.Ink2, 66, 8, rowW - 66 - 74, 20, true, TextAnchor.MiddleLeft);
                var description = Ui.Label(row, BoosterInfo.Description(type), 12, Palette.Ink2, 66, 28, rowW - 66 - 74, 30, false, TextAnchor.UpperLeft);
                description.lineSpacing = 0.9f;
                Ui.Fit(description, 9);
                if (unlocked)
                {
                    Ui.Label(row, "×" + progress.Stock(type), 20, progress.Stock(type) > 0 ? Palette.Ink : Palette.Ink3, rowW - 70, 0, 56, rowH - 8, true,
                        TextAnchor.MiddleRight);
                }
                else
                {
                    Ui.IconImage(row, Icon.Lock, Palette.Ink3, rowW - 36, (rowH - 8) / 2f - 9, 18);
                    Ui.Label(row, $"ур. {Economy.UnlockLevel(type)}", 12, Palette.Ink3, rowW - 66, (rowH - 8) / 2f + 2, 60, 18, true);
                }
            }

            return y + cardH;
        }

        private float BuildBasket(RectTransform content, float y)
        {
            var progress = App.Progress;
            var cardW = W - 32f;
            const float cardH = 136f;
            var card = Ui.Card(content, "Basket", 16, y, cardW, cardH, 24, Palette.Card, Palette.CardShadow, 3f, true);
            var circle = Ui.Image(card, "Back", Art.Circle, Palette.DangerSoft);
            Ui.PlaceCenter(circle.rectTransform, 44, 40, 56, 56);
            var logo = Ui.Image(card, "Ball", Art.YarnBall, Palette.Hex("#FF6FA5"));
            Ui.PlaceCenter(logo.rectTransform, 44, 40, 38, 38);
            Ui.Label(card, "Корзинка пряжи", 17, Palette.Ink, 84, 12, cardW - 140, 24, true, TextAnchor.MiddleLeft);
            Ui.Label(card, $"{progress.YarnKinds} из {YarnCatalog.Count} мотков", 13, Palette.Ink2, 84, 34, cardW - 140, 18, false, TextAnchor.MiddleLeft);
            Ui.IconImage(card, Icon.Back, Palette.Ink3, cardW - 28, 40, 18).rectTransform.localScale = new Vector3(-1f, 1f, 1f);

            const float ball = 30f;
            const float gap = 8f;
            var max = Mathf.FloorToInt((cardW - 32f + gap) / (ball + gap));
            var shown = 0;
            for (var i = 0; i < YarnCatalog.Count && shown < max; i++)
            {
                if (!progress.HasYarn(i))
                {
                    continue;
                }

                YarnView.Create(card, YarnCatalog.All[i], 16 + ball / 2f + shown * (ball + gap), 98, ball, true);
                shown++;
            }

            if (shown == 0)
            {
                Ui.Label(card, "Пока пусто: мотки дают за новые картинки и сундуки", 13, Palette.Ink3, 16, 86, cardW - 32, 24, false);
            }

            card.gameObject.AddComponent<Tap>().OnClick(() => App.ShowCollections(CollectionsTab.Yarn));
            return y + cardH;
        }
    }
}
