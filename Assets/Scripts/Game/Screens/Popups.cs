#nullable enable

using System;
using KnittedPicture.Core;
using UnityEngine;

namespace KnittedPicture.Game
{
    public static class BoosterInfo
    {
        public static string Name(BoosterType type)
        {
            switch (type)
            {
                case BoosterType.Slot: return "+1 слот";
                case BoosterType.Scissors: return "Ножницы";
                case BoosterType.Undo: return "Отменить";
                case BoosterType.Time: return "+20 секунд";
                default: return string.Empty;
            }
        }

        public static string Description(BoosterType type)
        {
            switch (type)
            {
                case BoosterType.Slot: return "Ещё одно место на станке, когда клубкам тесно";
                case BoosterType.Scissors: return "Достают любой клубок с поля, даже из глубины, во льду или под замком, и срезают клубок со станка";
                case BoosterType.Undo: return "Возвращает последний ход";
                case BoosterType.Time: return "Отгоняет моль: +20 секунд к таймеру";
                default: return string.Empty;
            }
        }

        /// <summary>Одна короткая строка — для списков, где полное описание перегружает.</summary>
        public static string Short(BoosterType type)
        {
            switch (type)
            {
                case BoosterType.Slot: return "Ещё одно место на станке";
                case BoosterType.Scissors: return "Достать любой клубок или срезать со станка";
                case BoosterType.Undo: return "Вернуть последний ход";
                case BoosterType.Time: return "Отогнать моль, когда она близко";
                default: return string.Empty;
            }
        }

        public static Icon Icon(BoosterType type)
        {
            switch (type)
            {
                case BoosterType.Slot: return KnittedPicture.Game.Icon.PlusSlot;
                case BoosterType.Scissors: return KnittedPicture.Game.Icon.Scissors;
                case BoosterType.Undo: return KnittedPicture.Game.Icon.Undo;
                case BoosterType.Time: return KnittedPicture.Game.Icon.Clock;
                default: return KnittedPicture.Game.Icon.Check;
            }
        }
    }

    /// <summary>«Новый бустер» — один раз, когда бустер открывается, с подарком.</summary>
    public sealed class BoosterIntroPopup : Popup
    {
        public BoosterIntroPopup(App app, BoosterType type)
            : base(app)
        {
            AddIcon(BoosterInfo.Icon(type), Palette.Primary, Palette.PrimarySoft, 76);
            AddTitle("Новый бустер", 24);
            AddText(BoosterInfo.Name(type), 20, 1);
            AddText(BoosterInfo.Description(type), 16, 2.2f);
            AddText($"+{Economy.FreeOnUnlock} в подарок", 16, 1);
            AddPrimary("Понятно").OnClick(Close);
            Finish(8);
        }
    }

    /// <summary>Заглушка видео-рекламы: полторы секунды и награда.</summary>
    public sealed class RewardedStubPopup : Popup
    {
        public RewardedStubPopup(App app, Action done)
            : base(app)
        {
            AddIcon(KnittedPicture.Game.Icon.Video, Palette.Ink, Palette.Track, 64);
            AddTitle("Видео-реклама", 22);
            AddText("Здесь будет ролик из SDK издателя. Сейчас это заглушка.", 15, 2.2f);
            var fill = Ui.ProgressBar(Card, 24, Cursor, Inner, 10, 0.02f, Palette.Primary);
            Space(30);
            Finish(4);
            Tweens.Run(Card, 1.5f, t => fill.sizeDelta = new Vector2(Mathf.Max(10f, Inner * t), 10f), null, () =>
            {
                Close();
                done();
            });
        }

        public override bool Closable => false;

        protected override bool CloseOnDim => false;
    }

    public sealed class PausePopup : Popup
    {
        public PausePopup(App app, Action restart, Action home, string? warning = null)
            : base(app)
        {
            AddTitle("Пауза");
            if (warning != null)
            {
                var warn = AddText(warning, 13, 1);
                warn.color = Palette.CoinInk;
            }

            AddPrimary("Продолжить").OnClick(Close);
            var sound = AddSecondary(SoundLabel(app.Progress.SoundOn), app.Progress.SoundOn ? KnittedPicture.Game.Icon.SoundOn : KnittedPicture.Game.Icon.SoundOff);
            sound.OnClick(() =>
            {
                app.SetSound(!app.Progress.SoundOn);
                Close();
                app.ShowPopup(new PausePopup(app, restart, home, warning));
            });
            var (again, toHome) = AddPair("Заново", KnittedPicture.Game.Icon.Restart, "Домой", KnittedPicture.Game.Icon.Home);
            again.OnClick(() =>
            {
                Close();
                restart();
            });
            toHome.OnClick(home);
            Finish(12);
        }

        private static string SoundLabel(bool on) => on ? "Звук: включён" : "Звук: выключен";
    }

    /// <summary>
    /// Настройки — нижняя шторка, как в системных настройках iOS и Android: всё в зоне большого пальца,
    /// закрыть — крестиком, тапом мимо или системной «Назад». Переключатели меняются на месте, без
    /// переоткрытия окна. Опасное действие отдельно и красным, с подтверждением вторым касанием.
    /// </summary>
    public sealed class SettingsPopup : Popup
    {
        private const float RowH = 56f;

        public SettingsPopup(App app)
            : base(app, true)
        {
            AddTitle("Настройки", 24);

            var consent = app.Ads.NeedsConsent;
            var group = Group(consent ? 4 : 3);
            ToggleRow(group, 0, KnittedPicture.Game.Icon.SoundOn, Palette.Link, "Звук",
                () => app.Progress.SoundOn, app.SetSound);
            ToggleRow(group, 1, KnittedPicture.Game.Icon.Vibrate, Palette.Danger, "Вибрация",
                () => app.Progress.Data.hapticsOn, app.SetHaptics);
            ToggleRow(group, 2, KnittedPicture.Game.Icon.Bell, Palette.CoinShadow, "Напоминания",
                () => app.Progress.Data.remindersOn, on =>
                {
                    app.Progress.Data.remindersOn = on;
                    if (on)
                    {
                        Reminders.AskPermission();
                    }

                    app.Save();
                });
            if (consent)
            {
                ToggleRow(group, 3, KnittedPicture.Game.Icon.Gift, Palette.Primary, "Персональная реклама",
                    () => app.Progress.Data.adsConsent == 1, app.SetAdsConsent);
            }

            Space(12f);

            // Покупки — только там, где они работают (RuStore с SDK). «Без рекламы» пропадает после покупки.
            var purchases = app.Purchases.Available;
            var buyNoAds = purchases && !app.Progress.Data.adsRemoved;
            var help = Group(1 + (buyNoAds ? 1 : 0) + (purchases ? 1 : 0));
            var row = 0;
            NavRow(help, row++, KnittedPicture.Game.Icon.Info, Palette.Primary, "Как играть", () =>
            {
                Close();
                app.ShowIntro();
            });
            if (buyNoAds)
            {
                NavRow(help, row++, KnittedPicture.Game.Icon.Star, Palette.CoinShadow, "Убрать рекламу", () =>
                {
                    Close();
                    app.BuyNoAds();
                });
            }

            if (purchases)
            {
                NavRow(help, row, KnittedPicture.Game.Icon.Restart, Palette.Link, "Восстановить покупки", () => app.RestorePurchases(true));
            }

            Space(12f);

            // Сброс — отдельной строкой, красным. Первое касание спрашивает, второе стирает.
            var danger = Group(1);
            var resetRow = Ui.Place(Ui.Node(danger, "Reset"), 0, 0, Inner, RowH);
            Ui.Stretch(Ui.Image(resetRow, "Hit", null, Color.clear, true).rectTransform);
            var resetLabel = Ui.Label(resetRow, "Сбросить прогресс", 16, Palette.Danger, 0, 0, Inner, RowH);
            var armed = false;
            var resetTap = resetRow.gameObject.AddComponent<Tap>();
            resetTap.OnClick(() =>
            {
                if (armed)
                {
                    app.ResetProgress();
                    return;
                }

                armed = true;
                resetLabel.text = "Точно стереть всё? Нажми ещё раз";
                Haptics.Play(Haptic.Medium);
            });

            Space(6f);
            AddLink("Политика конфиденциальности").OnClick(() => Application.OpenURL(StoreConfig.PrivacyPolicyUrl));
            var version = AddText("Версия " + Application.version, 12, 1);
            version.color = Palette.Ink3;
            Finish(8);
        }

        /// <summary>Строка-переход: значок, подпись и стрелка вправо; тап по всей строке.</summary>
        private void NavRow(RectTransform group, int index, KnittedPicture.Game.Icon icon, Color tint, string title, System.Action open)
        {
            var row = Row(group, index, icon, tint, title);
            var chevron = Ui.IconImage(row, KnittedPicture.Game.Icon.Back, Palette.Ink3, Inner - 24f, RowH / 2f, 18f);
            chevron.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            row.gameObject.AddComponent<Tap>().OnClick(open);
        }

        /// <summary>Сгруппированный список: светлая подложка, строки через тонкие разделители.</summary>
        private RectTransform Group(int rows)
        {
            var box = Ui.Box(Card, "Group", 16, Palette.Surface).rectTransform;
            Ui.Place(box, 24, Cursor, Inner, rows * RowH);
            Ui.Outline(box, 16, Palette.Line, 1.5f);
            for (var i = 1; i < rows; i++)
            {
                var line = Ui.Box(box, "Divider", 1, Palette.Line).rectTransform;
                Ui.Place(line, 60f, i * RowH - 0.75f, Inner - 60f, 1.5f);
            }

            Space(rows * RowH);
            return box;
        }

        /// <summary>Строка: значок в цветном кружке и подпись слева.</summary>
        private RectTransform Row(RectTransform group, int index, KnittedPicture.Game.Icon icon, Color tint, string title)
        {
            var row = Ui.Place(Ui.Node(group, title), 0, index * RowH, Inner, RowH);
            Ui.Stretch(Ui.Image(row, "Hit", null, Color.clear, true).rectTransform);
            var circle = Ui.Image(row, "IconBack", Art.Circle, Palette.WithAlpha(tint, 0.16f));
            Ui.PlaceCenter(circle.rectTransform, 30f, RowH / 2f, 34f, 34f);
            Ui.IconImage(row, icon, tint, 30f, RowH / 2f, 20f);
            Ui.Label(row, title, 16, Palette.Ink, 58f, 0, Inner - 140f, RowH, false, TextAnchor.MiddleLeft);
            return row;
        }

        /// <summary>Строка с переключателем справа; тап по всей строке переключает.</summary>
        private void ToggleRow(RectTransform group, int index, KnittedPicture.Game.Icon icon, Color tint, string title,
            System.Func<bool> get, System.Action<bool> set)
        {
            var row = Row(group, index, icon, tint, title);
            const float trackW = 52f;
            const float trackH = 32f;
            var track = Ui.Box(row, "Switch", trackH / 2f, Palette.Inactive);
            Ui.PlaceCenter(track.rectTransform, Inner - 16f - trackW / 2f, RowH / 2f, trackW, trackH);
            var knob = Ui.Image(track.rectTransform, "Knob", Art.Circle, Color.white).rectTransform;
            Ui.PlaceCenter(knob, trackH / 2f, trackH / 2f, trackH - 6f, trackH - 6f);

            void Show(float t)
            {
                track.color = Color.Lerp(Palette.Inactive, Palette.Primary, t);
                knob.anchoredPosition = new Vector2(Mathf.Lerp(trackH / 2f, trackW - trackH / 2f, t), knob.anchoredPosition.y);
            }

            Show(get() ? 1f : 0f);
            row.gameObject.AddComponent<Tap>().OnClick(() =>
            {
                var on = !get();
                set(on);
                Tweens.Run(knob, 0.18f, t => Show(on ? t : 1f - t), Ease.OutCubic);
            });
        }
    }

    /// <summary>Не хватает монет на бустер: предлагаем видео, без давления.</summary>
    public sealed class NotEnoughPopup : Popup
    {
        public NotEnoughPopup(App app, BoosterType type, Action granted)
            : base(app)
        {
            AddIcon(BoosterInfo.Icon(type), Palette.Ink, Palette.CoinSoft, 68);
            AddTitle("Не хватает монет", 24);
            AddText($"{BoosterInfo.Name(type)} стоит {Economy.Price(type)}, у тебя {app.Progress.Coins}", 16, 1.6f);
            AddPrimary("Получить за видео").OnClick(() =>
            {
                Close();
                app.Ads.ShowRewarded("booster_" + type, ok =>
                {
                    if (!ok)
                    {
                        return;
                    }

                    app.Progress.AddStock(type, 1);
                    app.Save();
                    granted();
                });
            });
            AddLink("Не сейчас").OnClick(Close);
            Finish(8);
        }
    }

    public enum FailKind
    {
        Jam,
        TimeUp,
    }

    /// <summary>
    /// Затор или моль. Спасение (rescue) — за бустер из запаса, монеты или видео, и только то,
    /// что правда поможет; null — спасать нечем, остаётся спокойно начать заново.
    /// </summary>
    public sealed class FailPopup : Popup
    {
        public FailPopup(App app, FailKind kind, BoosterType? rescue, string reason, int percent, Action<BoosterType> rescued,
            Action restart, Action home, bool freeRescue = false, string? streakWarning = null, Color[]? needed = null,
            string? advice = null)
            : base(app, bottomSheet: true)
        {
            AddIcon(kind == FailKind.Jam ? KnittedPicture.Game.Icon.Knot : KnittedPicture.Game.Icon.Moth, Palette.Danger, Palette.DangerSoft, 64);
            AddTitle(kind == FailKind.Jam ? "Станок застрял" : "Моль добралась до картинки", 24);
            if (percent >= 50)
            {
                // «Почти!» мотивирует попробовать ещё раз сильнее лёгкой победы.
                var near = AddText($"Довязано {percent}%, почти!", 18, 1);
                near.color = Palette.Danger;
            }

            // Коротко и по очереди: есть спасение — что ждёт картинка (или причина), нет — совет на следующую попытку.
            var showNeeded = rescue != null && needed != null && needed.Length > 0;
            var showAdvice = rescue == null && !string.IsNullOrEmpty(advice);
            if (!showNeeded && !showAdvice)
            {
                AddText(reason, 15, 1.6f);
            }

            // Затор: какие цвета ждёт картинка — клубками в ряд.
            if (showNeeded && needed != null)
            {
                const float ball = 30f;
                const float gap = 8f;
                var shown = Math.Min(needed.Length, 5);
                var label = Ui.Label(Card, "Картинка ждёт:", 14, Palette.Ink2, 24, Cursor, Inner, ball, true, TextAnchor.MiddleLeft);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                var x = 24f + label.preferredWidth + 12f;
                var width = shown * ball + (shown - 1) * gap;
                if (x + width > 24f + Inner)
                {
                    x = 24f + Inner - width;
                }

                for (var i = 0; i < shown; i++)
                {
                    var yarn = Ui.Image(Card, "Needed", Art2.YarnBall(needed[i]), Color.white);
                    Ui.PlaceCenter(yarn.rectTransform, x + i * (ball + gap) + ball / 2f, Cursor + ball / 2f, ball, ball);
                }

                Space(ball + 10f);
            }

            // Приём на следующую попытку: проигрыш учит, а не просто наказывает.
            if (showAdvice)
            {
                var tip = AddText(advice!, 14, 2.4f);
                tip.color = Palette.Link;
            }

            if (rescue != null)
            {
                var type = rescue.Value;
                var stock = app.Progress.Stock(type);
                var title = freeRescue ? $"{BoosterInfo.Name(type)} · подарок серии"
                    : stock > 0
                    ? $"{BoosterInfo.Name(type)} · есть {stock}"
                    : $"{BoosterInfo.Name(type)} · {Economy.Price(type)} монет";
                AddPrimary(title).OnClick(() =>
                {
                    // Ножницы списываются, когда ими режут; слот и время — сразу.
                    var paid = type == BoosterType.Scissors ? freeRescue || app.Progress.CanAfford(type) : app.Progress.Pay(type) != Payment.NotEnough;
                    if (!paid)
                    {
                        app.Toast("Не хватает монет, можно за видео");
                        return;
                    }

                    app.Save();
                    Sfx.Play(Sound.Coin);
                    Close();
                    rescued(type);
                });
                if (!freeRescue)
                {
                AddSecondary(BoosterInfo.Name(type) + " за видео", KnittedPicture.Game.Icon.Video, 46f).OnClick(() =>
                {
                    Close();
                    app.Ads.ShowRewarded("rescue_" + type, ok =>
                    {
                        if (!ok)
                        {
                            app.ShowPopup(new FailPopup(app, kind, rescue, reason, percent, rescued, restart, home, freeRescue, streakWarning, needed, advice));
                            return;
                        }

                        if (type == BoosterType.Scissors)
                        {
                            // Ножницы за видео — в запас: ими и срежут.
                            app.Progress.AddStock(type, 1);
                            app.Save();
                        }

                        rescued(type);
                    });
                });
                }
            }

            if (streakWarning != null)
            {
                var warn = AddText(streakWarning, 13, 1);
                warn.color = Palette.CoinInk;
            }

            if (rescue == null)
            {
                // Спасать нечем — главная кнопка «Заново».
                AddPrimary("Заново").OnClick(() =>
                {
                    Close();
                    restart();
                });
            }

            // «Заново» и «Домой» — маленькие круглые кнопки внизу, не спорят со спасением. На белом окне
            // они залиты сиреневым и подписаны: значок без подписи и без заливки терялся.
            const float round = 48f;
            var rowX = 24f + Inner / 2f;
            if (rescue != null)
            {
                RoundAction(KnittedPicture.Game.Icon.Restart, "Заново", rowX - round / 2f - 48f, round, () =>
                {
                    Close();
                    restart();
                });
                RoundAction(KnittedPicture.Game.Icon.Home, "Домой", rowX - round / 2f + 48f, round, home);
            }
            else
            {
                RoundAction(KnittedPicture.Game.Icon.Home, "Домой", rowX - round / 2f, round, home);
            }

            Space(round + 24f);
            Finish(16);
        }

        private void RoundAction(KnittedPicture.Game.Icon icon, string caption, float x, float size, System.Action action)
        {
            Ui.RoundButton(Card, icon, x, Cursor, size, true).OnClick(action);
            Ui.Label(Card, caption, 12, Palette.Ink2, x - 20f, Cursor + size + 4f, size + 40f, 16);
        }

        public override bool Closable => false;

        protected override bool CloseOnDim => false;
    }
}
