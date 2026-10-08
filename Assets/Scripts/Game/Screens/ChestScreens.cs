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

            // Сундук падает сверху с ускорением, ударяется, приседает и подпрыгивает — потом «дышит».
            var chest = _chest.Root;
            var rest = chest.anchoredPosition;
            chest.anchoredPosition = rest + new Vector2(0, 420f);
            Tweens.Run(chest, 0.42f, t => chest.anchoredPosition = rest + new Vector2(0, 420f * (1f - t * t)), null, () =>
            {
                Sfx.Play(Sound.Knot, 0.8f);
                Haptics.Play(Haptic.Medium);
                Fx.Sparkle(_stage, chest.TransformPoint(new Vector3(-70f, -60f, 0)), Color.white, 5, 40f);
                Fx.Sparkle(_stage, chest.TransformPoint(new Vector3(70f, -60f, 0)), Color.white, 5, 40f);
                Tweens.Run(chest, 0.4f, t =>
                {
                    // Удар: сплющился, отскочил на десяток точек вверх и встал.
                    var squash = t < 0.3f ? Ease.Bump(t / 0.3f) * 0.14f : 0f;
                    var hop = t >= 0.3f ? Mathf.Sin((t - 0.3f) / 0.7f * Mathf.PI) * 14f : 0f;
                    chest.localScale = new Vector3(1f + squash * 0.8f, 1f - squash, 1f);
                    chest.anchoredPosition = rest + new Vector2(0, hop);
                }, null, () =>
                {
                    chest.localScale = Vector3.one;
                    chest.anchoredPosition = rest;
                    _chest.Breathe();
                    _chest.Shine(0.28f, 0.06f);
                });
            }, 0.05f);
            var hint = _hint;
            Tweens.Run(hint, 1.8f, t => hint.color = Palette.WithAlpha(Color.white, 0.45f + 0.4f * Ease.Bump(t)), null, null, 0.8f, true);
        }

        /// <summary>Назад можно, пока награда не записана: сундук останется тем же.</summary>
        public override bool Closable => !_claimed && _step != Step.Opening;

        protected override bool CloseOnDim => false;

        private float ChestTopY => Mathf.Max(_h * 0.19f, 132f);

        private float TrayY => ChestTopY + 96f;

        private float StepTitleY => TrayY + 52f;

        private float CardsTop => StepTitleY + 40f;

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
                Tweens.Run(chest, 0.38f, t =>
                {
                    chest.anchoredPosition = Vector2.Lerp(from, to, t);
                    chest.localScale = Vector3.one * Mathf.Lerp(1f, 0.66f, t);
                }, Ease.OutCubic, () =>
                {
                    ShowCoins();
                    Tweens.Delay(_stage, 0.55f, ShowBoosters);
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
            const int flyers = 10;
            for (var i = 0; i < flyers; i++)
            {
                // Фонтан: монета вылетает вверх веером, на миг зависает и падает в лоток.
                var coin = Ui.Coin(_stage, 0, 0, 22);
                coin.position = from;
                var spread = (i - (flyers - 1) / 2f) / flyers;
                var peak = from + new Vector3(spread * 220f * unit, UnityEngine.Random.Range(90f, 150f) * unit, 0);
                var spin = UnityEngine.Random.Range(-540f, 540f);
                var index = i;
                Tweens.Run(coin, 0.62f, t =>
                {
                    var u = 1f - t;
                    coin.position = u * u * from + 2f * u * t * peak + t * t * target;
                    coin.localScale = Vector3.one * (t < 0.2f ? Mathf.Lerp(0.3f, 1.15f, t / 0.2f) : Mathf.Lerp(1.15f, 0.8f, (t - 0.2f) / 0.8f));
                    coin.localRotation = Quaternion.Euler(0, 0, spin * t);
                }, Ease.InOutCubic, () =>
                {
                    UnityEngine.Object.Destroy(coin.gameObject);
                    Sfx.Play(Sound.Coin, 0.35f);
                    if (label != null)
                    {
                        label.text = "+" + Mathf.RoundToInt(total * (index + 1f) / flyers);
                    }

                    Tweens.Punch(chip, 0.1f, 0.14f);
                }, i * 0.045f);
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

            // Карточка выпрыгивает из сундука: дугой вверх с вращением, падает на место и пружинит.
            var rest = visual.anchoredPosition;
            var start = (Vector2)root.InverseTransformPoint(_chest.Mouth);
            var tilt = (index - (count - 1) / 2f) * 14f + (count == 1 ? 10f : 0f);
            visual.anchoredPosition = start;
            visual.localScale = Vector3.one * 0.2f;
            group.alpha = 0f;
            Tweens.Run(visual, 0.5f, t =>
            {
                var arc = 4f * t * (1f - t) * 110f;
                visual.anchoredPosition = Vector2.LerpUnclamped(start, rest, t) + new Vector2(0, arc);
                visual.localScale = Vector3.one * Mathf.Lerp(0.2f, 1f, Ease.OutCubic(t));
                visual.localRotation = Quaternion.Euler(0, 0, tilt * (1f - t) * 2f);
                group.alpha = Mathf.Clamp01(t * 5f);
            }, null, () =>
            {
                visual.localRotation = Quaternion.identity;
                visual.anchoredPosition = rest;
                Sfx.Play(Sound.Pick, 0.6f);
                Haptics.Play(Haptic.Tap);
                Tweens.Run(visual, 0.24f, t =>
                {
                    var squash = Ease.Bump(t) * 0.1f * (1f - t);
                    visual.localScale = new Vector3(1f + squash, 1f - squash, 1f);
                }, null, () => visual.localScale = Vector3.one);
            }, delay);

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
                // Выбор со смыслом: что даст моток — новый бонус, следующий уровень или монеты за повтор.
                var level = App.Progress.YarnLevel(yarn.Id);
                var perk = YarnCatalog.PerkOf(yarn.Id);
                _description.text = !App.Progress.PerksOpen
                    ? (level == 0 ? $"{yarn.Description}. Набор «{set}»" : $"Уже есть: повтор поднимет уровень мотка")
                    : level == 0
                    ? $"Набор «{set}». Бонус: {YarnCatalog.PerkText(perk, YarnCatalog.Power(yarn.Id, 1))}"
                    : level < YarnCatalog.MaxLevel
                        ? $"Уровень {level + 1}: {YarnCatalog.PerkText(perk, YarnCatalog.Power(yarn.Id, level + 1))}"
                        : $"Уже максимальный уровень: повтор даст {YarnCatalog.DuplicateCoins(yarn.Rarity)} монет";
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
            Tweens.Delay(_stage, 0.12f * (options.Length - 1) + 0.75f, () => _selectable = true);
            for (var i = 0; i < options.Length; i++)
            {
                var type = options[i];
                var (_, visual, _, _) = AddCard(i, options.Length, i * 0.12f);
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
            Tweens.Delay(_stage, 0.8f + 0.28f * (options.Length - 1) + 0.45f, () => _selectable = true);
            for (var i = 0; i < options.Length; i++)
            {
                var yarn = YarnCatalog.All[options[i]];
                var card = AddCard(i, options.Length, i * 0.12f);
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
                var yarnLevel = App.Progress.YarnLevel(yarn.Id);
                var levelUp = !isNew && yarnLevel < YarnCatalog.MaxLevel;
                var chipText = isNew ? "новый!" : levelUp ? $"ур. {yarnLevel + 1}!" : $"+{YarnCatalog.DuplicateCoins(yarn.Rarity)} монет";
                var chip = Ui.Box(front, "State", 11, isNew ? Palette.DangerSoft : levelUp ? Palette.PrimarySoft : Palette.CoinSoft).rectTransform;
                Ui.PlaceCenter(chip, w / 2f, 130, w - 16, 22);
                Ui.Fit(Ui.Label(chip, chipText, 12, isNew ? Palette.Danger : levelUp ? Palette.Primary : Palette.CoinInk, 0, 0, w - 16, 22), 9);

                // Значок бонуса в углу: сразу видно, чем поможет моток (когда бонусы уже открыты).
                if (App.Progress.PerksOpen)
                {
                    var perkBack = Ui.Image(front, "Perk", Art.Circle, Palette.Surface);
                    Ui.PlaceCenter(perkBack.rectTransform, 18, 18, 26, 26);
                    YarnCardPopup.PerkIcon(front, YarnCatalog.PerkOf(yarn.Id), 18, 18, 15);
                }
                front.gameObject.SetActive(false);

                // Переворот по очереди, когда все карточки легли.
                var rare = yarn.Rarity != Rarity.Common;
                var flipDelay = 0.8f + i * 0.28f;
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
            var levelUp = granted != null && granted.Value.YarnLevelUp;
            var duplicate = granted != null && !granted.Value.NewYarn && !levelUp;
            var level = App.Progress.YarnLevel(yarn.Id);
            var chip = AddTrayChip(c => YarnView.Create(c, yarn, 22, 20, 28, true),
                duplicate ? "+" + granted!.Value.DuplicateCoins : levelUp ? $"ур. {level}" : yarn.Name);
            ClearCards(yarnChoice, Ui.WorldCenter(chip));

            _stepTitle.text = "Всё в корзинке";
            _description.text = duplicate
                ? $"«{yarn.Name}» уже на максимуме: повтор стал монетами"
                : levelUp
                    ? $"«{yarn.Name}» теперь уровня {level}: бонус сильнее"
                    : $"Новый моток «{yarn.Name}» в коллекции";
            _description.rectTransform.anchoredPosition = new Vector2(24, -(CardsTop + 10f));
            Fx.Sparkle(_stage, Ui.WorldCenter(chip), Palette.Coin, 10, 60f);
            Sfx.Play(Sound.Win, 0.5f);
            Haptics.Play(Haptic.Success);
            Tweens.Delay(_stage, 0.5f, () => ShowButton("Готово"));
            _confirm = () =>
            {
                Close();
                _finished();
            };
        }
    }

    /// <summary>
    /// «Сундук»: один главный объект — сундук. Под ним звёзды до открытия (или «Открыть»),
    /// что внутри (монеты, бустер на выбор, моток на выбор — с прицелом на большой сундук)
    /// и путь из четырёх сундуков. Ниже — запасы бустеров плитками; описание — по тапу на плитку.
    /// Корзинка пряжи живёт во вкладке «Пряжа», здесь её нет, чтобы не дублировать.
    /// </summary>
    public sealed class ChestScreen : ScreenView
    {
        private RectTransform? _bar;

        public ChestScreen(App app)
            : base(app, "ChestScreen")
        {
            Ui.RoundButton(Root, Icon.Back, 16, 8).OnClick(app.ShowHome);
            Ui.Fit(Ui.Label(Root, "Сундук", 22, Palette.Ink, 66, 8, W - 66 - 136, 44, true, TextAnchor.MiddleLeft), 15);
            Ui.CoinPill(Root, app.Progress.Coins, W - 16, 8);
            var h = H - 64f;
            var content = Ui.Scroll(Root, 0, 64, W, h);
            var y = 6f;
            y = BuildChest(content, y) + 28f;
            y = BuildStock(content, y) + 16f;
            content.sizeDelta = new Vector2(W, Mathf.Max(y + 24f, h));
        }

        private float BuildChest(RectTransform content, float y)
        {
            var progress = App.Progress;
            var cardW = W - 32f;
            const float cardH = 452f;
            var card = Ui.Card(content, "ChestCard", 16, y, cardW, cardH, 24, Palette.Card, Palette.CardShadow, 3f);

            var big = progress.NextChestBig;
            var ready = progress.CanOpenChest;

            // Сундук — главный объект: крупно, дышит, готовый светится. Тап по готовому открывает его,
            // по неготовому — сундук подпрыгивает и мигает полоска звёзд: видно, чего не хватает.
            var area = Ui.Place(Ui.Node(card, "ChestArea"), 0, 0, cardW, 184);
            area.gameObject.AddComponent<RectMask2D>();
            Ui.Stretch(Ui.Image(area, "Hit", null, Color.clear, true).rectTransform);
            var chest = ChestView.Create(area, cardW / 2f, 104, 168f, big);
            chest.Breathe();
            if (ready)
            {
                chest.Shine(0.35f, 0.1f);
            }

            var tap = area.gameObject.AddComponent<Tap>();
            tap.PressFeedback = false;
            tap.OnClick(() =>
            {
                if (App.Progress.CanOpenChest)
                {
                    Open();
                    return;
                }

                Tweens.Shake(chest.Root, 6f, 0.3f);
                Haptics.Play(Haptic.Soft);
                if (_bar != null)
                {
                    var bar = _bar;
                    Tweens.Run(bar, 0.5f, t => bar.localScale = new Vector3(1f, 1f + 0.5f * Ease.Bump(t), 1f));
                }
            });

            Ui.Label(card, big ? "Большой сундук" : "Звёздный сундук", 20, Palette.Ink, 16, 186, cardW - 32, 28);
            if (ready)
            {
                Ui.Button(card, "Открыть", 24, 222, cardW - 48, 56, Palette.Primary, Palette.PrimaryShadow, Color.white, 20).OnClick(Open);
            }
            else
            {
                var value = Mathf.Min(progress.ChestProgress, ProgressService.ChestEvery);
                Ui.IconImage(card, Icon.Star, Palette.Coin, 36, 246, 24);
                var track = Ui.Box(card, "Track", 6, Palette.Track).rectTransform;
                Ui.Place(track, 56, 240, cardW - 56 - 80, 12);
                _bar = track;
                var fill = Ui.Box(track, "Fill", 6, Palette.Coin).rectTransform;
                Ui.Place(fill, 0, 0, Mathf.Max(12f, (cardW - 136) * value / ProgressService.ChestEvery), 12);
                Ui.Label(card, $"{value}/{ProgressService.ChestEvery}", 17, Palette.Ink, cardW - 76, 234, 60, 24, true, TextAnchor.MiddleRight);
            }

            BuildInside(card, cardW, 296f, big);
            BuildCycle(card, cardW, 376f);
            return y + cardH;
        }

        private void Open() => App.ShowPopup(new ChestOpening(App, App.ShowChest));

        /// <summary>
        /// Что внутри — три плашки: монеты, бустер на выбор, моток на выбор. Ожидание награды
        /// работает сильнее любой подсказки; у большого сундука всё щедрее и подписано золотом.
        /// </summary>
        private void BuildInside(RectTransform card, float cardW, float y, bool big)
        {
            const float gap = 8f;
            const float h = 66f;
            var w = (cardW - 32f - gap * 2f) / 3f;
            var items = new (string text, System.Action<RectTransform, float> icon)[]
            {
                ($"{Chest.CoinRange(big).Min}–{Chest.CoinRange(big).Max}", (c, cx) => Ui.Coin(c, cx, 22, 26)),
                (big ? "бустер ×2" : "бустер", (c, cx) => Ui.IconImage(c, Icon.PlusSlot, Palette.Primary, cx, 22, 24)),
                (big ? "редкий моток" : "моток", (c, cx) =>
                {
                    var ball = Ui.Image(c, "Ball", Art.YarnBall, big ? Palette.Hex("#A98BFF") : Palette.Hex("#FF6FA5"));
                    Ui.PlaceCenter(ball.rectTransform, cx, 22, 28, 28);
                }),
            };
            for (var i = 0; i < items.Length; i++)
            {
                var x = 16f + i * (w + gap);
                var cell = Ui.Box(card, "Inside", 16, big ? Palette.CoinSoft : Palette.Surface).rectTransform;
                Ui.Place(cell, x, y, w, h);
                items[i].icon(cell, w / 2f);
                Ui.Fit(Ui.Label(cell, items[i].text, 13, big ? Palette.CoinInk : Palette.Ink2, 4, 40, w - 8, 20, true), 10);
            }
        }

        /// <summary>Путь из четырёх сундуков: открытые с галочкой, текущий в кольце, большой — золотой.</summary>
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

                // Подпись только у большого: галочка и кольцо понятны без слов.
                if (big && !done)
                {
                    Ui.Label(card, "большой", 11, Palette.CoinInk, cx - 32, y + 54, 64, 16, true);
                }
            }
        }

        /// <summary>
        /// Запасы — плитками в ряд: значок, сколько есть и название. Закрытые — замок и уровень.
        /// Что делает бустер — по тапу на плитку, а не строкой мелкого текста под каждым.
        /// </summary>
        private float BuildStock(RectTransform content, float y)
        {
            var progress = App.Progress;
            var types = (BoosterType[])Enum.GetValues(typeof(BoosterType));
            Ui.Label(content, "Запасы", 18, Palette.Ink, 20, y, W - 40, 26, true, TextAnchor.MiddleLeft);
            y += 34f;
            const float gap = 10f;
            const float tileH = 104f;
            var tileW = (W - 32f - gap * (types.Length - 1)) / types.Length;
            for (var i = 0; i < types.Length; i++)
            {
                var type = types[i];
                var unlocked = progress.IsUnlocked(type, progress.CurrentLevel);
                var stock = progress.Stock(type);
                var tile = Ui.Card(content, type.ToString(), 16 + i * (tileW + gap), y, tileW, tileH, 20, Palette.Card, Palette.CardShadow, 3f, true);
                var circle = Ui.Image(tile, "Back", Art.Circle, unlocked ? Palette.PrimarySoft : Palette.Track);
                Ui.PlaceCenter(circle.rectTransform, tileW / 2f, 36, 48, 48);
                Ui.IconImage(tile, unlocked ? BoosterInfo.Icon(type) : Icon.Lock, unlocked ? Palette.Primary : Palette.Ink3, tileW / 2f, 36, 24);
                if (unlocked)
                {
                    var chip = Ui.Box(tile, "Count", 11, stock > 0 ? Palette.Ink : Palette.Inactive).rectTransform;
                    Ui.PlaceCenter(chip, tileW / 2f + 22f, 16, 34, 22);
                    Ui.Label(chip, "×" + stock, 12, Color.white, 0, 0, 34, 22);
                }

                Ui.Fit(Ui.Label(tile, unlocked ? BoosterInfo.Name(type) : $"ур. {Economy.UnlockLevel(type)}", 13,
                    unlocked ? Palette.Ink : Palette.Ink3, 4, 68, tileW - 8, 28, unlocked), 9).lineSpacing = 0.85f;

                var target = type;
                tile.gameObject.AddComponent<Tap>().OnClick(() => App.ShowPopup(new InfoPopup(App, BoosterInfo.Name(target),
                    App.Progress.IsUnlocked(target, App.Progress.CurrentLevel)
                        ? BoosterInfo.Description(target)
                        : $"{BoosterInfo.Description(target)}. Откроется на уровне {Economy.UnlockLevel(target)}")));
            }

            return y + tileH;
        }
    }
}
