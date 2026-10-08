#nullable enable

using KnittedPicture.Core;
using UnityEngine;

namespace KnittedPicture.Game
{
    /// <summary>Главные разделы вне уровней.</summary>
    public enum MainTab
    {
        Album,
        Home,
        Yarn,
    }

    /// <summary>
    /// Нижняя панель вкладок: «Альбом», «Главная», «Пряжа». Так устроены и iOS (tab bar), и Android
    /// (bottom navigation): 3 раздела, значок и подпись, активная вкладка выделена, ячейки не меньше 48 dp
    /// по высоте — палец попадает с первого раза. Вкладки — только переходы, не действия.
    /// Фон уходит под системную полоску жеста внизу экрана, а сами кнопки остаются в безопасной зоне.
    /// Красная точка — в разделе ждёт награда.
    /// </summary>
    public static class TabBar
    {
        public const float Height = 64f;

        public static void Create(App app, RectTransform root, float w, float h, MainTab active)
        {
            var bar = Ui.Place(Ui.Node(root, "TabBar"), 0, h - Height, w, Height + app.BottomInset);
            var back = Ui.Image(bar, "Back", null, Palette.Card, true);
            Ui.Stretch(back.rectTransform);
            var line = Ui.Image(bar, "Line", null, Palette.Line);
            Ui.Place(line.rectTransform, 0, 0, w, 1.5f);

            var progress = app.Progress;
            var albumBadge = CollectionsScreen.PendingCount(progress, CollectionsTab.Album) + CollectionsScreen.PendingCount(progress, CollectionsTab.Achievements);
            var yarnBadge = CollectionsScreen.PendingCount(progress, CollectionsTab.Yarn);
            var cellW = w / 3f;
            Item(bar, 0, cellW, MainTab.Album, active, "Альбом", albumBadge, () => app.ShowCollections(CollectionsTab.Album));
            Item(bar, 1, cellW, MainTab.Home, active, "Главная", 0, app.ShowHome);
            Item(bar, 2, cellW, MainTab.Yarn, active, "Пряжа", yarnBadge, () => app.ShowCollections(CollectionsTab.Yarn));
        }

        private static void Item(RectTransform bar, int index, float cellW, MainTab tab, MainTab active, string title, int badge, System.Action open)
        {
            var on = tab == active;
            var color = on ? Palette.Primary : Palette.Ink2;
            var cell = Ui.Image(bar, "Tab" + tab, null, Color.clear, true);
            Ui.Place(cell.rectTransform, index * cellW, 0, cellW, Height);
            var rt = cell.rectTransform;
            if (on)
            {
                // Активная вкладка — мягкая «пилюля» под значком (как в Material 3 и iOS 26).
                var pill = Ui.Box(rt, "Active", 15, Palette.WithAlpha(Palette.Primary, 0.12f));
                Ui.PlaceCenter(pill.rectTransform, cellW / 2f, 22, 64, 30);
            }

            if (tab == MainTab.Yarn)
            {
                var ball = Ui.Image(rt, "Icon", Art.YarnBall, color);
                Ui.PlaceCenter(ball.rectTransform, cellW / 2f, 22, 24, 24);
            }
            else
            {
                Ui.IconImage(rt, tab == MainTab.Album ? Icon.Album : Icon.Home, color, cellW / 2f, 22, 22);
            }

            Ui.Fit(Ui.Label(rt, title, 12, color, 2, 40, cellW - 4, 18, on), 10);
            if (badge > 0)
            {
                Ui.Badge(rt, cellW / 2f + 22f, 6, badge);
            }

            var tap = cell.gameObject.AddComponent<Tap>();
            tap.OnClick(() =>
            {
                if (!on)
                {
                    open();
                }
            });
        }
    }
}
