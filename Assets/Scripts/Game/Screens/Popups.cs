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
                case BoosterType.Slot: return L.T("+1 место");
                case BoosterType.Scissors: return L.T("Ножницы");
                case BoosterType.Undo: return L.T("Отмена");
                case BoosterType.Time: return L.T("+20 секунд");
                default: return string.Empty;
            }
        }

        public static string Description(BoosterType type)
        {
            switch (type)
            {
                case BoosterType.Slot: return L.T("Добавляет на станок ещё одно место до конца уровня");
                case BoosterType.Scissors: return L.T("Берут любой клубок с поля или срезают лишний со станка");
                case BoosterType.Undo: return L.T("Возвращает поле и станок к началу последнего хода");
                case BoosterType.Time: return L.T("Отгоняет моль и даёт ещё 20 секунд");
                default: return string.Empty;
            }
        }

        /// <summary>Одна короткая строка — для списков, где полное описание перегружает.</summary>
        public static string Short(BoosterType type)
        {
            switch (type)
            {
                case BoosterType.Slot: return L.T("Добавить место на станке");
                case BoosterType.Scissors: return L.T("Взять или срезать любой клубок");
                case BoosterType.Undo: return L.T("Вернуть последний ход");
                case BoosterType.Time: return L.T("Отогнать моль на 20 секунд");
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
        /// <summary>after — что показать следом, когда окно закрыто (например, знакомство с бонусами пряжи).</summary>
        public BoosterIntroPopup(App app, BoosterType type, Action? after = null)
            : base(app)
        {
            Closed = after;
            AddIcon(BoosterInfo.Icon(type), Palette.Primary, Palette.PrimarySoft, 76);
            AddTitle(L.T("Новый бустер"), 24);
            AddText(BoosterInfo.Name(type), 20, 1);
            AddText(BoosterInfo.Description(type), 16, 2.2f);
            AddText(L.F("+{0} в подарок", Economy.FreeOnUnlock), 16, 1);
            AddPrimary(L.T("Понятно")).OnClick(Close);
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
            AddTitle(L.T("Видео-реклама"), 22);
            AddText(L.T("Здесь будет ролик из SDK издателя. Сейчас это заглушка."), 15, 2.2f);
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
            AddTitle(L.T("Пауза"));
            if (warning != null)
            {
                var warn = AddText(warning, 13, 1);
                warn.color = Palette.CoinInk;
            }

            AddPrimary(L.T("Продолжить")).OnClick(Close);
            // Звук и музыка переключаются на месте: меняются подпись и значок, окно остаётся.
            var sound = AddSecondary(SoundLabel(app.Progress.SoundOn), app.Progress.SoundOn ? KnittedPicture.Game.Icon.SoundOn : KnittedPicture.Game.Icon.SoundOff);
            BindToggle(sound, () => app.Progress.SoundOn, app.SetSound, SoundLabel,
                on => on ? KnittedPicture.Game.Icon.SoundOn : KnittedPicture.Game.Icon.SoundOff, false);
            var music = AddSecondary(MusicLabel(app.Progress.Data.musicOn), KnittedPicture.Game.Icon.Music, 50f);
            BindToggle(music, () => app.Progress.Data.musicOn, app.SetMusic, MusicLabel, _ => KnittedPicture.Game.Icon.Music, true);
            var (again, toHome) = AddPair(L.T("Заново"), KnittedPicture.Game.Icon.Restart, L.T("Домой"), KnittedPicture.Game.Icon.Home);
            again.OnClick(() =>
            {
                Close();
                restart();
            });
            toHome.OnClick(home);

            // «Меньше движения» и подсказки нужнее всего прямо в уровне: без выхода на главную.
            AddLink(L.T("Удобство")).OnClick(() =>
            {
                Close();
                app.ShowPopup(new ComfortPopup(app, () => app.ShowPopup(new PausePopup(app, restart, home, warning))));
            });
            Finish(12);
        }

        private static string MusicLabel(bool on) => on ? L.T("Музыка: включена") : L.T("Музыка: выключена");

        /// <summary>Кнопка-переключатель на месте: подпись, значок (и его положение под новую ширину подписи) меняются без пересоздания окна.</summary>
        private void BindToggle(Tap button, Func<bool> get, Action<bool> set, Func<bool, string> text,
            Func<bool, KnittedPicture.Game.Icon> icon, bool dimWhenOff)
        {
            var label = button.GetComponentInChildren<UnityEngine.UI.Text>();
            UnityEngine.UI.Image? image = null;
            foreach (var candidate in button.GetComponentsInChildren<UnityEngine.UI.Image>())
            {
                if (candidate.name.StartsWith("Icon", StringComparison.Ordinal))
                {
                    image = candidate;
                }
            }

            void Show(bool on, bool animate)
            {
                label.text = text(on);
                if (image == null)
                {
                    return;
                }

                var sprite = Art.Icon(icon(on));
                var changed = image.sprite != sprite;
                image.sprite = sprite;
                var tint = dimWhenOff && !on ? Palette.Ink3 : Palette.Ink;
                image.color = tint;
                var rt = image.rectTransform;
                rt.anchoredPosition = new Vector2(Inner / 2f - label.preferredWidth / 2f - 6f, rt.anchoredPosition.y);

                // Сменился значок: новый вырастает с 0.25 до 1 и проявляется за 0.3 с, без отскока. У музыки спрайт
                // один, её состояние читается цветом, поэтому она не «выпрыгивает». «Меньше движения» — сразу итог.
                Tweens.Kill(rt);
                rt.localScale = Vector3.one;
                if (!animate || !changed || Tweens.Calm)
                {
                    return;
                }

                rt.localScale = Vector3.one * 0.25f;
                image.color = Palette.WithAlpha(tint, 0f);
                Tweens.Run(rt, 0.3f, t =>
                {
                    rt.localScale = Vector3.one * Mathf.LerpUnclamped(0.25f, 1f, t);
                    image.color = Palette.WithAlpha(tint, t);
                }, Ease.OutCubic, () =>
                {
                    rt.localScale = Vector3.one;
                    image.color = tint;
                });
            }

            button.OnClick(() =>
            {
                var on = !get();
                set(on);
                Show(on, true);
            });
            if (dimWhenOff && image != null)
            {
                image.color = get() ? Palette.Ink : Palette.Ink3;
            }
        }

        private static string SoundLabel(bool on) => on ? L.T("Звук: включён") : L.T("Звук: выключен");
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
        /// Крестик, тап мимо и системная «Назад» делают то же самое (Popup.Dismiss), а не закрывают всё.
        /// </summary>
        protected void AddBack(Action back)
        {
            Back = back;
            Ui.RoundButton(Card, KnittedPicture.Game.Icon.Back, 8f, 8f, 44f, true).OnClick(Dismiss);
        }

        /// <summary>Подпись раздела над группой строк: мелкий жирный текст капсом, как в системных настройках.</summary>
        protected void AddSection(string text)
        {
            var label = Ui.Label(Card, text.ToUpperInvariant(), 12, Palette.Ink2, 40f, Cursor, Inner - 32f, 18f, true, TextAnchor.MiddleLeft);
            Ui.Fit(label, 10);
            Space(24f);
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
                // Шарик едет от того места, где он сейчас (тап посреди пути не рвёт движение), и замедляется к краю,
                // без отскока. В полёте чуть сплющивается, как капля; «Меньше движения» без сплющивания.
                var from = Mathf.Clamp01((knob.anchoredPosition.x - trackH / 2f) / (trackW - trackH));
                var to = on ? 1f : 0f;
                Tweens.Kill(knob);
                Tweens.Run(knob, 0.36f, t =>
                {
                    Show(Mathf.LerpUnclamped(from, to, t));
                    var squash = Tweens.Calm ? 0f : 0.18f * Ease.Bump(Mathf.Clamp01(t));
                    knob.localScale = new Vector3(1f + squash, 1f - squash * 0.6f, 1f);
                }, Ease.OutCubic, () => knob.localScale = Vector3.one);
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
            AddTitle(L.T("Настройки"), 24);

            // Строки только для того, что в этой сборке действительно работает: напоминания — при пакете
            // уведомлений на телефоне, персональная реклама — когда SDK рекламы спрашивает согласие.
            var consent = app.Ads.NeedsConsent;
            var reminders = Reminders.Supported;
            var group = Group(3 + (reminders ? 1 : 0) + (consent ? 1 : 0));
            var line = 0;
            ToggleRow(group, line++, KnittedPicture.Game.Icon.SoundOn, Palette.Link, L.T("Звук"),
                () => app.Progress.SoundOn, app.SetSound);
            ToggleRow(group, line++, KnittedPicture.Game.Icon.Music, Palette.Primary, L.T("Музыка"),
                () => app.Progress.Data.musicOn, app.SetMusic);
            ToggleRow(group, line++, KnittedPicture.Game.Icon.Vibrate, Palette.Link, L.T("Вибрация"),
                () => app.Progress.Data.hapticsOn, app.SetHaptics);
            if (reminders)
            {
                ToggleRow(group, line++, KnittedPicture.Game.Icon.Bell, Palette.CoinShadow, L.T("Напоминания"),
                    () => app.Progress.Data.remindersOn, on =>
                    {
                        app.Progress.Data.remindersOn = on;
                        if (on)
                        {
                            Reminders.AskPermission();
                        }

                        app.Save();
                    });
            }

            if (consent)
            {
                ToggleRow(group, line, KnittedPicture.Game.Icon.Gift, Palette.Primary, L.T("Персональная реклама"),
                    () => app.Progress.Data.adsConsent == 1, app.SetAdsConsent);
            }

            Space(12f);

            // «Без рекламы» — только там, где покупки работают (RuStore с SDK), и пропадает после покупки.
            var buyNoAds = app.Purchases.Available && !app.Progress.Data.adsRemoved;
            var help = Group(4 + (buyNoAds ? 1 : 0));
            var row = 0;

            // Язык: названия языков на них самих, чтобы свой язык нашёлся, даже если интерфейс непонятен.
            NavRow(help, row++, new Badge(L.IsRussian ? "Ru" : "En"), Palette.Primary,
                L.T("Язык") + ": " + (L.IsRussian ? "Русский" : "English"),
                () => app.SetLanguage(L.IsRussian ? L.English : L.Russian));
            NavRow(help, row++, new Badge("Aa"), Palette.Link, L.T("Удобство"), () =>
            {
                Close();
                app.ShowPopup(new ComfortPopup(app, () => app.ShowPopup(new SettingsPopup(app))));
            });
            NavRow(help, row++, KnittedPicture.Game.Icon.Info, Palette.Primary, L.T("Как играть"), () =>
            {
                Close();
                app.ShowIntro();
            });
            if (buyNoAds)
            {
                Analytics.Event("offer_view", ("placement", "no_ads"));
                NavRow(help, row++, KnittedPicture.Game.Icon.Star, Palette.CoinShadow, L.T("Убрать рекламу"), () =>
                {
                    Close();
                    app.BuyNoAds();
                });
            }

            NavRow(help, row, KnittedPicture.Game.Icon.Album, Palette.Ink2, L.T("Об игре"), () =>
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

            AddTitle(L.T("Удобство"), 24);
            var data = app.Progress.Data;
            AddSection(L.T("Экран и движение"));
            var group = Group(3, TallRowH);
            ToggleRow(group, 0, KnittedPicture.Game.Icon.Pause, Palette.Primary, L.T("Меньше движения"),
                () => data.reduceMotion, on => Set(() => data.reduceMotion = on),
                L.T("Без покачиваний, всплытий, пружин и тряски"), TallRowH);
            ToggleRow(group, 1, new Badge("Aa"), Palette.CoinShadow, L.T("Крупный текст"),
                () => data.largeText, on => Set(() => data.largeText = on),
                L.T("Мелкие подписи крупнее, где помещаются"), TallRowH);
            ToggleRow(group, 2, KnittedPicture.Game.Icon.Bolt, Palette.Ink2, L.T("Экономия заряда"),
                () => data.batterySaver, on => Set(() => data.batterySaver = on),
                L.T("30 кадров в секунду вместо 60: телефон меньше греется"), TallRowH);

            Space(12f);
            AddSection(L.T("Подсказки"));
            var hints = Group(1, TallRowH);
            ToggleRow(hints, 0, KnittedPicture.Game.Icon.Hand, Palette.Primary, L.T("Подсказка хода"),
                () => data.idleHints, on => Set(() => data.idleHints = on),
                L.T("Нужный клубок засветится, если долго нет хода"), TallRowH);

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
            AddTitle(L.T("Об игре"), 24);

            var rate = StoreLinks.Rate;
            var mail = StoreLinks.Support(app);
            var purchases = app.Purchases.Available;
            var rows = 1 + (rate != null ? 1 : 0) + (mail != null ? 1 : 0) + (purchases ? 1 : 0);
            var group = Group(rows);
            var row = 0;
            if (rate != null)
            {
                NavRow(group, row++, KnittedPicture.Game.Icon.Star, Palette.CoinShadow, L.T("Оценить игру"), () => Application.OpenURL(rate));
            }

            if (mail != null)
            {
                NavRow(group, row++, KnittedPicture.Game.Icon.Bell, Palette.Link, L.T("Написать нам"), () => Application.OpenURL(mail));
            }

            if (purchases)
            {
                NavRow(group, row++, KnittedPicture.Game.Icon.Restart, Palette.Link, L.T("Восстановить покупки"), () => app.RestorePurchases(true));
            }

            NavRow(group, row, KnittedPicture.Game.Icon.Info, Palette.Ink2, L.T("Политика конфиденциальности"),
                () => Application.OpenURL(StoreConfig.PrivacyPolicyUrl));

            Space(12f);

            // Сброс — отдельной строкой, красным. Первое касание спрашивает, второе стирает.
            var danger = Group(1);
            var resetRow = Ui.Place(Ui.Node(danger, "Reset"), 0, 0, Inner, RowH);
            Ui.Stretch(Ui.Image(resetRow, "Hit", null, Color.clear, true).rectTransform);
            var resetLabel = Ui.Label(resetRow, L.T("Сбросить прогресс"), 16, Palette.Danger, 0, 0, Inner, RowH);
            var armed = false;
            resetRow.gameObject.AddComponent<Tap>().OnClick(() =>
            {
                if (armed)
                {
                    app.ResetProgress();
                    return;
                }

                armed = true;
                resetLabel.text = L.T("Точно стереть всё? Тапни ещё раз");
                Haptics.Play(Haptic.Medium);
            });

            Space(10f);
            var version = AddText(L.T("Версия ") + Application.version, 12, 1);
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
            var deficit = Math.Max(1, Economy.Price(type) - app.Progress.Coins);
            // Монеты могли прийти, пока окно открыто: тогда вместо «Не хватает 1» просто название бустера.
            var missing = Economy.Price(type) - app.Progress.Coins;
            AddTitle(missing > 0 ? L.F("Не хватает {0} {1}", deficit, YarnCatalog.CoinsGenitive(deficit)) : BoosterInfo.Name(type), 24);
            AddText(L.F("{0} стоит {1} {2}", BoosterInfo.Name(type), Economy.Price(type), YarnCatalog.CoinsWord(Economy.Price(type))), 16, 1.6f);

            // Бустер за видео — не больше Economy.VideoBoostersPerDay в день: иначе монеты и бустеры теряют цену.
            if (app.Progress.VideoBoostersLeft(app.Today) > 0)
            {
                Analytics.Event("offer_view", ("placement", "booster_" + type), ("price", Economy.Price(type)));
                AddPrimary(L.T("Получить за видео")).OnClick(() =>
                {
                    // Окно остаётся, пока видео не досмотрено: при отмене или без видео игрок видит то же окно и тост с причиной.
                    app.ShowRewarded("booster_" + type, () =>
                    {
                        // Видео досмотрено: запас начисляем в любом случае, но закрытое окно и его экран не трогаем.
                        app.Progress.AddStock(type, 1);
                        app.Progress.RecordVideoBooster(app.Today);
                        app.Save();
                        if (!IsOpen)
                        {
                            return;
                        }

                        Close();
                        granted();
                    });
                });
                AddLimit(VideoLimitText);
            }
            else
            {
                AddText(L.T("Бустеры за видео на сегодня закончились. Монеты дают уровни, задания и подарок дня"), 14, 2f);
                AddLimit(VideoLimitText);
            }

            AddLink(L.T("Не сейчас")).OnClick(Close);
            Finish(8);
        }

        /// <summary>«Использовано 1 из 3 · новые через 5 ч»: лимит бустеров за видео и когда он обновится.</summary>
        private string VideoLimitText() => Limits.Used(Economy.VideoBoostersPerDay - App.Progress.VideoBoostersLeft(App.Today),
            Economy.VideoBoostersPerDay, Limits.NextMidnight());
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
        public FailPopup(App app, FailKind kind, BoosterType? rescue, string reason, int percent, int left, Action<BoosterType, string> rescued,
            Action restart, Action home, bool freeRescue = false, string? streakWarning = null, Color[]? needed = null,
            string? advice = null, int rescuesBefore = 0, bool videoAllowed = true, Action? videoUsed = null)
            : base(app, bottomSheet: true)
        {
            AddIcon(kind == FailKind.Jam ? KnittedPicture.Game.Icon.Knot : KnittedPicture.Game.Icon.Moth, Palette.Danger, Palette.DangerSoft, 64);
            AddTitle(kind == FailKind.Jam ? L.T("Станок застрял") : L.T("Моль добралась до картинки"), 24);
            // Сколько не хватило, видно всегда: это прогресс, а не упрёк. «Осталось 3 петли» понятнее процентов, а полоса
            // показывает, как близко было. От половины или когда осталось мало рядом тёплое «почти!» (не красное):
            // оно подталкивает попробовать ещё раз сильнее лёгкой победы.
            {
                var near = percent >= 50 || (left > 0 && left <= 5);
                var main = left > 0
                    ? L.F(L.Plural(left, "Осталась {0} петля", "Осталось {0} петли", "Осталось {0} петель"), left)
                    : L.F("Довязано {0}%", percent);
                var done = Ui.Label(Card, main, 18, left > 0 ? Palette.Ink : Palette.Ink2, 24, Cursor, Inner, 24);
                if (near)
                {
                    done.horizontalOverflow = HorizontalWrapMode.Overflow;
                    done.alignment = TextAnchor.MiddleLeft;
                    var soon = Ui.Label(Card, L.T("почти!"), 18, Palette.CoinInk, 24, Cursor, Inner, 24);
                    soon.horizontalOverflow = HorizontalWrapMode.Overflow;
                    soon.alignment = TextAnchor.MiddleLeft;
                    var gap = 8f;
                    var total = done.preferredWidth + gap + soon.preferredWidth;
                    var x = 24f + Inner / 2f - total / 2f;
                    Ui.Place(done.rectTransform, x, Cursor, done.preferredWidth + 2f, 24);
                    Ui.Place(soon.rectTransform, x + done.preferredWidth + gap, Cursor, soon.preferredWidth + 2f, 24);
                }

                if (left > 0)
                {
                    // Полоса: дорожка и заливка на довязанное, по центру, не шире 220.
                    var barWidth = Math.Min(Inner, 220f);
                    var barX = 24f + (Inner - barWidth) / 2f;
                    var track = Ui.Box(Card, "Progress", 4, Palette.Track);
                    Ui.Place(track.rectTransform, barX, Cursor + 30f, barWidth, 8);
                    var fill = Ui.Box(track.rectTransform, "Fill", 4, Palette.Primary);
                    Ui.Place(fill.rectTransform, 0, 0, Math.Max(8f, barWidth * Math.Min(100, Math.Max(0, percent)) / 100f), 8);
                    Space(44f);
                }
                else
                {
                    Space(30f);
                }
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
                // Подпись и клубки в одну строку; не влезают (320 точек, английский) — клубки уменьшаются до 24,
                // а если и так тесно, переносятся на вторую строку под подписью.
                const float gap = 8f;
                var shown = Math.Min(needed.Length, 5);
                var label = Ui.Label(Card, L.T("Картинка ждёт:"), 14, Palette.Ink2, 24, Cursor, Inner, 30f, true, TextAnchor.MiddleLeft);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                var ball = 30f;
                var room = Inner - label.preferredWidth - 12f;
                var width = shown * ball + (shown - 1) * gap;
                if (width > room)
                {
                    ball = 24f;
                    width = shown * ball + (shown - 1) * gap;
                }

                var wrap = width > room;
                var x = wrap ? 24f : 24f + Inner - width;
                if (!wrap && x < 24f + label.preferredWidth + 12f)
                {
                    x = 24f + label.preferredWidth + 12f;
                }

                var rowY = Cursor;
                if (wrap)
                {
                    // Вторая строка: подпись над клубками по центру окна, клубки ровным рядом.
                    label.alignment = TextAnchor.MiddleCenter;
                    Space(30f);
                    rowY = Cursor;
                    x = 24f + (Inner - width) / 2f;
                }

                for (var i = 0; i < shown; i++)
                {
                    var yarn = Ui.Image(Card, "Needed", Art2.YarnBall(needed[i]), Color.white);
                    Ui.PlaceCenter(yarn.rectTransform, x + i * (ball + gap) + ball / 2f, rowY + ball / 2f, ball, ball);
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
                var title = freeRescue ? L.F("{0} · в подарок", BoosterInfo.Name(type))
                    : stock > 0
                    ? L.F("{0} · есть {1}", BoosterInfo.Name(type), stock)
                    : L.F("{0} · {1} {2}", BoosterInfo.Name(type), Economy.RescuePrice(type, rescuesBefore), YarnCatalog.CoinsWord(Economy.RescuePrice(type, rescuesBefore)));
                var viaPay = freeRescue ? "free" : stock > 0 ? "stock" : "coins";
                Analytics.Event("offer_view", ("placement", "rescue_" + type), ("price", freeRescue ? 0 : Economy.RescuePrice(type, rescuesBefore)),
                    ("stock", stock), ("video", !freeRescue && videoAllowed));
                var rescueButton = AddPrimary(title);
                rescueButton.OnClick(() =>
                {
                    // Ножницы списываются, когда ими режут; слот и время — сразу.
                    // Цена растёт с каждым спасением в этой попытке (Economy.RescuePrice).
                    var paid = type == BoosterType.Scissors
                        ? freeRescue || app.Progress.CanAffordRescue(type, rescuesBefore)
                        : app.Progress.PayRescue(type, rescuesBefore) != Payment.NotEnough;
                    if (!paid)
                    {
                        var deficit = Math.Max(1, Economy.RescuePrice(type, rescuesBefore) - app.Progress.Coins);
                        Ui.Deny((RectTransform)rescueButton.transform, L.F("Не хватает {0} {1}", deficit, YarnCatalog.CoinsGenitive(deficit)));
                        return;
                    }

                    app.Save();
                    Sfx.Play(Sound.Coin);
                    Close();
                    rescued(type, viaPay);
                });
                // Спасение за видео — одно на попытку: дальше только запас или монеты. Когда оно уже было,
                // кнопка не прячется молча, а становится строкой с причиной.
                if (!freeRescue && !videoAllowed)
                {
                    Limits.Line(Card, L.T("Спасение за видео уже было в этой попытке"), 24, Cursor, Inner, null, 20f);
                    Space(26f);
                }

                if (!freeRescue && videoAllowed)
                {
                    AddSecondary(BoosterInfo.Name(type) + L.T(" за видео"), KnittedPicture.Game.Icon.Video, 46f).OnClick(() =>
                    {
                        // Окно проигрыша не закрываем и не пересоздаём: при отмене или без видео оно остаётся как было, с тостом.
                        app.ShowRewarded("rescue_" + type, () =>
                        {
                            if (!IsOpen)
                            {
                                return;
                            }

                            Close();
                            videoUsed?.Invoke();

                            if (type == BoosterType.Scissors)
                            {
                                // Ножницы за видео — в запас: ими и срежут.
                                app.Progress.AddStock(type, 1);
                                app.Save();
                            }

                            rescued(type, "video");
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
                AddPrimary(L.T("Заново")).OnClick(() =>
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
                RoundAction(KnittedPicture.Game.Icon.Restart, L.T("Заново"), rowX - round / 2f - 48f, round, () =>
                {
                    Close();
                    restart();
                });
                RoundAction(KnittedPicture.Game.Icon.Home, L.T("Домой"), rowX - round / 2f + 48f, round, home);
            }
            else
            {
                RoundAction(KnittedPicture.Game.Icon.Home, L.T("Домой"), rowX - round / 2f, round, home);
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
