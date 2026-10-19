#nullable enable

using System.Collections.Generic;
using System.Linq;
using KnittedPicture.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KnittedPicture.Game
{
    /// <summary>Построение экрана: шапка, таймер, цели, картинка, станок, поле, бустеры, баннеры входа.</summary>
    public sealed partial class GameScreen
    {
        // ---------- Построение ----------

        private void BuildHud()
        {
            Ui.RoundButton(Root, Icon.Pause, Pad, 8).OnClick(() =>
            {
                if (!_ended)
                {
                    App.ShowPopup(new PausePopup(App, Restart, LeaveHome, StreakWarning()));
                }
            });

            // Только номер: у сложного плашка красная. Подсказка уровня открывается тапом по плашке.
            var hasTip = !string.IsNullOrEmpty(_level.tip);
            var text = _run.Pattern ? L.T("Узор дня") : _level.id.ToString();
            var width = _run.Pattern ? 120f : 64f;
            var pill = Ui.Card(Root, "Level", W / 2f - width / 2f, 8, width, 44, 22,
                _level.hard ? Palette.Danger : Palette.Card, Palette.CardShadow, _level.hard ? 0f : 2f, hasTip);
            Ui.Fit(Ui.Label(pill, text, 20, _level.hard ? Color.white : Palette.Ink, 6, 0, width - 12, 44), 12);
            if (hasTip)
            {
                // Подсказка уровня исчезает сама; вернуть её можно тапом по плашке уровня.
                pill.gameObject.AddComponent<Tap>().OnClick(() =>
                {
                    if (!_ended && !_failShown)
                    {
                        App.ShowPopup(new InfoPopup(App, L.T("Подсказка уровня"), _level.Tip));
                    }
                });
            }
            BuildYarnBadge();

            // Монеты на уровне не показываем: цена бустера видна в окне покупки, заработанное — на победе.
            _coins = Ui.Label(Root, "", 1, Color.clear, 0, 0, 1, 1);
            _coins.enabled = false;
        }

        /// <summary>
        /// Значок мотка на спицах справа в шапке: круглый моток и сила бонуса «+N». Виден, только когда бонусы открыты
        /// и моток выбран. Тап напоминает, что именно он даёт. Слева его ограничивает плашка уровня
        /// (она ровно по центру, у «Узора дня» шире), так что на 320 и 390 точек ничего не перекрывается.
        /// </summary>
        private void BuildYarnBadge()
        {
            var progress = App.Progress;
            var id = progress.ActiveYarn;
            if (!progress.PerksOpen || id < 0)
            {
                return;
            }

            var yarn = YarnCatalog.All[id];
            var perk = YarnCatalog.PerkOf(id);
            var power = progress.ActivePower;
            var bonus = perk == YarnPerk.Coins ? YarnCatalog.CoinsBonus(power) : YarnCatalog.GoldBonus(power);

            // «Тёплая копилка» платит только за новую картинку (не за повтор и не в «Узоре дня»): тогда значок
            // тусклый и без «+N», чтобы не обещать монеты, которых не будет.
            var idle = perk == YarnPerk.Coins && (_run.Pattern || progress.IsCompleted(_level.id));
            var width = idle ? 44f : 72f;
            var pill = Ui.Card(Root, "YarnBadge", W - Pad - width, 8, width, 44, 22, Palette.Card, Palette.CardShadow, 2f, true);
            YarnView.Create(pill, yarn, idle ? 22f : 24f, 22f, 26f, true);
            if (idle)
            {
                pill.gameObject.AddComponent<CanvasGroup>().alpha = 0.5f;
            }
            else
            {
                Ui.Fit(Ui.Label(pill, "+" + bonus, 16, Palette.Ink, 40f, 0, width - 46f, 44, true, TextAnchor.MiddleLeft), 11);
            }

            pill.gameObject.AddComponent<Tap>().OnClick(() =>
            {
                var text = YarnCatalog.PerkText(perk, power);
                App.Toast(idle ? L.F("«{0}» на спицах: {1}. Только за новую картинку", yarn.Name, text) : L.F("«{0}» на спицах: {1}", yarn.Name, text));
            });
            _yarnBadge = pill;
            _yarnPerk = perk;
            _yarnBonus = perk == YarnPerk.Gold ? bonus : 0;
        }

        private void BuildTimer()
        {
            const float y = 60f;
            _timerX = 44f;
            _timerW = W - 44f - 100f;
            var yarn = Ui.Image(Root, "Target", Art2.YarnBall(_colors[0]), Color.white);
            Ui.PlaceCenter(yarn.rectTransform, 26, y + 13, 24, 24);
            var track = Ui.Box(Root, "TimerTrack", 5, Palette.Track);
            Ui.Place(track.rectTransform, _timerX, y + 8, _timerW, 10);
            _timerFillImage = Ui.Box(track.rectTransform, "Fill", 5, Palette.Primary);
            _timerFill = _timerFillImage.rectTransform;
            Ui.Place(_timerFill, 0, 0, _timerW, 10);
            _timerIcon = Ui.IconImage(Root, Icon.Moth, Palette.WithAlpha(Palette.Ink, 0.5f), _timerX + _timerW + 16f, y + 13, 18);
            _timerText = Ui.Label(Root, "", 16, Palette.Ink, W - Pad - 56, y, 56, 26, true, TextAnchor.MiddleRight);

            // Опора справа посередине: на сердцебиении цифры растут от своего края, а не уезжают.
            _timerText.rectTransform.pivot = new Vector2(1f, 0.5f);
            _timerText.rectTransform.anchoredPosition = new Vector2(W - Pad, -(y + 13f));
            RefreshTimer();
        }

        /// <summary>Плашки цели над картинкой больше нет: золотые кольца видны прямо на картинке, итог — на победе.</summary>
        private bool HasGoals => false;

        private float GoalsY => _game.TimeLimit > 0 ? 94f : 60f;

        /// <summary>Цель уровня над картинкой: золотые петли (одна плашка по центру).</summary>
        private void BuildGoals()
        {
            if (!HasGoals)
            {
                return;
            }

            const float chipW = 150f;
            var chip = Ui.Card(Root, "GoldGoal", W / 2f - chipW / 2f, GoalsY, chipW, 28, 14, Palette.Card, Palette.CardShadow, 2f, true);

            // Тап по цели — короткая справка; игра на это время стоит.
            var info = L.F("Вяжи петли в золотом кольце, пока горит комбо: +{0} {1} за каждую", _goldPerLoop, YarnCatalog.CoinsWord(_goldPerLoop));
            chip.gameObject.AddComponent<Tap>().OnClick(() => App.ShowPopup(new InfoPopup(App, L.T("Золотые петли"), info)));
            Ui.IconImage(chip, Icon.Star, Palette.Coin, 18, 14, 18);
            _goldGoal = Ui.Fit(Ui.Label(chip, "", 14, Palette.Ink, 32, 0, chipW - 38, 28, true, TextAnchor.MiddleLeft), 10);
            RefreshGoals();
        }

        private void RefreshGoals()
        {
            if (_goldGoal != null)
            {
                _goldGoal.text = L.F("Золото {0}/{1}", _goldCaught, _level.gold.Length);
            }
        }

        private void ComputeLayout()
        {
            _top = (_game.TimeLimit > 0 ? 96f : 62f) + (HasGoals ? 34f : 0f);
            // На первых уровнях бустеров нет: их место отдаём полю, а клубки делаем крупнее.
            var boosterRow = new[] { BoosterType.Scissors, BoosterType.Undo, BoosterType.Slot }.Any(t => App.Progress.IsBoosterOpen(t, BoosterLevel));
            var boostersY = boosterRow ? H - 10f - 60f : H - 4f;
            _colW = (W - 2 * Pad - 16f) / _game.ColumnCount;
            _spoolSize = Mathf.Min((boosterRow ? 52f : 58f) + (App.Tablet ? 8f : 0f), _colW - 12f);

            for (var attempt = 0; attempt < 4; attempt++)
            {
                _fieldH = VisibleRows * (_spoolSize + 10f) + 10f + DepthRow;
                _fieldY = boostersY - 16f - _fieldH;
                _loomY = _fieldY - 12f - LoomTop - LoomH;
                if (_loomY - 10f - _top >= 170f || _spoolSize <= 40f)
                {
                    break;
                }

                _spoolSize -= 4f;
            }
        }

        private void BuildPicture()
        {
            var availH = _loomY - 10f - _top;
            var step = Mathf.Min((W - 2 * Pad - 24f) / _game.Width, (availH - 24f - 26f) / _game.Height, App.Tablet ? 36f : 30f);
            var gap = Mathf.Max(1f, Mathf.Round(step * 0.1f));
            var cell = step - gap;
            var gridW = _game.Width * step - gap;
            var gridH = _game.Height * step - gap;
            var cardW = gridW + 24f;
            var cardH = gridH + 24f + 26f;
            var y = _top + Mathf.Max(0f, (availH - cardH) / 2f);

            _pictureCard = Ui.Layer(Ui.Card(Root, "PictureCard", (W - cardW) / 2f, y, cardW, cardH, 22, Palette.Card, Palette.CardShadow), true);
            _picture = new PictureView(_pictureCard, _game, _colors, cell, gap);
            Ui.Place(_picture.Root, 12, 12, gridW, gridH);

            _progressWidth = cardW - 24f;
            var bar = Ui.Box(_pictureCard, "ProgressTrack", 4, Palette.Track);
            Ui.Place(bar.rectTransform, 12, 12 + gridH + 13, _progressWidth, 8);
            _progressFill = Ui.Box(bar.rectTransform, "Fill", 4, Palette.Primary).rectTransform;
            Ui.Place(_progressFill, 0, 0, 8, 8);
            _progressText = Ui.Label(_pictureCard, "0%", 13, Palette.Ink2, cardW - 12 - 44, 12 + gridH + 5, 44, 24, true, TextAnchor.MiddleRight);
        }

        private void BuildLoom()
        {
            // Подписи станка нет: слоты видны. Надпись появляется, только когда места кончаются.
            _loomTitle = Ui.Label(Root, "", 13, Palette.Ink2, Pad + 6, _loomY + 2f, 120, 24, true, TextAnchor.MiddleLeft);
            _loomLabel = Ui.Label(Root, "", 13, Palette.Ink2, W - Pad - 186, _loomY + 2f, 180, 24, true, TextAnchor.MiddleRight);
            _loomBgImage = Ui.Box(Root, "Loom", 20, Palette.LoomBg);
            _loomBg = Ui.Layer(_loomBgImage.rectTransform, true);
            Ui.Place(_loomBg, Pad, _loomY + LoomTop, W - 2 * Pad, LoomH);
            // Рамки тревоги и «Разгона» лежат ровно по краю станка, внутрь, а не выступают на соседей.
            _loomAlert = Ui.Image(Root, "LoomAlert", Art.RoundRectOutline(20, 3), Color.clear);
            _loomAlert.type = Image.Type.Sliced;
            _loomAlert.pixelsPerUnitMultiplier = Art.Density;
            Ui.Place(_loomAlert.rectTransform, Pad, _loomY + LoomTop, W - 2 * Pad, LoomH);
            _rushGlow = Ui.Image(Root, "RushGlow", Art.RoundRectOutline(20, 4), Color.clear);
            _rushGlow.type = Image.Type.Sliced;
            _rushGlow.pixelsPerUnitMultiplier = Art.Density;
            Ui.Place(_rushGlow.rectTransform, Pad, _loomY + LoomTop, W - 2 * Pad, LoomH);
            BuildComboChip();
            RebuildSlots();
        }

        /// <summary>
        /// Плашка комбо в строке над станком, на месте подписи «Станок»: «Комбо ×3» и полоска —
        /// сколько ещё станок может простоять. Компактная и целиком в своей строке.
        /// </summary>
        private void BuildComboChip()
        {
            _comboChip = Ui.Place(Ui.Node(Root, "Combo"), Pad + 2f, _loomY + 2f, ComboW, 24);
            _comboGroup = _comboChip.gameObject.AddComponent<CanvasGroup>();
            _comboGroup.blocksRaycasts = false;
            _comboGroup.alpha = 0f;
            _comboBack = Ui.Box(_comboChip, "Back", 13, Palette.Link);
            Ui.Stretch(_comboBack.rectTransform);
            _comboBolt = Ui.IconImage(_comboChip, Icon.Bolt, Color.white, 14, 11, 14);
            _comboText = Ui.Fit(Ui.Label(_comboChip, "", 13, Color.white, 24, 0, ComboW - 30, 19, true, TextAnchor.MiddleLeft), 10);
            var track = Ui.Box(_comboChip, "HeatTrack", 1.5f, Palette.WithAlpha(Color.white, 0.3f));
            Ui.Place(track.rectTransform, 10, 18, ComboW - 20, 3);
            _comboHeat = Ui.Box(track.rectTransform, "Heat", 1.5f, Color.white).rectTransform;
            Ui.Place(_comboHeat, 0, 0, ComboW - 20, 3);
        }

        private void RebuildSlots()
        {
            Ui.Clear(_loomBg);

            // Чёткий контур лотка (внутри станка: трясётся и пружинит вместе с ним).
            Ui.Outline(_loomBg, 20, Palette.Line, 1.5f);
            var n = _game.Slots.Count;
            const float gap = SlotGap;
            var slotW = (W - 2 * Pad - 16f - gap * (n - 1)) / n;
            _slotSpool = Mathf.Min(44f, slotW - 10f);
            _slotFrames = new RectTransform[n];
            _slotOutlines = new Image[n];
            _slotViews = new SlotSpool?[n];
            _tailViews = new RectTransform?[n];
            for (var i = 0; i < n; i++)
            {
                // Слот — выточенная лунка (вдавленная плашка с тенью сверху и кромкой), а не плоский квадрат.
                var frame = Ui.Box(_loomBg, "Slot", 14, Palette.Card);
                frame.gameObject.name = "Slot" + i;
                Ui.Place(frame.rectTransform, 8 + i * (slotW + gap), 8, slotW, 58);
                var outline = Ui.Image(frame.rectTransform, "Outline", Art.RoundRectOutline(14, 2), Palette.SlotEmpty);
                outline.type = Image.Type.Sliced;
                outline.pixelsPerUnitMultiplier = Art.Density;
                Ui.Stretch(outline.rectTransform);
                _slotFrames[i] = frame.rectTransform;
                _slotOutlines[i] = outline;
                var slot = i;
                var tap = frame.gameObject.AddComponent<Tap>();
                frame.raycastTarget = true;
                tap.ClickSound = false;
                tap.PressFeedback = false;
                tap.OnClick(() => OnSlotTap(slot));
            }
        }

        private void BuildField()
        {
            var card = Ui.Layer(Ui.Card(Root, "Field", Pad, _fieldY, W - 2 * Pad, _fieldH, 24, Palette.Card, Palette.CardShadow), true);
            _fieldContent = Ui.Stretch(Ui.Node(card, "Content"));
        }

        private void BuildBoosters()
        {
            // Только открытые бустеры: закрытая серая кнопка «ур. 12» ничего не делает и только шумит.
            var types = new[] { BoosterType.Scissors, BoosterType.Undo, BoosterType.Slot }
                .Where(t => App.Progress.IsBoosterOpen(t, BoosterLevel)).ToArray();
            if (types.Length == 0)
            {
                return;
            }

            const float gap = 10f;
            var w = Mathf.Min((W - 2 * Pad - gap * 2) / 3f, (W - 2 * Pad - gap * (types.Length - 1)) / types.Length);
            var x0 = (W - (w * types.Length + gap * (types.Length - 1))) / 2f;
            var y = H - 10f - 60f;
            for (var i = 0; i < types.Length; i++)
            {
                var type = types[i];
                var button = new BoosterButton(Root, type, x0 + i * (w + gap), y, w);
                button.Tap.OnClick(() => OnBooster(type));
                _boosters[type] = button;
            }
        }

        private void ShowIntro()
        {
            if (!string.IsNullOrEmpty(_level.tip))
            {
                Tweens.Delay(Root, 0.6f, ShowTip);
            }

            // Один баннер на старте — самое важное: сложный уровень, иначе загадка, иначе подарок серии.
            var bannerY = _top + 40f;
            if (_level.hard)
            {
                Banner(L.T("Сложный уровень"), Palette.Danger, bannerY);
            }
            else if (_level.mystery)
            {
                Banner(L.T("Загадка: что получится?"), Palette.Link, bannerY);
            }
            else if (_run.Warm.Any)
            {
                Banner(L.F("Серия ×{0}: {1}", _run.WarmStreak, WarmGifts(_run.Warm)), Palette.CoinShadow, bannerY, 0.3f);
            }
        }

        /// <summary>Уровень, по которому открыты бустеры: у «узора дня» — текущий уровень кампании.</summary>
        private int BoosterLevel => _run.Pattern ? App.Progress.CurrentLevel : _level.id;

        /// <summary>Серия есть и уровень новый: перезапуск или уход её обнулят — честно предупредить.</summary>
        private string? StreakWarning() =>
            !_run.Pattern && App.Progress.Streak > 0 && !App.Progress.IsCompleted(_level.id)
                ? L.F("«Заново» или «Домой» обнулят тёплую серию ×{0}", App.Progress.Streak)
                : null;

        /// <summary>Подарок серии словами: «+1 слот, ножницы и отмена».</summary>
        public static string WarmGifts(WarmStart warm)
        {
            var parts = new List<string>();
            if (warm.ExtraSlot)
            {
                parts.Add(L.T("+1 место"));
            }

            if (warm.FreeScissors)
            {
                parts.Add(L.T("ножницы"));
            }

            if (warm.FreeUndo)
            {
                parts.Add(L.T("отмена"));
            }

            return parts.Count <= 1 ? string.Concat(parts) : string.Join(", ", parts.Take(parts.Count - 1)) + L.T(" и ") + parts[parts.Count - 1];
        }

        private void Banner(string text, Color color, float y, float delay = 0f)
        {
            var width = Mathf.Min(W - 2 * Pad, 320f);
            var banner = Ui.Card(Root, "Banner", W / 2f - width / 2f, y, width, 56, 28, color, color, 0f);
            Ui.Fit(Ui.Label(banner, text, 20, Color.white, 14, 0, width - 28, 56), 13);
            var group = banner.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            banner.localScale = Vector3.zero;
            Tweens.Scale(banner, 0.6f, 1f, 0.3f, Ease.OutBack, null, delay);
            Tweens.Fade(group, 1f, 0f, 0.4f, () => Object.Destroy(banner.gameObject), 1.6f + delay);
        }
    }
}
