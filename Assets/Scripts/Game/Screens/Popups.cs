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
                case BoosterType.Scissors: return "Достают любой клубок с поля — даже из глубины, во льду или под замком — или срезают клубок со станка";
                case BoosterType.Undo: return "Возвращает последний ход, если поторопился";
                case BoosterType.Time: return "Отгоняет моль: +20 секунд к таймеру";
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
            AddText("Здесь будет ролик из SDK издателя. Сейчас — заглушка.", 15, 2.2f);
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
        public PausePopup(App app, Action restart)
            : base(app)
        {
            AddTitle("Пауза");
            AddPrimary("Продолжить").OnClick(Close);
            AddSecondary("Начать заново", KnittedPicture.Game.Icon.Restart).OnClick(() =>
            {
                Close();
                restart();
            });
            var sound = AddSecondary(SoundLabel(app.Progress.SoundOn), app.Progress.SoundOn ? KnittedPicture.Game.Icon.SoundOn : KnittedPicture.Game.Icon.SoundOff);
            sound.OnClick(() =>
            {
                app.SetSound(!app.Progress.SoundOn);
                Close();
                app.ShowPopup(new PausePopup(app, restart));
            });
            AddLink("На главную", KnittedPicture.Game.Icon.Home).OnClick(app.ShowHome);
            Finish(12);
        }

        private static string SoundLabel(bool on) => on ? "Звук: включён" : "Звук: выключен";
    }

    public sealed class SettingsPopup : Popup
    {
        private int _resetTaps;

        public SettingsPopup(App app)
            : base(app)
        {
            AddTitle("Настройки");
            var on = app.Progress.SoundOn;
            AddSecondary(on ? "Звук: включён" : "Звук: выключен", on ? KnittedPicture.Game.Icon.SoundOn : KnittedPicture.Game.Icon.SoundOff)
                .OnClick(() =>
                {
                    app.SetSound(!app.Progress.SoundOn);
                    Close();
                    app.ShowPopup(new SettingsPopup(app));
                });
            AddSecondary("Как играть", KnittedPicture.Game.Icon.Hand).OnClick(() =>
            {
                Close();
                app.ShowIntro();
            });
            AddPrimary("Готово").OnClick(Close);
            var reset = AddLink("Сбросить прогресс");
            reset.OnClick(() =>
            {
                _resetTaps++;
                if (_resetTaps >= 2)
                {
                    app.ResetProgress();
                }
                else
                {
                    app.Toast("Нажми ещё раз, чтобы стереть прогресс");
                }
            });
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
    /// Затор или моль. Спасение — за бустер из запаса, монеты или видео;
    /// всегда можно спокойно начать заново.
    /// </summary>
    public sealed class FailPopup : Popup
    {
        public FailPopup(App app, FailKind kind, bool canRescue, int percent, Action rescue, Action restart)
            : base(app, bottomSheet: true)
        {
            var type = kind == FailKind.Jam ? BoosterType.Slot : BoosterType.Time;
            AddIcon(kind == FailKind.Jam ? KnittedPicture.Game.Icon.Knot : KnittedPicture.Game.Icon.Moth, Palette.Danger, Palette.DangerSoft, 64);
            AddTitle(kind == FailKind.Jam ? "Станок застрял" : "Моль добралась до картинки", 24);
            if (percent >= 50)
            {
                // «Почти!» мотивирует попробовать ещё раз сильнее лёгкой победы.
                var near = AddText($"Довязано {percent}% — почти!", 18, 1);
                near.color = Palette.Danger;
            }

            AddText(kind == FailKind.Jam
                ? canRescue ? "Слоты заняты, а нужного цвета на станке нет" : "Станок заполнен до предела — попробуй ещё раз"
                : "Время вышло. Отгони моль и довяжи картинку", 16, 1.6f);

            if (canRescue)
            {
                var stock = app.Progress.Stock(type);
                var title = stock > 0
                    ? $"{BoosterInfo.Name(type)} · есть {stock}"
                    : $"{BoosterInfo.Name(type)} · {Economy.Price(type)} монет";
                AddPrimary(title).OnClick(() =>
                {
                    if (app.Progress.Pay(type) == Payment.NotEnough)
                    {
                        app.Toast("Не хватает монет — можно за видео");
                        return;
                    }

                    app.Save();
                    Sfx.Play(Sound.Coin);
                    Close();
                    rescue();
                });
                AddSecondary(BoosterInfo.Name(type) + " за видео", KnittedPicture.Game.Icon.Video).OnClick(() =>
                {
                    Close();
                    app.Ads.ShowRewarded("rescue_" + type, ok =>
                    {
                        if (ok)
                        {
                            rescue();
                        }
                        else
                        {
                            app.ShowPopup(new FailPopup(app, kind, canRescue, percent, rescue, restart));
                        }
                    });
                });
            }

            AddLink("Начать заново", KnittedPicture.Game.Icon.Restart).OnClick(() =>
            {
                Close();
                restart();
            });
            AddLink("На главную", KnittedPicture.Game.Icon.Home).OnClick(() =>
            {
                app.Progress.BreakStreak();
                app.ShowHome();
            });
            Finish(4);
        }

        public override bool Closable => false;

        protected override bool CloseOnDim => false;
    }
}
