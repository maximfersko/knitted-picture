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
    public sealed class CollectionsScreen : ScreenView
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
                    Ui.Badge(face.rectTransform, _tabW - 12, 6, pending);
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

        // ---------- Альбом ----------

        /// <summary>Каждая коллекция — карточка-страница альбома: шапка, пять картинок с подписями, награда.</summary>
        private float BuildAlbum(RectTransform content)
        {
            var progress = App.Progress;
            var y = 6f;
            var cardW = W - 32f;
            const float gap = 8f;
            var justAdded = App.JustAdded;

            // Рамки для картинок: выбранная рамка образцом и кнопка — главный «сток» монет без давления.
            {
                const float stripH = 64f;
                var strip = Ui.Card(content, "FramesStrip", 16, y, cardW, stripH, 20, Palette.Card, Palette.CardShadow, 3f, true);
                var frame = progress.SelectedFrame;
                var sample = Ui.Card(strip, "Sample", 12, 10, 44, 44, 10, Palette.Hex(frame.Face), Palette.CardShadow, 0f);
                var mat = Ui.Box(sample, "Mat", 7, Palette.Hex(frame.Mat));
                Ui.Place(mat.rectTransform, 4f, 4f, 36f, 36f);
                Ui.Outline(sample, 10, Palette.Hex(frame.Edge), 1.5f).raycastTarget = false;
                // Заголовок в одну строку (ужимается, если не влезает), название рамки — под ним.
                var title = Ui.Fit(Ui.Label(strip, L.T("Рамки для картинок"), 16, Palette.Ink, 68, 9, cardW - 192, 24, true, TextAnchor.MiddleLeft), 11);
                title.horizontalOverflow = HorizontalWrapMode.Overflow;
                title.resizeTextForBestFit = false;
                if (title.preferredWidth > cardW - 192)
                {
                    title.fontSize = Mathf.Max(11, Mathf.FloorToInt(title.fontSize * (cardW - 192) / title.preferredWidth));
                }

                Ui.Fit(Ui.Label(strip, frame.Name, 13, Palette.Ink2, 68, 34, cardW - 192, 20, false, TextAnchor.MiddleLeft), 10);
                var open = Ui.Button(strip, L.T("Выбрать"), cardW - 112, 14, 100, 36, Palette.Primary, Palette.PrimaryShadow, Color.white, 15);
                open.OnClick(() => App.ShowPopup(new FramesPopup(App)));
                strip.gameObject.AddComponent<Tap>().OnClick(() => App.ShowPopup(new FramesPopup(App)));
                y += stripH + 16f;
            }

            for (var c = 0; c < Collections.All.Length; c++)
            {
                var collection = Collections.All[c];
                var tile = Mathf.Floor((cardW - 28f - gap * (collection.Count - 1)) / collection.Count);
                var done = 0;
                var stars = 0;
                for (var id = collection.FirstLevel; id <= collection.LastLevel; id++)
                {
                    done += progress.IsCompleted(id) ? 1 : 0;
                    stars += progress.StarsOf(id);
                }

                var tilesY = 66f;
                var stripY = tilesY + tile + MiniPicture.StarsOverhang + 34f;
                var cardH = stripY + 48f + 14f;
                var cardY = y;
                var index = c;

                // Карточки ниже первого экрана достраиваются в следующих кадрах (Defer): альбом открывается сразу.
                void BuildCard()
                {
                var card = Ui.Card(content, collection.Name, 16, cardY, cardW, cardH, 24, Palette.Card, Palette.CardShadow, 3f);
                Header(card, cardW, collection.Name, L.F("{0} из {1} картинок", done, collection.Count), done == collection.Count ? L.T("Собрана") : null,
                    $"{stars}/{collection.Count * 3}");

                MiniPicture.Shelf(card, 8f, tilesY + tile - 4f, collection.Count * (tile + gap) - gap + 12f);
                for (var i = 0; i < collection.Count; i++)
                {
                    var id = collection.FirstLevel + i;
                    if (!App.Levels.Has(id))
                    {
                        continue;
                    }

                    var level = App.Levels.Get(id);
                    var state = progress.IsCompleted(id) ? TileState.Done : progress.IsAvailable(id) ? TileState.Current : TileState.Locked;
                    var x = 14 + i * (tile + gap);
                    var levelId = id;
                    var tap = MiniPicture.Tile(card, level, state, x, tilesY, tile, progress.StarsOf(id));
                    var rt = (RectTransform)tap.transform;
                    if (id == justAdded)
                    {
                        NewInAlbum(card, rt, x, tilesY, tile);
                    }
                    tap.OnClick(() => LevelInfoPopup.OnTile(App, levelId, rt));
                    var name = Ui.Label(card, state == TileState.Locked ? L.F("Ур. {0}", id) : level.Title, 11,
                        state == TileState.Current ? Palette.Primary : state == TileState.Locked ? Palette.Ink3 : Palette.Ink2,
                        x - 3, tilesY + tile + MiniPicture.StarsOverhang + 3f, tile + 6, 26, state == TileState.Current, TextAnchor.UpperCenter);
                    name.lineSpacing = 0.85f;

                    // Длинное слово («Клубничка», «Подсолнух») не наезжает на соседнее: ужимается в свою ширину.
                    Ui.Fit(name, 8);
                }

                CollectionReward.Build(App, card, index, 12, stripY, cardW - 24, () => App.ShowCollections(CollectionsTab.Album));
                }

                if (cardY > H)
                {
                    Defer(BuildCard);
                }
                else
                {
                    BuildCard();
                }

                y += cardH + 16f;
            }

            // Подсветка новой картинки — один раз.
            App.JustAdded = 0;
            return y;
        }

        /// <summary>
        /// Картинка, только что попавшая в альбом: рамка трижды вспыхивает золотом и гаснет, над ней ярлык «Новая»,
        /// из рамки искры. Один раз — при первом заходе в альбом после победы.
        /// </summary>
        private void NewInAlbum(RectTransform card, RectTransform tile, float x, float y, float size)
        {
            var ring = Ui.Image(card, "NewRing", Art.RoundRectOutline(10, 3), Palette.Coin);
            ring.type = UnityEngine.UI.Image.Type.Sliced;
            ring.pixelsPerUnitMultiplier = Art.Density;
            Ui.Place(ring.rectTransform, x - 4f, y - 4f, size + 8f, size + 8f);
            ring.raycastTarget = false;
            Tweens.Run(ring, 2.4f, t =>
            {
                var pulse = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 3f)) * (t < 0.83f ? 1f : (1f - t) / 0.17f);
                ring.color = Palette.WithAlpha(Palette.Coin, 0.15f + 0.85f * pulse);
            }, null, () => ring.color = Palette.WithAlpha(Palette.Coin, 0.4f), 0.35f);

            var label = Ui.Box(card, "NewLabel", 9, Palette.Danger).rectTransform;
            Ui.Place(label, x + size / 2f - 26f, y - 12f, 52f, 18f);
            Ui.Label(label, L.T("Новая"), 11, Color.white, 0, 0, 52, 18);
            Tweens.Scale(label, 0f, 1f, 0.4f, Ease.OutBack, null, 0.35f);
            Tweens.Delay(tile, 0.5f, () => Fx.Sparkle(Root, Ui.WorldCenter(tile), Palette.Coin, 10, size * 0.7f));
        }

        /// <summary>Полоска «В работе»: моток, его бонус; тап — карточка мотка. Её же видно на экране сундука.</summary>
        public static void ActiveYarnStrip(RectTransform parent, float x, float y, float w, System.Action refresh)
        {
            var app = App.Instance;
            if (app == null)
            {
                return;
            }

            var progress = app.Progress;
            var active = progress.ActiveYarn;
            var open = progress.PerksOpen;
            var strip = Ui.Box(parent, "Active", 14, active >= 0 && open ? Palette.PrimarySoft : Palette.Surface, active >= 0).rectTransform;
            Ui.Place(strip, x, y, w, 40);
            if (active < 0 || !open)
            {
                // До 9-го уровня пряжа — просто красивая коллекция: про бонусы только одна тихая строка.
                var text = !open ? L.F("Бонусы пряжи откроются на уровне {0}", YarnCatalog.PerksFromLevel) : L.T("В работе пока ничего: найди первый моток");
                Ui.Fit(Ui.Label(strip, text, 12, Palette.Ink2, 12, 0, w - 24, 40, false, TextAnchor.MiddleLeft), 9);
                return;
            }

            var yarn = YarnCatalog.All[active];
            var perk = YarnCatalog.PerkOf(active);
            YarnView.Create(strip, yarn, 22, 20, 26, true);
            Ui.Fit(Ui.Label(strip, L.F("В работе: {0} · ур. {1}", yarn.Name, progress.YarnLevel(active)), 13, Palette.Ink, 42, 3, w - 52, 18, true,
                TextAnchor.MiddleLeft), 10);
            Ui.Fit(Ui.Label(strip, YarnCatalog.PerkText(perk, YarnCatalog.Power(active, progress.YarnLevel(active))), 11, Palette.Primary,
                42, 20, w - 52, 16, false, TextAnchor.MiddleLeft), 8);
            strip.gameObject.AddComponent<Tap>().OnClick(() =>
                app.ShowPopup(new YarnCardPopup(app, active, refresh)));
        }

        /// <summary>
        /// Шапка карточки: название и подпись слева; справа — зелёная плашка «Собрана»
        /// или счёт со звездой.
        /// </summary>
        private static void Header(RectTransform card, float cardW, string title, string subtitle, string? doneChip, string score)
        {
            // Плашка справа — по ширине текста; название и подпись занимают остаток строки с зазором 12
            // и ужимаются, если не влезают, но на плашку не заходят.
            var chipText = doneChip ?? (score.Length > 0 ? score : null);
            var chipW = 0f;
            if (chipText != null)
            {
                var done = doneChip != null;
                var label = Ui.Label(card, chipText, 13, done ? Palette.Primary : Palette.Ink, true, TextAnchor.MiddleLeft);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                chipW = Mathf.Min(Mathf.Ceil(label.preferredWidth) + 42f, cardW * 0.42f);
                var back = done ? Palette.PrimarySoft : Palette.Surface;
                var chip = Ui.Card(card, done ? "Done" : "Score", cardW - 16 - chipW, 18, chipW, 28, 14, back, back, 0f);
                if (done)
                {
                    Ui.IconImage(chip, Icon.Check, Palette.Primary, 18, 14, 14);
                }
                else
                {
                    Ui.IconImage(chip, Icon.Star, Palette.Coin, 18, 14, 16);
                }

                label.rectTransform.SetParent(chip, false);
                Ui.Place(label.rectTransform, 30, 0, chipW - 36, 28);
            }

            var textW = cardW - 16 - (chipW > 0 ? chipW + 12f : 0f) - 16;
            Ui.Fit(Ui.Label(card, title, 18, Palette.Ink, 16, 12, textW, 26, true, TextAnchor.MiddleLeft), 13);
            Ui.Fit(Ui.Label(card, subtitle, 13, Palette.Ink2, 16, 36, textW, 20, false, TextAnchor.MiddleLeft), 10);
        }

        // ---------- Пряжа ----------

        /// <summary>
        /// Сверху — плетёная корзинка с найденными мотками и общим прогрессом, ниже — наборы карточками.
        /// Плитка мотка окрашена редкостью: у редкой мягкое сияние, у эпической — медленные лучи; найденные
        /// мотки чуть покачиваются, каждый в своём ритме, искры блёсток вспыхивают по очереди. При входе
        /// плитки выпрыгивают волной. «Меньше движения» всё это останавливает.
        /// </summary>
        private float BuildYarn(RectTransform content)
        {
            var progress = App.Progress;
            var cardW = W - 32f;
            var y = 6f;
            var entering = App.PreviousScreen != typeof(CollectionsScreen);

            const float bannerH = 150f;
            var banner = Ui.Card(content, "Basket", 16, y, cardW, bannerH, 24, Palette.Card, Palette.CardShadow, 3f);
            BuildBasket(banner, progress);
            const float textX = 118f;
            Ui.Fit(Ui.Label(banner, L.T("Корзинка пряжи"), 18, Palette.Ink, textX, 14, cardW - textX - 48, 26, true, TextAnchor.MiddleLeft), 13);
            var kinds = Ui.Label(banner, L.F("{0} из {1} мотков", progress.YarnKinds, YarnCatalog.Count), 13, Palette.Ink2, textX, 40, cardW - textX - 48, 18, false,
                TextAnchor.MiddleLeft);
            if (entering)
            {
                kinds.text = L.F("0 из {0} мотков", YarnCatalog.Count);
                Ui.RollTo(kinds, progress.YarnKinds, 0.8f, L.F("{{0}} из {0} мотков", YarnCatalog.Count), 0.15f);
            }
            var fill = Ui.ProgressBar(banner, textX, 66, cardW - textX - 18, 10, (float)progress.YarnKinds / YarnCatalog.Count, Palette.Link);
            if (entering && !Tweens.Calm)
            {
                // Полоска набирается до своего значения.
                var full = fill.sizeDelta.x;
                Tweens.Run(fill, 0.8f, t => fill.sizeDelta = new Vector2(Mathf.Max(10f, full * t), fill.sizeDelta.y), Ease.OutCubic, null, 0.15f);
            }

            Ui.InfoButton(banner, cardW - 30, 26, L.T("Пряжа и её бонусы"),
                L.T("Мотки выпадают за новые картинки, в звёздном сундуке и на 7-й день подарков. ") +
                L.F("С уровня {0} моток «в работе» даёт бонус: монеты за картинку или дороже золотые петли. ", YarnCatalog.PerksFromLevel) +
                L.T("Повтор поднимает уровень мотка до 3, дальше даёт монеты. Полный набор приносит награду."), 26);
            ActiveYarnStrip(banner, 12, 98, cardW - 24, () => App.ShowCollections(CollectionsTab.Yarn));
            y += bannerH + 16f;

            const float gap = 8f;
            var setIndex = 0;
            var tileIndex = 0;
            foreach (var set in YarnCatalog.Sets)
            {
                var owned = 0;
                for (var i = set.First; i < set.First + set.Count; i++)
                {
                    owned += progress.HasYarn(i) ? 1 : 0;
                }

                var cellW = (cardW - 28f - gap * (set.Count - 1)) / set.Count;
                const float cellH = 140f;
                var cellsY = 66f;
                var stripY = cellsY + cellH + 14f;
                var cardH = stripY + 48f + 14f;
                var cardY = y;
                var target = set;
                var firstTile = tileIndex;
                var order = setIndex;
                var ownedCount = owned;

                // Наборы ниже первого экрана достраиваются в следующих кадрах (Defer).
                void BuildSet()
                {
                    var card = Ui.Card(content, target.Name, 16, cardY, cardW, cardH, 24, Palette.Card, Palette.CardShadow, 3f);
                    Header(card, cardW, target.Name, L.F("{0} из {1} мотков", ownedCount, target.Count), ownedCount == target.Count ? L.T("Собран") : null, "");
                    for (var i = 0; i < target.Count; i++)
                    {
                        var yarn = YarnCatalog.All[target.First + i];
                        var x = 14 + i * (cellW + gap);
                        var cell = YarnTile(card, yarn, x, cellsY, cellW, cellH, firstTile + i);
                        if (entering)
                        {
                            Tweens.Scale(cell, 0.82f, 1f, 0.42f, Ease.OutBack, null, 0.08f + order * 0.1f + i * 0.05f);
                        }
                    }

                    var booster = BoosterInfo.Name(target.Reward.Booster!.Value).ToLower();
                    RewardStrip.Build(card, 12, stripY, cardW - 24, L.F("За весь набор: {0} монет и {1} ×{2}", target.Reward.Coins, booster, target.Reward.BoosterCount),
                        (float)ownedCount / target.Count, progress.CanClaimSet(target), progress.IsSetClaimed(target), L.T("Набор собран!"),
                        () => Claim(progress.ClaimSet(target, App.Random), L.F("Набор «{0}»", target.Name), Icon.Gift, CollectionsTab.Yarn));
                }

                if (cardY > H)
                {
                    Defer(BuildSet);
                }
                else
                {
                    BuildSet();
                }

                tileIndex += set.Count;
                y += cardH + 16f;
                setIndex++;
            }

            return y;
        }

        /// <summary>
        /// Корзинка в шапке: задняя стенка с ручкой, до трёх найденных мотков (сначала тот, что «в работе»,
        /// дальше последние найденные) и передняя плетёная стенка поверх. Мотки чуть покачиваются.
        /// </summary>
        private static void BuildBasket(RectTransform banner, ProgressService progress)
        {
            const float bx = 8f;
            const float by = 4f;
            const float bw = 104f;
            const float bh = bw * 96f / 120f;
            Ui.Place(Ui.Image(banner, "BasketBack", Art2.BasketBack, Color.white).rectTransform, bx, by, bw, bh);

            var shown = new System.Collections.Generic.List<int>();
            if (progress.ActiveYarn >= 0 && progress.HasYarn(progress.ActiveYarn))
            {
                shown.Add(progress.ActiveYarn);
            }

            for (var id = YarnCatalog.Count - 1; id >= 0 && shown.Count < 3; id--)
            {
                if (progress.HasYarn(id) && !shown.Contains(id))
                {
                    shown.Add(id);
                }
            }

            // Горловина корзинки в растре — на высоте 46 из 96 (снизу): мотки лежат чуть выше неё.
            var rim = by + bh * (1f - 46f / 96f);
            float[] xs = { 0.5f, 0.3f, 0.7f };
            float[] lift = { 12f, 6f, 6f };
            for (var i = shown.Count - 1; i >= 0; i--)
            {
                var size = i == 0 ? 36f : 30f;
                var view = YarnView.Create(banner, YarnCatalog.All[shown[i]], bx + bw * xs[i], rim - lift[i], size, true);
                var rest = view.anchoredPosition;
                var phase = i * 0.33f;
                Tweens.Run(view, 3.2f, t =>
                {
                    var wave = Mathf.Sin((t + phase) * Mathf.PI * 2f);
                    view.anchoredPosition = rest + new Vector2(0f, 1.8f * wave);
                    view.localRotation = Quaternion.Euler(0f, 0f, 4f * Mathf.Sin((t + phase) * Mathf.PI * 2f + 1f));
                }, null, null, 0f, true);
            }

            Ui.Place(Ui.Image(banner, "BasketFront", Art2.BasketFront, Color.white).rectTransform, bx, by, bw, bh);
        }

        /// <summary>
        /// Плитка мотка: фон по редкости (обычная — светлая, редкая — голубоватая с сиянием, эпическая — тёплая
        /// с лучами), клубок на мягкой тени покачивается, звезда уровня, камень редкости. Не найден — бледный
        /// силуэт с «?». «В работе» — зелёная рамка дышит. Тап — карточка мотка.
        /// </summary>
        private RectTransform YarnTile(RectTransform card, YarnInfo yarn, float x, float y, float cellW, float cellH, int index)
        {
            var progress = App.Progress;
            var has = progress.HasYarn(yarn.Id);
            var back = !has ? Palette.WithAlpha(Palette.Surface, 0.6f)
                : yarn.Rarity == Rarity.Epic ? Palette.Hex("#FFF4D9")
                : yarn.Rarity == Rarity.Rare ? Palette.Hex("#EAF0F9")
                : Palette.Surface;
            var cell = Ui.Box(card, "Yarn", 16, back, true).rectTransform;
            Ui.Place(cell, x, y, cellW, cellH);
            var border = !has ? Palette.WithAlpha(Palette.Line, 0.7f)
                : yarn.Rarity == Rarity.Epic ? Palette.WithAlpha(Palette.Coin, 0.7f)
                : yarn.Rarity == Rarity.Rare ? Palette.WithAlpha(Palette.Link, 0.35f)
                : Palette.Line;
            Ui.Outline(cell, 16, border, 1.5f);

            const float ballY = 46f;
            const float ballSize = 58f;
            if (has)
            {
                // Свечение редкости: голубое у редкой, тёплое с лучами у эпической (YarnView.Aura).
                YarnView.Aura(cell, yarn.Rarity, cellW / 2f, ballY, ballSize, index * 0.21f);
            }

            var shadow = Ui.Image(cell, "Shadow", Art2.BallShadow, Palette.WithAlpha(Color.white, has ? 1f : 0.5f));
            var shadowRt = shadow.rectTransform;
            Ui.PlaceCenter(shadowRt, cellW / 2f + 2f, ballY + ballSize * 0.46f, ballSize * 0.9f, ballSize * 0.26f);
            var ball = YarnView.Create(cell, yarn, cellW / 2f, ballY, ballSize, has);
            if (has)
            {
                // Покачивание: клубок чуть приподнимается, тень под ним сжимается; у каждого свой ритм.
                var rest = ball.anchoredPosition;
                var period = 2.6f + (index % 3) * 0.35f;
                Tweens.Run(ball, period, t =>
                {
                    var lift = 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI * 2f);
                    ball.anchoredPosition = rest + new Vector2(0f, 3f * lift);
                    shadowRt.localScale = new Vector3(1f - 0.12f * lift, 1f - 0.12f * lift, 1f);
                }, null, null, index * 0.37f, true);
            }

            Ui.Fit(Ui.Label(cell, has ? yarn.Name : "???", 13, has ? Palette.Ink : Palette.Ink3, 2, 84, cellW - 4, 20), 10);
            RarityChip(cell, yarn.Rarity, cellW / 2f, 118f, cellW - 8f);

            // Уровень мотка — на выпуклой звезде в углу.
            if (has)
            {
                var star = Ui.Image(cell, "Level", Art2.LevelStar, Color.white);
                Ui.PlaceCenter(star.rectTransform, cellW - 17f, 17f, 30f, 30f);
                Ui.Label(star.rectTransform, progress.YarnLevel(yarn.Id).ToString(), 12, Color.white, 0, 2, 30, 26);
            }

            if (progress.ActiveYarn == yarn.Id)
            {
                var ring = Ui.Image(cell, "ActiveRing", Art.RoundRectOutline(16, 2.5f), Palette.Primary);
                ring.type = UnityEngine.UI.Image.Type.Sliced;
                ring.pixelsPerUnitMultiplier = Art.Density;
                Ui.Stretch(ring.rectTransform);
                Tweens.Run(ring, 2.2f, t => ring.color = Palette.WithAlpha(Palette.Primary, 0.55f + 0.45f * Ease.Bump(t)), null, null, 0f, true);
                var mark = Ui.Image(cell, "Active", Art.Circle, Palette.Primary);
                Ui.PlaceCenter(mark.rectTransform, 15, 15, 20, 20);
                Ui.IconImage(cell, Icon.Check, Color.white, 15, 15, 12);
            }

            // Тап — карточка с характеристикой; у ненайденного видно, за чем охотиться.
            var id = yarn.Id;
            cell.gameObject.AddComponent<Tap>().OnClick(() =>
                App.ShowPopup(new YarnCardPopup(App, id, () => App.ShowCollections(CollectionsTab.Yarn))));
            return cell;
        }

        /// <summary>Плашка редкости: огранённый камень (дымчатый, аметист, топаз) и подпись цветом редкости.</summary>
        public static void RarityChip(RectTransform parent, Rarity rarity, float cx, float cy, float maxW = 0f)
        {
            var back = rarity == Rarity.Common ? Palette.Track : rarity == Rarity.Rare ? Palette.Hex("#E1EAF8") : Palette.CoinSoft;
            var text = YarnCatalog.RarityName(rarity);
            var chip = Ui.Box(parent, "Rarity", 10, back).rectTransform;
            var label = Ui.Label(chip, text, 11, YarnView.RarityColor(rarity), true, TextAnchor.MiddleLeft);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            const float gem = 16f;
            var w = Mathf.Max(text.Length * 6.6f, label.preferredWidth) + gem + 16f;
            if (maxW > 0f && w > maxW)
            {
                // «эпическая» не вылезает за узкую плитку: плашка по ширине плитки, текст ужимается.
                w = maxW;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                Ui.Fit(label, 8);
            }

            Ui.PlaceCenter(chip, cx, cy, w, 20);
            var icon = Ui.Image(chip, "Gem", Art2.Gem(rarity), Color.white);
            Ui.PlaceCenter(icon.rectTransform, 5f + gem / 2f, 9.5f, gem, gem);
            Ui.Place(label.rectTransform, 6f + gem, 0f, w - gem - 10f, 20f);
        }

        // ---------- Достижения ----------

        /// <summary>
        /// Три секции: «Можно забрать» (золотые карточки с кнопкой), «В процессе» (прогресс и награда),
        /// «Получено» (белые карточки с зелёной плашкой — не теряются на фоне).
        /// </summary>
        private float BuildAchievements(RectTransform content)
        {
            var progress = App.Progress;
            var y = 4f;
            var sections = new[]
            {
                (L.T("Можно забрать"), 0),
                (L.T("В процессе"), 1),
                (L.T("Получено"), 2),
            };
            foreach (var (title, rank) in sections)
            {
                var items = new System.Collections.Generic.List<AchievementInfo>();
                foreach (var a in Achievements.All)
                {
                    if (Rank(a) == rank)
                    {
                        items.Add(a);
                    }
                }

                if (items.Count == 0)
                {
                    continue;
                }

                Ui.Label(content, $"{title} · {items.Count}", 14, Palette.Ink2, 20, y, W - 40, 24, true, TextAnchor.MiddleLeft);
                y += 28f;
                foreach (var a in items)
                {
                    // Строки ниже первого экрана — в следующих кадрах; высота строки известна заранее.
                    var rowY = y;
                    var item = a;
                    if (rowY > H)
                    {
                        Defer(() => AchievementRow(content, item, rowY));
                        y = rowY + AchievementRowH + 10f;
                    }
                    else
                    {
                        y = AchievementRow(content, item, rowY);
                    }
                }

                y += 10f;
            }

            return y;

            int Rank(AchievementInfo a) => progress.CanClaimAchievement(a) ? 0 : progress.IsAchievementClaimed(a) ? 2 : 1;
        }

        private const float AchievementRowH = 72f;

        private float AchievementRow(RectTransform content, AchievementInfo a, float y)
        {
            const float rowH = AchievementRowH;
            const float rightW = 112f;
            var progress = App.Progress;
            var claimed = progress.IsAchievementClaimed(a);
            var can = progress.CanClaimAchievement(a);
            var w = W - 32f;
            var row = Ui.Card(content, "Achievement", 16, y, w, rowH, 18, can ? Palette.CoinSoft : Palette.Card, Palette.CardShadow, 3f);
            if (can)
            {
                Ui.Outline(row, 18, Palette.Coin, 2f);
            }

            // Медаль со своим рисунком: золотая, когда можно забрать (на мягком сиянии, с искрами) и когда получена
            // (с зелёной галочкой), серебряная и чуть бледнее — пока в процессе.
            if (can)
            {
                var glow = Ui.Image(row, "Glow", Art.Glow, Palette.WithAlpha(Palette.Coin, 0.5f));
                var glowRt = glow.rectTransform;
                Ui.PlaceCenter(glowRt, 36, rowH / 2f, 76, 76);
                Tweens.Run(glowRt, 2.2f, t => glowRt.localScale = Vector3.one * (0.9f + 0.14f * Ease.Bump(t)), null, null, 0f, true);
            }

            var cup = Ui.Image(row, "Trophy", Art2.Medal(Art2.BadgeOf(a), can || claimed), Palette.WithAlpha(Color.white, can || claimed ? 1f : 0.85f));
            var cupRt = cup.rectTransform;
            Ui.PlaceCenter(cupRt, 36, rowH / 2f, 56, 56);
            if (can)
            {
                // Кубок, который ждёт, раз в пару секунд подмигивает искрой и чуть подпрыгивает.
                var spark = Ui.Image(row, "Spark", Art2.Sparkle, Palette.Hex("#FFF3B0"));
                var sparkRt = spark.rectTransform;
                Ui.PlaceCenter(sparkRt, 46, rowH / 2f - 12f, 16, 16);
                Tweens.Run(sparkRt, 2.4f, t =>
                {
                    var flash = t < 0.3f ? Ease.Bump(t / 0.3f) : 0f;
                    sparkRt.localScale = Vector3.one * (0.1f + flash);
                    sparkRt.localRotation = Quaternion.Euler(0f, 0f, 120f * t);
                }, null, null, 0.4f, true);
                Tweens.Run(cupRt, 2.4f, t => cupRt.localScale = Vector3.one * (1f + 0.08f * (t > 0.5f && t < 0.75f ? Ease.Bump((t - 0.5f) / 0.25f) : 0f)),
                    null, null, 0f, true);
            }
            else if (claimed)
            {
                var tick = Ui.Image(row, "Done", Art.Circle, Palette.Primary);
                Ui.PlaceCenter(tick.rectTransform, 52, rowH / 2f + 14f, 18, 18);
                Ui.IconImage(row, Icon.Check, Color.white, 52, rowH / 2f + 14f, 11);
            }

            // Готовое к получению: полоска уже полная и не нужна, кнопка компактнее и стоит с отступом от текста.
            const float claimW = 100f;
            var textW = can ? w - 68f - claimW - 28f : w - 68f - rightW - 26f;
            var hasBar = !claimed && !can;
            var titleY = hasBar ? 9f : 14f;
            Ui.Fit(Ui.Label(row, a.Title, 15, Palette.Ink, 68, titleY, textW, 20, true, TextAnchor.MiddleLeft), 11);
            Ui.Fit(Ui.Label(row, a.Description, 12, Palette.Ink2, 68, titleY + 21, textW, 18, false, TextAnchor.MiddleLeft), 9);
            var value = progress.AchievementValue(a);
            if (hasBar)
            {
                Ui.ProgressBar(row, 68, 54, textW, 6, (float)value / a.Target, can ? Palette.Primary : Palette.Link);
            }

            var right = w - rightW - 10f;
            if (can)
            {
                var target = a;
                var claimButton = Ui.Button(row, L.T("Забрать"), w - claimW - 14f, 17, claimW, 36, Palette.Primary, Palette.PrimaryShadow, Color.white, 15);
                claimButton.OnClick(() => Claim(progress.ClaimAchievement(target, App.Random), target.Title, Icon.Trophy, CollectionsTab.Achievements,
                    Art2.Medal(Art2.BadgeOf(target), true)));
                Ui.Sheen((RectTransform)claimButton.transform, claimW, 36f, 3.2f, 0.6f + y * 0.002f);
            }
            else if (claimed)
            {
                var chip = Ui.Card(row, "Done", right + 4, 22, rightW - 4, 28, 14, Palette.PrimarySoft, Palette.PrimarySoft, 0f);
                Ui.IconImage(chip, Icon.Check, Palette.Primary, 16, 14, 14);
                Ui.Label(chip, L.T("Получено"), 12, Palette.Primary, 26, 0, rightW - 32, 28, true, TextAnchor.MiddleLeft);
            }
            else
            {
                Ui.Label(row, $"{value}/{a.Target}", 13, Palette.Ink2, right, 8, rightW, 20, true, TextAnchor.MiddleRight);
                RewardInline(row, a.Reward, right + rightW, 42);
            }

            return y + rowH + 10f;
        }

        /// <summary>
        /// Награда в строке достижения — колонками: плашка монет одной ширины всегда на одном месте,
        /// справа от неё — место под значок бустера (пустое, если бустера нет). Так монеты стоят ровно в ряд.
        /// </summary>
        private static void RewardInline(RectTransform parent, Reward reward, float rightEdge, float cy)
        {
            const float boosterColumn = 32f;
            const float chipW = 78f;
            if (reward.Booster != null)
            {
                var badge = Ui.Image(parent, "Booster", Art.Circle, Palette.PrimarySoft);
                Ui.PlaceCenter(badge.rectTransform, rightEdge - 12, cy, 28, 28);
                var art = Ui.Image(parent, "BoosterArt", Art2.Booster(reward.Booster.Value), Color.white);
                Ui.PlaceCenter(art.rectTransform, rightEdge - 12, cy, 24, 24);
            }

            if (reward.Coins > 0)
            {
                var chip = Ui.Box(parent, "Coins", 12, Palette.CoinSoft).rectTransform;
                Ui.PlaceCenter(chip, rightEdge - boosterColumn - chipW / 2f, cy, chipW, 24);
                Ui.Label(chip, "+" + reward.Coins, 14, Palette.Ink, 6, 0, chipW - 36, 24, true, TextAnchor.MiddleRight);
                Ui.Coin(chip, chipW - 16, 12, 17);
            }
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
