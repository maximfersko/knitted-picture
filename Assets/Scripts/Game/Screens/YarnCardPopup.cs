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

            // Характеристика: бонус, уровень точками, что даёт сейчас и что будет дальше.
            var shownLevel = owned ? level : 1;
            var power = YarnCatalog.Power(yarnId, shownLevel);
            var box = Ui.Box(Card, "Perk", 18, active ? Palette.PrimarySoft : Palette.Surface).rectTransform;
            var boxY = Cursor - 4f;
            var nextText = !owned
                ? "Найди за новую картинку, в сундуке или в подарке дня"
                : level < YarnCatalog.MaxLevel
                    ? $"Ур. {level + 1}: {YarnCatalog.PerkText(perk, YarnCatalog.Power(yarnId, level + 1))}"
                    : $"Максимальный уровень. Повтор даст {YarnCatalog.DuplicateCoins(yarn.Rarity)} монет";
            const float boxH = 112f;
            Ui.Place(box, 24, boxY, Inner, boxH);
            Ui.Outline(box, 18, active ? Palette.Primary : Palette.Line, active ? 2f : 1.5f);
            var iconBack = Ui.Image(box, "IconBack", Art.Circle, PerkSoft(perk));
            Ui.PlaceCenter(iconBack.rectTransform, 26, 26, 34, 34);
            PerkIcon(box, perk, 26, 26);
            Ui.Fit(Ui.Label(box, YarnCatalog.PerkTitle(perk), 15, Palette.Ink, 50, 12, Inner - 50 - 74, 28, true, TextAnchor.MiddleLeft), 11);
            for (var i = 0; i < YarnCatalog.MaxLevel; i++)
            {
                var dot = Ui.Image(box, "Level", Art.Circle, i < level ? Palette.Primary : Palette.SlotEmpty);
                Ui.PlaceCenter(dot.rectTransform, Inner - 50 + i * 14f, 20, 10, 10);
            }

            Ui.Label(box, owned ? $"ур. {level}" : "ур. —", 11, Palette.Ink2, Inner - 66, 28, 56, 16, true);
            var now = Ui.Label(box, (owned ? "" : "При находке: ") + YarnCatalog.PerkText(perk, power), 14,
                owned ? Palette.Ink : Palette.Ink2, 14, 46, Inner - 28, 34, owned, TextAnchor.MiddleLeft);
            now.lineSpacing = 0.9f;
            Ui.Fit(now, 10);
            Ui.Fit(Ui.Label(box, nextText, 12, Palette.Ink3, 14, 82, Inner - 28, 22, false, TextAnchor.MiddleLeft), 9);
            Space(boxH + 12f);

            if (owned && !active)
            {
                AddText("В работе один моток: его бонус действует в каждом уровне", 12, 1);
                AddPrimary("Взять в работу", 54).OnClick(() =>
                {
                    progress.SetActiveYarn(yarnId);
                    app.Save();
                    Sfx.Play(Sound.Unlock, 0.7f);
                    Closed = changed;
                    Close();
                    app.Toast($"«{yarn.Name}» в работе: {YarnCatalog.PerkText(perk, power)}");
                });
                AddLink("Закрыть").OnClick(Close);
            }
            else
            {
                AddPrimary("Понятно", 50).OnClick(Close);
            }

            Finish(6);
        }

        private static Color RaritySoft(Rarity rarity) =>
            rarity == Rarity.Common ? Palette.Surface : rarity == Rarity.Rare ? Palette.Hex("#EDE4FF") : Palette.CoinSoft;

        private static Color PerkSoft(YarnPerk perk) =>
            perk == YarnPerk.Coins || perk == YarnPerk.Gold ? Palette.CoinSoft : perk == YarnPerk.Combo ? Palette.Hex("#EDE4FF") : Palette.Hex("#FFE6CC");

        /// <summary>Значок бонуса: монета — копилка, часы — ровный ход, звезда — золото, молния — разгон.</summary>
        public static void PerkIcon(RectTransform parent, YarnPerk perk, float cx, float cy, float size = 20f)
        {
            switch (perk)
            {
                case YarnPerk.Coins:
                    Ui.Coin(parent, cx, cy, size);
                    break;
                case YarnPerk.Combo:
                    Ui.IconImage(parent, Icon.Clock, Palette.Link, cx, cy, size);
                    break;
                case YarnPerk.Gold:
                    Ui.IconImage(parent, Icon.Star, Palette.CoinShadow, cx, cy, size);
                    break;
                default:
                    Ui.IconImage(parent, Icon.Bolt, Palette.Hex("#FF9A3C"), cx, cy, size);
                    break;
            }
        }
    }
}
