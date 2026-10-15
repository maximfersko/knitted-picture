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

            // «Меньше движения» и подсказки нужнее всего прямо в уровне: без выхода на главную.
            AddLink("Удобство").OnClick(() =>
            {
                Close();
                app.ShowPopup(new ComfortPopup(app, () => app.ShowPopup(new PausePopup(app, restart, home, warning))));
            });
            Finish(12);
        }

        private static string SoundLabel(bool on) => on ? "Звук: включён" : "Звук: выключен";
    }

    /// <summary>
    /// Шторка-список, как системные настройки iOS и Android: сгруппированные строки со значком, подписью
    /// (и пояснением), переключателем или стрелкой. Общая основа для «Настроек», «Удобства» и «Об игре».
    /// </summary>
    public abstract class ListSheet : Popup
    {
        protected const float RowH = 56f;

        /// <summary>Строка с пояснением в две строки.</summary>
        protected const float TallRowH = 70f;

        protected ListSheet(App app)
            : base(app, true)
        {
        }

        /// <summary>Значок строки: фигура из набора иконок, готовый спрайт или пара букв.</summary>
        protected readonly struct Badge
        {
            public Badge(KnittedPicture.Game.Icon icon) : this(icon, null, null)
            {
            }

            public Badge(Sprite sprite) : this(null, sprite, null)
            {
            }

            public Badge(string glyph) : this(null, null, glyph)
            {
            }

            private Badge(KnittedPicture.Game.Icon? icon, Sprite? sprite, string? glyph)
            {
                Icon = icon;
                Sprite = sprite;
                Glyph = glyph;
            }

            public KnittedPicture.Game.Icon? Icon { get; }

            public Sprite? Sprite { get; }

            public string? Glyph { get; }

            public static implicit operator Badge(KnittedPicture.Game.Icon icon) => new Badge(icon);
        }

        /// <summary>
        /// Кнопка «назад» слева от заголовка вложенной шторки: закрывает её и открывает ту, откуда пришли.
        /// Крестик и тап мимо закрывают совсем.
        /// </summary>
        protected void AddBack(Action back)
        {
            Ui.RoundButton(Card, KnittedPicture.Game.Icon.Back, 10f, 10f, 44f, true).OnClick(() =>
            {
                Close();
                back();
            });
        }

        /// <summary>Сгруппированный список: светлая подложка, строки через тонкие разделители.</summary>
        protected RectTransform Group(int rows, float rowH = RowH)
        {
            var box = Ui.Box(Card, "Group", 16, Palette.Surface).rectTransform;
            Ui.Place(box, 24, Cursor, Inner, rows * rowH);
            Ui.Outline(box, 16, Palette.Line, 1.5f);
            for (var i = 1; i < rows; i++)
            {
                var line = Ui.Box(box, "Divider", 1, Palette.Line).rectTransform;
                Ui.Place(line, 60f, i * rowH - 0.75f, Inner - 60f, 1.5f);
            }

            Space(rows * rowH);
            return box;
        }

        /// <summary>Строка: значок в цветном кружке, подпись и (если есть) пояснение мельче под ней.</summary>
        protected RectTransform Row(RectTransform group, int index, Badge badge, Color tint, string title, string? note = null, float rowH = RowH)
        {
            var row = Ui.Place(Ui.Node(group, title), 0, index * rowH, Inner, rowH);
            Ui.Stretch(Ui.Image(row, "Hit", null, Color.clear, true).rectTransform);
            var circle = Ui.Image(row, "IconBack", Art.Circle, Palette.WithAlpha(tint, 0.16f));
            Ui.PlaceCenter(circle.rectTransform, 30f, rowH / 2f, 34f, 34f);
            if (badge.Sprite != null)
            {
                var image = Ui.Image(row, "Badge", badge.Sprite, Color.white);
                Ui.PlaceCenter(image.rectTransform, 30f, rowH / 2f, 24f, 24f);
            }
            else if (badge.Glyph != null)
            {
                Ui.Label(row, badge.Glyph, 15, tint, 13f, rowH / 2f - 17f, 34f, 34f);
            }
            else if (badge.Icon != null)
            {
                Ui.IconImage(row, badge.Icon.Value, tint, 30f, rowH / 2f, 20f);
            }

            var textW = Inner - 140f;
            if (note == null)
            {
                Ui.Label(row, title, 16, Palette.Ink, 58f, 0, textW, rowH, false, TextAnchor.MiddleLeft);
            }
            else
            {
                Ui.Fit(Ui.Label(row, title, 16, Palette.Ink, 58f, 9f, textW, 24f, false, TextAnchor.MiddleLeft), 13);
                var hint = Ui.Label(row, note, 12, Palette.Ink2, 58f, 32f, textW, rowH - 38f, false, TextAnchor.UpperLeft);
                hint.lineSpacing = 0.9f;
                Ui.Fit(hint, 10);
            }

            return row;
        }

        /// <summary>Строка-переход: значок, подпись и стрелка вправо; тап по всей строке.</summary>
        protected void NavRow(RectTransform group, int index, Badge badge, Color tint, string title, Action open, string? note = null, float rowH = RowH)
        {
            var row = Row(group, index, badge, tint, title, note, rowH);
            var chevron = Ui.IconImage(row, KnittedPicture.Game.Icon.Back, Palette.Ink3, Inner - 24f, rowH / 2f, 18f);
            chevron.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            row.gameObject.AddComponent<Tap>().OnClick(open);
        }

        /// <summary>Строка с переключателем справа; тап по всей строке переключает.</summary>
        protected void ToggleRow(RectTransform group, int index, Badge badge, Color tint, string title,
            Func<bool> get, Action<bool> set, string? note = null, float rowH = RowH)
        {
            var row = Row(group, index, badge, tint, title, note, rowH);
            const float trackW = 52f;
            const float trackH = 32f;
            var track = Ui.Box(row, "Switch", trackH / 2f, Palette.Inactive);
            Ui.PlaceCenter(track.rectTransform, Inner - 16f - trackW / 2f, rowH / 2f, trackW, trackH);
            var knob = Ui.Image(track.rectTransform, "Knob", Art.Circle, Color.white).rectTransform;
            Ui.PlaceCenter(knob, trackH / 2f, trackH / 2f, trackH - 6f, trackH - 6f);

            void Show(float t)
            {
                track.color = Color.Lerp(Palette.Inactive, Palette.Primary, t);
                knob.anchoredPosition = new Vector2(Mathf.LerpUnclamped(trackH / 2f, trackW - trackH / 2f, t), knob.anchoredPosition.y);
            }

            Show(get() ? 1f : 0f);
            row.gameObject.AddComponent<Tap>().OnClick(() =>
            {
                var on = !get();
                set(on);
                // Шарик перескакивает на пружине и чуть сплющивается в полёте, как капля.
                Tweens.Kill(knob);
                Tweens.Run(knob, 0.36f, t =>
                {
                    Show(on ? t : 1f - t);
                    var squash = 0.18f * Ease.Bump(Mathf.Clamp01(t));
                    knob.localScale = new Vector3(1f + squash, 1f - squash * 0.6f, 1f);
                }, Tweens.Calm ? Ease.OutCubic : Ease.Spring, () => knob.localScale = Vector3.one);
            });
        }
    }

    /// <summary>
    /// Настройки — нижняя шторка, как в системных настройках iOS и Android: всё в зоне большого пальца,
    /// закрыть — крестиком, тапом мимо или системной «Назад». Переключатели меняются на месте, без
    /// переоткрытия окна. Частое — сверху (звук, вибрация, напоминания), удобство и справка — переходами,
    /// редкое и опасное (покупки, связь, сброс) — во вложенной шторке «Об игре».
    /// </summary>
    public sealed class SettingsPopup : ListSheet
    {
        public SettingsPopup(App app)
            : base(app)
        {
            AddTitle("Настройки", 24);

            var consent = app.Ads.NeedsConsent;
            var group = Group(consent ? 5 : 4);
            ToggleRow(group, 0, KnittedPicture.Game.Icon.SoundOn, Palette.Link, "Звук",
                () => app.Progress.SoundOn, app.SetSound);
            ToggleRow(group, 1, KnittedPicture.Game.Icon.Star, Palette.Primary, "Музыка",
                () => app.Progress.Data.musicOn, app.SetMusic);
            ToggleRow(group, 2, KnittedPicture.Game.Icon.Vibrate, Palette.Danger, "Вибрация",
                () => app.Progress.Data.hapticsOn, app.SetHaptics);
            ToggleRow(group, 3, KnittedPicture.Game.Icon.Bell, Palette.CoinShadow, "Напоминания",
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
                ToggleRow(group, 4, KnittedPicture.Game.Icon.Gift, Palette.Primary, "Персональная реклама",
                    () => app.Progress.Data.adsConsent == 1, app.SetAdsConsent);
            }

            Space(12f);

            // «Без рекламы» — только там, где покупки работают (RuStore с SDK), и пропадает после покупки.
            var buyNoAds = app.Purchases.Available && !app.Progress.Data.adsRemoved;
            var help = Group(3 + (buyNoAds ? 1 : 0));
            var row = 0;
            NavRow(help, row++, new Badge("Aa"), Palette.Link, "Удобство", () =>
            {
                Close();
                app.ShowPopup(new ComfortPopup(app, () => app.ShowPopup(new SettingsPopup(app))));
            });
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

            NavRow(help, row, KnittedPicture.Game.Icon.Album, Palette.Ink2, "Об игре", () =>
            {
                Close();
                app.ShowPopup(new AboutPopup(app));
            });

            Finish(12);
        }
    }

    /// <summary>
    /// Удобство: без лишнего движения, с текстом покрупнее, без подсказок-напоминаний и с экономией заряда.
    /// По Game Accessibility Guidelines и Xbox Accessibility Guidelines: давать остановить повторяющееся движение,
    /// крупный текст. Открывается из настроек и из паузы; движение меняется со следующей анимации,
    /// крупный текст — на перестроенном экране.
    /// </summary>
    public sealed class ComfortPopup : ListSheet
    {
        private readonly bool _largeText;

        public ComfortPopup(App app, Action? back = null)
            : base(app)
        {
            _largeText = app.Progress.Data.largeText;
            if (back != null)
            {
                AddBack(back);
            }

            AddTitle("Удобство", 24);
            var data = app.Progress.Data;
            var group = Group(4, TallRowH);
            ToggleRow(group, 0, KnittedPicture.Game.Icon.Pause, Palette.Primary, "Меньше движения",
                () => data.reduceMotion, on => Set(() => data.reduceMotion = on),
                "Без покачиваний, всплытий, пружин и тряски", TallRowH);
            ToggleRow(group, 1, new Badge("Aa"), Palette.CoinShadow, "Крупный текст",
                () => data.largeText, on => Set(() => data.largeText = on),
                "Мелкие подписи крупнее, где помещаются", TallRowH);
            ToggleRow(group, 2, KnittedPicture.Game.Icon.Hand, Palette.Danger, "Подсказка при паузе",
                () => data.idleHints, on => Set(() => data.idleHints = on),
                "Рука покажет ход, если долго нет хода", TallRowH);
            ToggleRow(group, 3, KnittedPicture.Game.Icon.Bolt, Palette.Ink2, "Экономия заряда",
                () => data.batterySaver, on => Set(() => data.batterySaver = on),
                "30 кадров в секунду вместо 60: телефон меньше греется", TallRowH);

            // Крупный текст ложится на новые подписи: экран под шторкой перестраивается, когда она закрыта.
            Closed = () =>
            {
                if (App.Progress.Data.largeText != _largeText)
                {
                    App.RefreshScreen();
                }
            };
            Finish(12);
        }

        private void Set(Action change)
        {
            change();
            App.ApplyComfort();
        }
    }

    /// <summary>
    /// Об игре: оценить в магазине, написать разработчику (письмо уже с версией и моделью телефона),
    /// восстановить покупки, политика, версия и — отдельно, красным, с подтверждением — сброс прогресса.
    /// </summary>
    public sealed class AboutPopup : ListSheet
    {
        public AboutPopup(App app)
            : base(app)
        {
            AddBack(() => app.ShowPopup(new SettingsPopup(app)));
            AddTitle("Об игре", 24);

            var rate = StoreLinks.Rate;
            var mail = StoreLinks.Support(app);
            var purchases = app.Purchases.Available;
            var rows = 1 + (rate != null ? 1 : 0) + (mail != null ? 1 : 0) + (purchases ? 1 : 0);
            var group = Group(rows);
            var row = 0;
            if (rate != null)
            {
                NavRow(group, row++, KnittedPicture.Game.Icon.Star, Palette.CoinShadow, "Оценить игру", () => Application.OpenURL(rate));
            }

            if (mail != null)
            {
                NavRow(group, row++, KnittedPicture.Game.Icon.Bell, Palette.Link, "Написать нам", () => Application.OpenURL(mail));
            }

            if (purchases)
            {
                NavRow(group, row++, KnittedPicture.Game.Icon.Restart, Palette.Link, "Восстановить покупки", () => app.RestorePurchases(true));
            }

            NavRow(group, row, KnittedPicture.Game.Icon.Info, Palette.Ink2, "Политика конфиденциальности",
                () => Application.OpenURL(StoreConfig.PrivacyPolicyUrl));

            Space(12f);

            // Сброс — отдельной строкой, красным. Первое касание спрашивает, второе стирает.
            var danger = Group(1);
            var resetRow = Ui.Place(Ui.Node(danger, "Reset"), 0, 0, Inner, RowH);
            Ui.Stretch(Ui.Image(resetRow, "Hit", null, Color.clear, true).rectTransform);
            var resetLabel = Ui.Label(resetRow, "Сбросить прогресс", 16, Palette.Danger, 0, 0, Inner, RowH);
            var armed = false;
            resetRow.gameObject.AddComponent<Tap>().OnClick(() =>
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

            Space(10f);
            var version = AddText("Версия " + Application.version, 12, 1);
            version.color = Palette.Ink3;
            Finish(8);
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
            // они залиты светло-серым и подписаны: значок без подписи и без заливки терялся.
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
