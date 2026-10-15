#nullable enable

using KnittedPicture.Core;
using UnityEngine;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Карточка мотка: как выглядит, откуда (набор, редкость), и главное — характеристика:
    /// какой бонус даёт моток «в работе», его уровень (растёт от повторов) и что будет на следующем.
    /// Ненайденный моток показывает набор, редкость и бонус — понятно, за чем охотиться.
    /// </summary>
    public sealed class YarnCardPopup : Popup
    {
        public YarnCardPopup(App app, int yarnId, System.Action changed)
            : base(app)
        {
            var progress = app.Progress;
            var yarn = YarnCatalog.All[yarnId];
            var owned = progress.HasYarn(yarnId);
            var level = progress.YarnLevel(yarnId);
            var active = progress.ActiveYarn == yarnId;
            var perk = YarnCatalog.PerkOf(yarnId);
            var set = YarnCatalog.SetOf(yarnId);
            var rarityColor = YarnView.RarityColor(yarn.Rarity);

            // Моток на мягком круге цвета редкости.
            const float ball = 92f;
            var back = Ui.Image(Card, "Back", Art.Circle, RaritySoft(yarn.Rarity));
            Ui.PlaceCenter(back.rectTransform, CardWidth / 2f, Cursor + ball / 2f + 4f, ball + 28f, ball + 28f);
            if (owned)
            {
                YarnView.Aura(Card, yarn.Rarity, CardWidth / 2f, Cursor + ball / 2f + 4f, ball);
            }

            var skein = YarnView.Create(Card, yarn, CardWidth / 2f, Cursor + ball / 2f + 4f, ball, owned);
            Tweens.Scale(skein, 0.6f, 1f, 0.35f, Ease.OutBack);
            if (active)
            {
                // «В работе» — значок по центру под мотком, на нижнем краю круга: в одну строку, ширина по тексту,
                // не заходит ни на клубок, ни на название.
                var text = Ui.Label(Card, L.T("в работе"), 12, Color.white, true, TextAnchor.MiddleLeft);
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                var chipW = Mathf.Ceil(text.preferredWidth) + 38f;
                var chipY = Cursor + ball + 18f - 12f;
                var chip = Ui.Card(Card, "Active", CardWidth / 2f - chipW / 2f, chipY, chipW, 24, 12, Palette.Primary, Palette.PrimaryShadow, 2f);
                Ui.IconImage(chip, Icon.Check, Color.white, 15, 12, 12);
                text.rectTransform.SetParent(chip, false);
                Ui.Place(text.rectTransform, 26, 0, chipW - 32, 24);
            }

            // Под кругом с мотком (и значком «в работе») — воздух до названия.
            Space(ball + (active ? 48f : 34f));
            Ui.Fit(AddTitle(owned ? yarn.Name : L.T("Ещё не найдена"), 22), 15);
            var sub = AddText(L.F("Набор «{0}» · {1}", set.Name, YarnCatalog.RarityName(yarn.Rarity)), 13, 1);
            sub.color = rarityColor;
            if (owned)
            {
                AddText(yarn.Description, 14, 1);
            }

            // Раздел «Бонус»: что делает моток — одним понятным предложением — и работает ли он сейчас.
            var open = progress.PerksOpen;
            var power = YarnCatalog.Power(yarnId, owned ? level : 1);
            var (status, statusBack, statusInk) = !owned ? (L.T("не найден"), Palette.Track, Palette.Ink3)
                : !open ? (L.F("с ур. {0}", YarnCatalog.PerksFromLevel), Palette.Track, Palette.Ink3)
                : active ? (L.T("работает"), Palette.Primary, Color.white)
                : (L.T("не в работе"), Palette.Track, Palette.Ink2);
            // Пояснение только там, где без него не понять: где взять ненайденный моток.
            // Остальное и так видно по статусу и кнопке «Взять в работу».
            var hint = !owned ? yarn.Seasonal ? L.T("Только за финал «Вязального сезона» своего времени года") : L.T("Выпадает за новые картинки, в сундуке и в подарке дня") : null;
            var working = owned && open && active;
            BeginSection(L.T("Бонус"), active && open);
            var iconBack = Ui.Image(_box!, "IconBack", Art.Circle, PerkSoft(perk));
            Ui.PlaceCenter(iconBack.rectTransform, 30, _boxY + 14f, 32, 32);
            PerkIcon(_box!, perk, 30, _boxY + 14f, 18);
            // Статус справа — плашка по ширине текста; название бонуса занимает остаток строки с зазором 10,
            // а если не влезает — ужимается, но на плашку не заходит.
            var statusText = Ui.Label(_box!, status, 11, statusInk, true, TextAnchor.MiddleCenter);
            statusText.horizontalOverflow = HorizontalWrapMode.Overflow;
            var pillW = Mathf.Min(Mathf.Ceil(statusText.preferredWidth) + 22f, Inner * 0.42f);
            var pill = Ui.Box(_box!, "Status", 11, statusBack).rectTransform;
            Ui.Place(pill, Inner - 14 - pillW, _boxY + 3f, pillW, 22);
            statusText.rectTransform.SetParent(pill, false);
            statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            Ui.Fit(statusText, 8);
            Ui.Place(statusText.rectTransform, 6, 0, pillW - 12, 22);
            var titleW = Inner - 14 - pillW - 10f - 54f;
            Ui.Fit(Ui.Label(_box!, YarnCatalog.PerkTitle(perk), 16, Palette.Ink, 54, _boxY, titleW, 28, true, TextAnchor.MiddleLeft), 11);
            _boxY += 36f;
            SectionText(YarnCatalog.PerkSentence(perk, power), 14, working ? Palette.Ink : Palette.Ink2, true);
            if (hint != null)
            {
                SectionText(hint, 12, Palette.Ink3, false);
            }
            EndSection();

            // Раздел «Уровень мотка»: сколько собрано, как поднять и что даст следующий уровень.
            if (owned)
            {
                BeginSection("", false);
                Ui.Label(_box!, L.F("Уровень {0} из {1}", level, YarnCatalog.MaxLevel), 14, Palette.Ink, 14, _boxY, 140, 22, true, TextAnchor.MiddleLeft);
                const float segW = 34f;
                for (var i = 0; i < YarnCatalog.MaxLevel; i++)
                {
                    var seg = Ui.Box(_box!, "Level", 4, i < level ? Palette.Primary : Palette.Inactive).rectTransform;
                    Ui.Place(seg, Inner - 14 - YarnCatalog.MaxLevel * (segW + 4f) + 4f + i * (segW + 4f), _boxY + 7f, segW, 8);
                }

                _boxY += 28f;

                // На максимальном уровне — путь к золотой версии: пять золотых делений.
                var golden = progress.IsGolden(yarnId);
                if (level >= YarnCatalog.MaxLevel)
                {
                    var gold = progress.GoldProgress(yarnId);
                    Ui.Label(_box!, golden ? L.T("Золотая версия") : L.F("К золоту {0} из {1}", gold, YarnCatalog.GoldenAfter), 13,
                        golden ? Palette.CoinInk : Palette.Ink2, 14, _boxY, 160, 20, true, TextAnchor.MiddleLeft);
                    const float goldW = 18f;
                    for (var i = 0; i < YarnCatalog.GoldenAfter; i++)
                    {
                        var seg = Ui.Box(_box!, "Gold", 4, i < gold ? Palette.Coin : Palette.Inactive).rectTransform;
                        Ui.Place(seg, Inner - 14 - YarnCatalog.GoldenAfter * (goldW + 4f) + 4f + i * (goldW + 4f), _boxY + 6f, goldW, 8);
                    }

                    _boxY += 26f;
                }

                var next = level < YarnCatalog.MaxLevel
                    ? L.F("Ещё такой моток: {0}", YarnCatalog.PerkShort(perk, YarnCatalog.Power(yarnId, level + 1)))
                    : golden
                        ? L.F("Золотой моток: бонус сильнее на 1. Повтор: +{0} монет", YarnCatalog.DuplicateCoins(yarn.Rarity))
                        : L.F("Повторы копят к золотой версии: особый вид и бонус сильнее на 1 (ещё {0})",
                            YarnCatalog.GoldenAfter - progress.GoldProgress(yarnId));
                SectionText(next, 12, Palette.Ink2, false);
                EndSection();
            }

            if (owned && !active && progress.PerksOpen)
            {
                AddPrimary(L.T("Взять в работу"), 54).OnClick(() =>
                {
                    progress.SetActiveYarn(yarnId);
                    app.Save();
                    Sfx.Play(Sound.Unlock, 0.7f);
                    Closed = changed;
                    Close();
                    app.Toast(L.F("«{0}» в работе: {1}", yarn.Name, YarnCatalog.PerkText(perk, power)));
                });
            }

            Finish(18);
        }

        private RectTransform? _box;
        private float _boxY;
        private float _boxTop;

        /// <summary>Начать раздел: подпись над плашкой и сама плашка; высота считается по содержимому.</summary>
        private void BeginSection(string caption, bool highlight)
        {
            if (caption.Length > 0)
            {
                var label = Ui.Label(Card, caption.ToUpperInvariant(), 11, Palette.Ink3, 30, Cursor - 6f, Inner - 12, 16, true, TextAnchor.MiddleLeft);
                label.rectTransform.SetAsLastSibling();
                Space(14f);
            }

            _boxTop = Cursor;
            _box = Ui.Box(Card, "Section", 16, highlight ? Palette.PrimarySoft : Palette.Surface).rectTransform;
            Ui.Outline(_box, 16, highlight ? Palette.Primary : Palette.Line, highlight ? 2f : 1.5f);
            _boxY = 12f;
        }

        /// <summary>Абзац внутри раздела: высота по тексту, ничего не налезает.</summary>
        private void SectionText(string text, float size, Color color, bool bold)
        {
            var label = Ui.Label(_box!, text, size, color, 14, _boxY, Inner - 28, 10, bold, TextAnchor.UpperLeft);
            label.lineSpacing = 0.95f;
            var h = Mathf.Max(size + 4f, label.preferredHeight);
            label.rectTransform.sizeDelta = new Vector2(Inner - 28, h);
            _boxY += h + 6f;
        }

        private void EndSection()
        {
            var h = _boxY + 6f;
            Ui.Place(_box!, 24, _boxTop, Inner, h);
            Space(h + 16f);
        }

        private static Color RaritySoft(Rarity rarity) => rarity == Rarity.Common ? Palette.Surface : RarityStyle.Soft(rarity);

        private static Color PerkSoft(YarnPerk perk) => Palette.CoinSoft;

        /// <summary>Значок бонуса: монета — копилка, звезда — золотые петли.</summary>
        public static void PerkIcon(RectTransform parent, YarnPerk perk, float cx, float cy, float size = 20f)
        {
            if (perk == YarnPerk.Coins)
            {
                Ui.Coin(parent, cx, cy, size);
            }
            else
            {
                Ui.IconImage(parent, Icon.Star, Palette.CoinShadow, cx, cy, size);
            }
        }
    }

    /// <summary>
    /// Один раз, когда бонусы пряжи открываются: что такое моток «в работе», что он даёт сейчас
    /// и как бонус растёт. Коротко, с карточкой текущего мотка.
    /// </summary>
    public sealed class YarnPerksIntroPopup : Popup
    {
        public YarnPerksIntroPopup(App app)
            : base(app)
        {
            var progress = app.Progress;
            var active = progress.ActiveYarn;
            if (active >= 0)
            {
                var yarn = YarnCatalog.All[active];
                var back = Ui.Image(Card, "Back", Art.Circle, Palette.CoinSoft);
                Ui.PlaceCenter(back.rectTransform, CardWidth / 2f, Cursor + 40f, 84, 84);
                var skein = YarnView.Create(Card, yarn, CardWidth / 2f, Cursor + 40f, 64, true);
                Tweens.Scale(skein, 0.5f, 1f, 0.4f, Ease.OutBack);
                Space(94f);
            }
            else
            {
                AddIcon(Icon.Gift, Palette.CoinShadow, Palette.CoinSoft, 72);
            }

            AddTitle(L.T("Бонусы пряжи"), 24);
            AddText(L.T("Моток «в работе» теперь помогает в каждом уровне. Повторы поднимают его уровень, и бонус растёт."), 14, 2);
            if (active >= 0)
            {
                var perk = YarnCatalog.PerkOf(active);
                var text = AddText(L.F("«{0}»: {1}", YarnCatalog.All[active].Name, YarnCatalog.PerkText(perk, YarnCatalog.Power(active, progress.YarnLevel(active)))), 15, 1);
                text.color = Palette.Primary;
            }

            AddText(L.T("Сменить моток можно во вкладке «Пряжа»"), 12, 1);
            AddPrimary(L.T("Понятно")).OnClick(Close);
            Finish(8);
        }
    }
}
