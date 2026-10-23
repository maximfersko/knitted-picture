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

        /// <summary>Какая вкладка была активна на прошлой панели: метка переезжает оттуда.</summary>
        private static MainTab? _last;

        public static void Create(App app, RectTransform root, float w, float h, MainTab active)
        {
            // Свой Canvas: при перелистывании панель стоит, пока экран под ней едет, — без пересборки экрана.
            var bar = Ui.Layer(Ui.Place(Ui.Node(root, "TabBar"), 0, h - Height, w, Height + app.BottomInset), true);
            var back = Ui.Image(bar, "Back", null, Palette.Card, true);
            Ui.Stretch(back.rectTransform);
            var line = Ui.Image(bar, "Line", null, Palette.Line);
            Ui.Place(line.rectTransform, 0, 0, w, 1.5f);

            var progress = app.Progress;
            var albumBadge = CollectionsScreen.PendingCount(progress, CollectionsTab.Album) + CollectionsScreen.PendingCount(progress, CollectionsTab.Achievements);
            var yarnBadge = CollectionsScreen.PendingCount(progress, CollectionsTab.Yarn);
            var cellW = w / 3f;
            Item(app, bar, 0, cellW, MainTab.Album, active, L.T("Альбом"), albumBadge, () => app.ShowCollections(CollectionsTab.Album));
            Item(app, bar, 1, cellW, MainTab.Home, active, L.T("Главная"), 0, app.ShowHome);
            Item(app, bar, 2, cellW, MainTab.Yarn, active, L.T("Пряжа"), yarnBadge, () => app.ShowCollections(CollectionsTab.Yarn));
            _last = active;
        }

        /// <summary>
        /// Свайп между разделами: соседняя вкладка слева (direction = -1) или справа (+1) от текущей,
        /// с тем же въездом, что и по тапу на вкладку. За краем (левее альбома, правее пряжи) — ничего.
        /// </summary>
        public static void Go(App app, MainTab from, int direction)
        {
            var to = (int)from + direction;
            if (direction == 0 || to < (int)MainTab.Album || to > (int)MainTab.Yarn || app.HasPopups)
            {
                return;
            }

            app.SlideNext(direction);
            switch ((MainTab)to)
            {
                case MainTab.Album:
                    app.ShowCollections(CollectionsTab.Album);
                    break;
                case MainTab.Yarn:
                    app.ShowCollections(CollectionsTab.Yarn);
                    break;
                default:
                    app.ShowHome();
                    break;
            }
        }

        private static void Item(App app, RectTransform bar, int index, float cellW, MainTab tab, MainTab active, string title, int badge, System.Action open)
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

                // Пришли с другой вкладки: метка переезжает от неё и замедляется к месту, без растяжения и отскока.
                if (app.PendingSlide != 0 && _last != null && _last != tab && !Tweens.Calm)
                {
                    var from = ((int)_last.Value - index) * cellW;
                    Tweens.Offset(pill.rectTransform, new Vector2(from, 0), Vector2.zero, 0.42f, Ease.OutCubic);
                }
            }

            // Объёмные значки: книга-альбом, домик, клубок. Неактивные приглушены серым, активный — в полном цвете
            // (значок при переходе не прыгает: активную вкладку выдают цвет, жирная подпись и пилюля).
            var art = tab == MainTab.Yarn ? Art2.YarnBall(Palette.Link) : tab == MainTab.Album ? Art2.AlbumBook : Art2.House;
            var icon = Ui.Image(rt, "Icon", art, on ? Color.white : new Color(0.78f, 0.81f, 0.86f, 0.85f));
            Ui.PlaceCenter(icon.rectTransform, cellW / 2f, 22, 28, 28);

            Ui.Fit(Ui.Label(rt, title, 12, color, 2, 40, cellW - 4, 18, on), 10);
            if (badge > 0)
            {
                Ui.Badge(rt, cellW / 2f + 18f, 13, badge, app.PendingSlide == 0);
            }

            var tap = cell.gameObject.AddComponent<Tap>();
            tap.OnClick(() =>
            {
                if (!on)
                {
                    app.SlideNext(tab > active ? 1 : -1);
                    open();
                }
            });
        }
    }
}
