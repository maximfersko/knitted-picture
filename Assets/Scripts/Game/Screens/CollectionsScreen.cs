#nullable enable

using KnittedPicture.Core;
using UnityEngine;

namespace KnittedPicture.Game
{
    public enum CollectionsTab
    {
        Album,
        Yarn,
        Achievements,
    }

    /// <summary>
    /// Коллекции: альбом картинок, «Корзинка» редкой пряжи и достижения.
    /// На каждой вкладке — награды, которые можно забрать; красные точки показывают, где они ждут.
    /// </summary>
    public sealed partial class CollectionsScreen : ScreenView
    {
        private const float TabsY = 62f;

        /// <summary>Страницы этого раздела: «Альбом» — картинки и достижения, «Пряжа» — одна корзинка.</summary>
        private readonly CollectionsTab[] _pages;
        private readonly string[] _titles;
        private readonly float _contentY;

        private readonly CollectionsTab _tab;
        private readonly UnityEngine.UI.ScrollRect[] _scrolls;
        private readonly UnityEngine.UI.Text[] _tabLabels;
        private RectTransform _strip = null!;
        private RectTransform _indicator = null!;
        private RectTransform _indicatorShadow = null!;
        private float _tabW;
        private float _pos;
        private float _speed;
        private int _target;
        private bool _dragging;
        private int _axis;

        public CollectionsScreen(App app, CollectionsTab tab)
            : base(app, "Collections")
        {
            _tab = tab;

            // Раздел нижней панели: «Пряжа» — одна страница; «Альбом» — картинки и достижения переключателем.
            var yarn = tab == CollectionsTab.Yarn;
            _pages = yarn ? new[] { CollectionsTab.Yarn } : new[] { CollectionsTab.Album, CollectionsTab.Achievements };
            _titles = yarn ? new[] { L.T("Пряжа") } : new[] { L.T("Картинки"), L.T("Достижения") };
            _contentY = _pages.Length > 1 ? 116f : 62f;
            _scrolls = new UnityEngine.UI.ScrollRect[_pages.Length];
            _tabLabels = new UnityEngine.UI.Text[_pages.Length];
            _target = Mathf.Max(0, System.Array.IndexOf(_pages, tab));
            _pos = _target;

            // Крупный заголовок раздела слева, как на верхних экранах iOS и Android; «назад» не нужен — есть вкладки.
            Ui.Label(Root, yarn ? L.T("Пряжа") : L.T("Альбом"), 28, Palette.Ink, 20, 8, W - 160, 44, true, TextAnchor.MiddleLeft);
            Ui.CoinPill(Root, app.Progress.Coins, W - 16, 8);
            if (_pages.Length > 1)
            {
                BuildTabs();
            }

            BuildPages();
            Apply();
            TabBar.Create(app, Root, W, H, yarn ? MainTab.Yarn : MainTab.Album);
        }

        public override void Tick(float dt)
        {
            if (!_dragging)
            {
                // Пружина: бросок пальцем переходит в движение, у страницы — мягкий перелёт и успокоение.
                Springs.Step(ref _pos, ref _speed, _target, dt);
                if (Mathf.Abs(_pos - _target) < 0.0005f && Mathf.Abs(_speed) < 0.01f)
                {
                    _pos = _target;
                    _speed = 0f;
                }
            }

            // Карусель стоит — ничего не двигаем: иначе экран пересобирался бы каждый кадр впустую.
            if (_dragging || _pos != _appliedPos)
            {
                Apply();
            }
        }

        /// <summary>
        /// Три вкладки — лентой рядом, как карусель на главной: лента едет за пальцем и плавно
        /// доводится до ближайшей вкладки, на крайних — туго, без зацикливания.
        /// </summary>
        private void BuildPages()
        {
            var h = H - _contentY - TabBar.Height;
            var viewport = Ui.Place(Ui.Node(Root, "Pages"), 0, _contentY, W, h);
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            _strip = Ui.Place(Ui.Node(viewport, "Strip"), 0, 0, W * _titles.Length, h);
            for (var i = 0; i < _titles.Length; i++)
            {
                var content = Ui.Scroll(_strip, i * W, 0, W, h);
                var page = (RectTransform)content.parent;
                _scrolls[i] = page.GetComponent<UnityEngine.UI.ScrollRect>();
                float height;
                switch (_pages[i])
                {
                    case CollectionsTab.Yarn:
                        height = BuildYarn(content);
                        break;
                    case CollectionsTab.Achievements:
                        height = BuildAchievements(content);
                        break;
                    default:
                        height = BuildAlbum(content);
                        break;
                }

                content.sizeDelta = new Vector2(W, Mathf.Max(height + 24f, h));

                // Пейджер и на единственной странице («Пряжа»): горизонтальный жест за край ведёт в соседний раздел.
                AttachPager(page, i);
            }
        }

        /// <summary>
        /// Pager стоит на том же объекте, что и вертикальная прокрутка страницы, и получает те же события.
        /// Первые точки жеста решают ось: горизонтальный жест листает вкладки и замораживает прокрутку.
        /// </summary>
        private void AttachPager(RectTransform page, int index)
        {
            var pager = page.gameObject.AddComponent<Pager>();
            var last = _titles.Length - 1;
            pager.Began += () =>
            {
                _axis = 0;
                _speed = 0f;
            };
            pager.Moved += dx =>
            {
                if (_axis == 0 && pager.Total.magnitude > 10f)
                {
                    _axis = Mathf.Abs(pager.Total.x) > Mathf.Abs(pager.Total.y) ? 1 : 2;
                    if (_axis == 1)
                    {
                        _dragging = true;
                        _scrolls[index].vertical = false;
                    }
                }

                if (_axis != 1)
                {
                    return;
                }

                var outside = _pos < 0f || _pos > last;
                _pos -= dx / W * (outside ? 0.3f : 1f);
            };
            pager.Released += velocity =>
            {
                _scrolls[index].vertical = true;
                if (_axis != 1)
                {
                    return;
                }

                _dragging = false;

                // Протянули за край ленты (или бросили к краю, стоя на крайней вкладке) — соседний раздел нижней
                // панели: за «Достижениями» и за «Пряжей» — главная. Лента сначала пружинит, потом экран уезжает.
                var beyond = _pos > last + 0.08f || (_pos >= last - 0.02f && velocity < -500f) ? 1
                    : _pos < -0.08f || (_pos <= 0.02f && velocity > 500f) ? -1 : 0;
                if (beyond != 0)
                {
                    var from = _tab == CollectionsTab.Yarn ? MainTab.Yarn : MainTab.Album;
                    var to = (int)from + beyond;
                    if (to >= (int)MainTab.Album && to <= (int)MainTab.Yarn)
                    {
                        _speed = 0f;
                        TabBar.Go(App, from, beyond);
                        return;
                    }
                }

                // Скорость пальца (точки в секунду) — в скорость ленты (страницы в секунду), с запасом против перелёта на две.
                _speed = Mathf.Clamp(-velocity / W, -4f, 4f);
                var next = Mathf.RoundToInt(_pos);
                if (Mathf.Abs(velocity) > 350f)
                {
                    next = velocity < 0 ? Mathf.FloorToInt(_pos) + 1 : Mathf.CeilToInt(_pos) - 1;
                }

                Turn(Mathf.Clamp(next, 0, last));
            };
        }

        private void Turn(int index)
        {
            index = Mathf.Clamp(index, 0, _titles.Length - 1);
            if (index != _target)
            {
                Sfx.Play(Sound.Pick, 0.5f);
            }

            _target = index;
        }

        /// <summary>Лента, «перетекающая» плашка активной вкладки и цвет подписей — по текущей позиции.</summary>
        /// <summary>Положение ленты, которое уже показано на экране.</summary>
        private float _appliedPos = float.NaN;

        private void Apply()
        {
            _appliedPos = _pos;
            _strip.anchoredPosition = new Vector2(-_pos * W, _strip.anchoredPosition.y);
            if (_pages.Length < 2)
            {
                return;
            }

            // Перетекание: передний край плашки уходит к соседней вкладке раньше, задний догоняет позже.
            var p = Mathf.Clamp(_pos, 0f, _titles.Length - 1);
            var i0 = Mathf.Min(Mathf.FloorToInt(p), _titles.Length - 2);
            var t = p - i0;
            var x0 = 20f + i0 * _tabW;
            var x1 = 20f + (i0 + 1) * _tabW;
            var head = Mathf.Lerp(x0, x1, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.6f)));
            var tail = Mathf.Lerp(x0, x1, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.4f) / 0.6f)));
            var left = Mathf.Min(head, tail);
            var width = Mathf.Max(head, tail) - left + _tabW;
            _indicator.anchoredPosition = new Vector2(left, -(TabsY + 4));
            _indicator.sizeDelta = new Vector2(width, 36);
            _indicatorShadow.anchoredPosition = new Vector2(left, -(TabsY + 6));
            _indicatorShadow.sizeDelta = new Vector2(width, 36);

            for (var i = 0; i < _tabLabels.Length; i++)
            {
                var near = 1f - Mathf.Clamp01(Mathf.Abs(i - _pos));
                _tabLabels[i].color = Color.Lerp(Palette.Ink2, Palette.Ink, near);
            }
        }

        private void BuildTabs()
        {
            // Подложка вкладок темнее фона, активная вкладка — белая плашка с тенью, она перетекает при листании.
            var track = Ui.Box(Root, "Tabs", 22, Palette.LoomBg);
            Ui.Place(track.rectTransform, 16, TabsY, W - 32, 44);
            Ui.Outline(track.rectTransform, 22, Palette.Line, 1.5f);
            _tabW = (W - 32 - 8) / _titles.Length;
            _indicatorShadow = Ui.Box(Root, "TabShadow", 18, Palette.CardShadow).rectTransform;
            Ui.Place(_indicatorShadow, 20, TabsY + 6, _tabW, 36);
            _indicator = Ui.Box(Root, "TabActive", 18, Palette.Card).rectTransform;
            Ui.Place(_indicator, 20, TabsY + 4, _tabW, 36);

            for (var i = 0; i < _titles.Length; i++)
            {
                var x = 20f + i * _tabW;
                var face = Ui.Box(Root, "Tab" + i, 18, Color.clear, true);
                Ui.Place(face.rectTransform, x, TabsY + 4, _tabW, 36);
                // С бейджем подпись сдвигается влево и ужимается: точка не закрывает «Достижения» на узком экране.
                var pending = Pending(_pages[i]);
                var labelW = pending > 0 ? _tabW - 22f : _tabW;
                _tabLabels[i] = Ui.Fit(Ui.Label(face.rectTransform, _titles[i], 15, Palette.Ink2, 0, 0, labelW, 36), 11);
                if (pending > 0)
                {
                    Ui.Badge(face.rectTransform, _tabW - 12, 6, pending, App.PendingSlide == 0);
                }

                var index = i;
                var tap = face.gameObject.AddComponent<Tap>();
                tap.PressFeedback = false;
                tap.OnClick(() => Turn(index));
            }
        }

        private int Pending(CollectionsTab tab) => PendingCount(App.Progress, tab);

        /// <summary>Сколько наград ждёт в разделе — для красных точек на вкладках.</summary>
        public static int PendingCount(ProgressService progress, CollectionsTab tab)
        {
            var count = 0;
            switch (tab)
            {
                case CollectionsTab.Album:
                    for (var i = 0; i < Collections.All.Length; i++)
                    {
                        count += progress.CanClaimCollection(i) ? 1 : 0;
                    }

                    break;
                case CollectionsTab.Yarn:
                    foreach (var set in YarnCatalog.Sets)
                    {
                        count += progress.CanClaimSet(set) ? 1 : 0;
                    }

                    break;
                default:
                    foreach (var a in Achievements.All)
                    {
                        count += progress.CanClaimAchievement(a) ? 1 : 0;
                    }

                    break;
            }

            return count;
        }

        // ---------- Общее ----------

        private void Claim(Granted? granted, string title, Icon icon, CollectionsTab tab, Sprite? art = null)
        {
            if (granted == null)
            {
                return;
            }

            App.Save();
            App.ShowPopup(new RewardPopup(App, title, icon, granted.Value, () => App.ShowCollections(tab), art));
        }
    }

    /// <summary>
    /// Полоска награды внизу карточки: прогресс с подписью, «Забрать» (золотая рамка) или «Награда получена».
    /// Одна на главной, в альбоме и в пряже.
    /// </summary>
    public static class RewardStrip
    {
        public static void Build(RectTransform parent, float x, float y, float w, string text, float fraction, bool canClaim, bool claimed,
            string readyText, System.Action claim)
        {
            var bar = Ui.Box(parent, "Reward", 16, canClaim ? Palette.CoinSoft : Palette.Surface).rectTransform;
            Ui.Place(bar, x, y, w, 48);
            Ui.Outline(bar, 16, canClaim ? Palette.Coin : Palette.Line, canClaim ? 2f : 1.5f);
            var iconBack = Ui.Image(bar, "IconBack", Art.Circle, claimed ? Palette.PrimarySoft : Palette.CoinSoft);
            Ui.PlaceCenter(iconBack.rectTransform, 26, 24, 34, 34);
            if (claimed)
            {
                Ui.IconImage(bar, Icon.Check, Palette.Primary, 26, 24, 18);
            }
            else
            {
                // Подарок-иллюстрация; когда можно забрать — потряхивается, будто внутри что-то есть.
                var gift = Ui.Image(bar, "Gift", Art2.Gift, Color.white);
                var giftRt = gift.rectTransform;
                Ui.PlaceCenter(giftRt, 26, 23, 30, 30);
                if (canClaim)
                {
                    Tweens.Run(giftRt, 2f, t =>
                    {
                        var shake = t < 0.4f ? Ease.Jolt(t / 0.4f) : 0f;
                        giftRt.localRotation = Quaternion.Euler(0f, 0f, 12f * shake);
                        giftRt.localScale = Vector3.one * (1f + 0.08f * Mathf.Abs(shake));
                    }, null, null, 0f, true);
                }
            }
            if (canClaim)
            {
                Ui.Fit(Ui.Label(bar, readyText, 14, Palette.Ink, 50, 12, w - 160, 24, true, TextAnchor.MiddleLeft), 10);
                var claimButton = Ui.Button(bar, L.T("Забрать"), w - 100, 6, 92, 36, Palette.Primary, Palette.PrimaryShadow, Color.white, 15);
                claimButton.OnClick(claim);
                Ui.Sheen((RectTransform)claimButton.transform, 92f, 36f, 3.2f, 0.6f);
            }
            else if (claimed)
            {
                Ui.Label(bar, L.T("Награда получена"), 14, Palette.Ink2, 50, 0, w - 60, 48, true, TextAnchor.MiddleLeft);
            }
            else
            {
                Ui.Fit(Ui.Label(bar, text, 12, Palette.Ink2, 50, 6, w - 60, 18, false, TextAnchor.MiddleLeft), 9);
                Ui.ProgressBar(bar, 50, 30, w - 64, 7, fraction, Palette.Primary);
            }
        }
    }

    /// <summary>Награда за коллекцию альбома: полоска прогресса и «Забрать» с окном награды.</summary>
    public static class CollectionReward
    {
        public static void Build(App app, RectTransform parent, int index, float x, float y, float w, System.Action refresh)
        {
            var progress = app.Progress;
            var collection = Collections.All[index];
            var done = 0;
            for (var id = collection.FirstLevel; id <= collection.LastLevel; id++)
            {
                done += progress.IsCompleted(id) ? 1 : 0;
            }

            RewardStrip.Build(parent, x, y, w, L.F("Собери все {0}: монеты и моток", collection.Count), (float)done / collection.Count,
                progress.CanClaimCollection(index), progress.IsCollectionClaimed(index), L.T("Коллекция собрана!"), () => Claim(app, index, refresh));
        }

        /// <summary>Забрать награду коллекции и показать её; refresh — перестроить экран после окна.</summary>
        public static void Claim(App app, int index, System.Action refresh)
        {
            var granted = app.Progress.ClaimCollection(index, app.Random);
            app.Save();
            if (granted != null)
            {
                app.ShowPopup(new RewardPopup(app, Collections.All[index].Name, Icon.Album, granted.Value, refresh, Art2.Medal(BadgeArt.Shelf, true)));
            }
        }
    }
}
