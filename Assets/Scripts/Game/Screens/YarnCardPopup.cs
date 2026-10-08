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
            var skein = YarnView.Create(Card, yarn, CardWidth / 2f, Cursor + ball / 2f + 4f, ball, owned);
            Tweens.Scale(skein, 0.6f, 1f, 0.35f, Ease.OutBack);
            if (active)
            {
                var chip = Ui.Card(Card, "Active", CardWidth / 2f + 26f, Cursor + ball - 6f, 92, 24, 12, Palette.Primary, Palette.PrimaryShadow, 2f);
                Ui.IconImage(chip, Icon.Check, Color.white, 14, 12, 12);
                Ui.Label(chip, "в работе", 12, Color.white, 24, 0, 64, 24, true, TextAnchor.MiddleLeft);
            }

            Space(ball + 30f);
            Ui.Fit(AddTitle(owned ? yarn.Name : "Ещё не найдена", 22), 15);
            var sub = AddText($"Набор «{set.Name}» · {YarnCatalog.RarityName(yarn.Rarity)}", 13, 1);
            sub.color = rarityColor;
            if (owned)
            {
                AddText(yarn.Description, 14, 1);
            }

            // Раздел «Бонус»: что делает моток — одним понятным предложением — и работает ли он сейчас.
            var open = progress.PerksOpen;
            var power = YarnCatalog.Power(yarnId, owned ? level : 1);
            var (status, statusBack, statusInk) = !owned ? ("не найден", Palette.Track, Palette.Ink3)
                : !open ? ($"с ур. {YarnCatalog.PerksFromLevel}", Palette.Track, Palette.Ink3)
                : active ? ("работает", Palette.Primary, Color.white)
                : ("не в работе", Palette.Track, Palette.Ink2);
            // Пояснение только там, где без него не понять: где взять ненайденный моток.
            // Остальное и так видно по статусу и кнопке «Взять в работу».
            var hint = !owned ? "Выпадает за новые картинки, в сундуке и в подарке дня" : null;
            var working = owned && open && active;
            BeginSection("Бонус", active && open);
            var iconBack = Ui.Image(_box!, "IconBack", Art.Circle, PerkSoft(perk));
            Ui.PlaceCenter(iconBack.rectTransform, 30, _boxY + 14f, 32, 32);
            PerkIcon(_box!, perk, 30, _boxY + 14f, 18);
            Ui.Fit(Ui.Label(_box!, YarnCatalog.PerkTitle(perk), 16, Palette.Ink, 54, _boxY, Inner - 54 - 104, 28, true, TextAnchor.MiddleLeft), 11);
            var pill = Ui.Box(_box!, "Status", 11, statusBack).rectTransform;
            Ui.Place(pill, Inner - 14 - 94, _boxY + 3f, 94, 22);
            Ui.Fit(Ui.Label(pill, status, 11, statusInk, 4, 0, 86, 22), 8);
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
                Ui.Label(_box!, $"Уровень {level} из {YarnCatalog.MaxLevel}", 14, Palette.Ink, 14, _boxY, 140, 22, true, TextAnchor.MiddleLeft);
                const float segW = 34f;
                for (var i = 0; i < YarnCatalog.MaxLevel; i++)
                {
                    var seg = Ui.Box(_box!, "Level", 4, i < level ? Palette.Primary : Palette.Inactive).rectTransform;
                    Ui.Place(seg, Inner - 14 - YarnCatalog.MaxLevel * (segW + 4f) + 4f + i * (segW + 4f), _boxY + 7f, segW, 8);
                }

                _boxY += 28f;
                var next = level < YarnCatalog.MaxLevel
                    ? $"Ещё такой моток: {YarnCatalog.PerkShort(perk, YarnCatalog.Power(yarnId, level + 1))}"
                    : $"Повтор: +{YarnCatalog.DuplicateCoins(yarn.Rarity)} монет";
                SectionText(next, 12, Palette.Ink2, false);
                EndSection();
            }

            if (owned && !active && progress.PerksOpen)
            {
                AddPrimary("Взять в работу", 54).OnClick(() =>
                {
                    progress.SetActiveYarn(yarnId);
                    app.Save();
                    Sfx.Play(Sound.Unlock, 0.7f);
                    Closed = changed;
                    Close();
                    app.Toast($"«{yarn.Name}» в работе: {YarnCatalog.PerkText(perk, power)}");
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

        private static Color RaritySoft(Rarity rarity) =>
            rarity == Rarity.Common ? Palette.Surface : rarity == Rarity.Rare ? Palette.Hex("#EDE4FF") : Palette.CoinSoft;

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

            AddTitle("Бонусы пряжи", 24);
            AddText("Моток «в работе» теперь помогает в каждом уровне. Повторы поднимают его уровень, и бонус растёт.", 14, 2);
            if (active >= 0)
            {
                var perk = YarnCatalog.PerkOf(active);
                var text = AddText($"«{YarnCatalog.All[active].Name}»: {YarnCatalog.PerkText(perk, YarnCatalog.Power(active, progress.YarnLevel(active)))}", 15, 1);
                text.color = Palette.Primary;
            }

            AddText("Сменить моток можно во вкладке «Пряжа»", 12, 1);
            AddPrimary("Понятно").OnClick(Close);
            Finish(8);
        }
    }
}
