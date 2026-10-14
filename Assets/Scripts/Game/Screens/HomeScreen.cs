#nullable enable

using KnittedPicture.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Главная: клубок «в работе», лента коллекций, ряд событий (подарок, задания, сундук, узор дня —
    /// с красными точками, когда что-то ждёт) и большая кнопка «Играть» с эскизом следующей картинки.
    /// </summary>
    public sealed class HomeScreen : ScreenView
    {
        public HomeScreen(App app)
            : base(app, "Home")
        {
            var progress = app.Progress;
            Ui.RoundButton(Root, Icon.Settings, 16, 8).OnClick(() => app.ShowPopup(new SettingsPopup(app)));
            Ui.CoinPill(Root, progress.Coins, W - 16, 8);

            // Главная под одну руку: всё, что нажимают каждый день, — в нижних двух третях экрана, куда
            // достаёт большой палец. Снизу вверх: вкладки, «Играть», ряд событий (подарок, задания, сундук,
            // узор дня), лента коллекций. Наверху то, что не нажимают часто: настройки, монеты и клубок
            // «в работе» — он занимает оставшееся место и не оставляет пустоты.
            // Сетка 8 pt и правило близости: внутри группы элементы рядом, между группами — заметный
            // промежуток (24–32 pt), иначе экран читается как одна слипшаяся куча. Нижние группы стоят
            // с фиксированными зазорами в зоне большого пальца; всё остальное место — клубку наверху.
            const float railH = 80f;
            const float heroTop = 58f;
            // На низких экранах (iPhone SE и меньше) зазоры ужимаются, чтобы клубку осталось место.
            var roomy = Mathf.Clamp01((H - 620f) / 120f);
            var playGap = Mathf.Lerp(12f, 24f, roomy);
            var groupGap = Mathf.Lerp(14f, 32f, roomy);
            var playY = H - TabBar.Height - 16f - 84f;
            BuildPlay(playY);
            var railY = playY - playGap - railH;
            BuildRails(railY, railH);
            var collectionTop = BuildCollection(railY - groupGap);

            var heroBottom = collectionTop - groupGap;
            var ball = Mathf.Clamp((heroBottom - heroTop) * 0.62f, 64f, 128f);
            BuildLogo(heroTop + (heroBottom - heroTop - ball) / 2f, ball);
            TabBar.Create(app, Root, W, H, MainTab.Home);

            // До первой рекламы — один раз спросить про персональную рекламу.
            if (app.Ads.NeedsConsent && progress.Data.adsConsent == 0)
            {
                Tweens.Delay(Root, 0.6f, () =>
                {
                    if (!app.HasPopups && app.Progress.Data.adsConsent == 0)
                    {
                        app.ShowPopup(new ConsentPopup(app));
                    }
                });
            }

            // Вход на главную: значки событий по очереди «выпрыгивают», кнопка «Играть» мягко подрастает.
            // Только при входе с другого экрана — после получения награды главная не прыгает заново.
            if (app.PreviousScreen != typeof(HomeScreen))
            {
                for (var i = 0; i < _rails.Count; i++)
                {
                    Tweens.Scale(_rails[i], 0.4f, 1f, 0.4f, Ease.OutBack, null, 0.08f + i * 0.06f);
                }
            }
        }

        /// <summary>Системная «Назад» на главной: на Android игра сворачивается, как обычное приложение.</summary>
        public override bool OnBack()
        {
            Platform.MoveToBack();
            return true;
        }

        /// <summary>
        /// Клубок на главной — это пряжа «в работе» из коллекции: её цвет, меланж вторым цветом,
        /// искры у блестящей и эпической, радужный — радуга. Нашёл новый моток — и главная другая.
        /// Сзади две бамбуковые спицы: сразу видно, что игра про вязание.
        /// Тап — клубок сжимается, подпрыгивает и чуть откатывается; каждый седьмой тап — кувырок с искрами.
        /// </summary>
        private void BuildLogo(float logoY, float ball)
        {
            var info = App.Progress.ActiveYarn >= 0 ? YarnCatalog.All[App.Progress.ActiveYarn] : null;
            var main = info != null ? Palette.Hex(info.Hex) : Palette.Hex("#FF6FA5");
            var cy = logoY + ball / 2f;

            // Тень на полу — отдельно: клубок подпрыгивает, а она остаётся и сжимается.
            var shadow = Ui.Image(Root, "LogoShadow", Art.Glow, Palette.WithAlpha(Palette.Ink, 0.22f));
            Ui.PlaceCenter(shadow.rectTransform, W / 2f, logoY + ball * 0.9f, ball * 0.78f, ball * 0.16f);

            // logo — прыжки и сжатие по тапу; sway — вечное лёгкое покачивание вместе со спицами.
            var logo = Ui.PlaceCenter(Ui.Node(Root, "Logo"), W / 2f, cy, ball, ball);
            var hit = Ui.Image(logo, "Hit", null, Color.clear, true);
            Ui.Stretch(hit.rectTransform);
            var sway = Ui.Stretch(Ui.Node(logo, "Sway"));
            Tweens.Run(sway, 3.2f, t => sway.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * Mathf.PI * 2f) * 4f), null, null, 0, true);

            // Спицы крест-накрест за клубком: бусины сверху, острия торчат снизу. Центр — середина шара
            // (в спрайте шар чуть выше середины: снизу место для нити).
            var ballCenter = ball / 2f - ball * 12f / 320f;
            foreach (var angle in new[] { -135f, 135f })
            {
                var needle = Ui.Image(sway, "Needle", Art.KnittingNeedle, Color.white);
                Ui.PlaceCenter(needle.rectTransform, ball / 2f, ballCenter, ball * 0.12f, ball * 1.42f);
                needle.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
            }

            var rainbow = info != null && info.Look == YarnLook.Rainbow;
            var yarn = Ui.Image(sway, "Ball", rainbow ? Art.YarnBallLargeRainbow : Art.YarnBallLarge, rainbow ? Color.white : main);
            Ui.Stretch(yarn.rectTransform);
            if (info != null && info.Look == YarnLook.Melange)
            {
                Ui.Stretch(Ui.Image(sway, "Melange", Art.YarnBallLargeMelange, Palette.Hex(info.Hex2)).rectTransform);
            }

            if (info != null && (info.Look == YarnLook.Sparkle || info.Rarity == Rarity.Epic))
            {
                var spark = info.Look == YarnLook.Sparkle ? Palette.Hex(info.Hex2) : Palette.Coin;
                var spots = new[] { (0.24f, 0.24f, 0.18f), (0.8f, 0.34f, 0.14f), (0.68f, 0.8f, 0.16f) };
                for (var i = 0; i < spots.Length; i++)
                {
                    var (x, y, size) = spots[i];
                    var star = Ui.Image(sway, "Spark", Art.Icon(Icon.Star), spark);
                    Ui.PlaceCenter(star.rectTransform, ball * x, ball * y, ball * size, ball * size);
                    var rt = star.rectTransform;
                    Tweens.Run(rt, 1.4f, t => rt.localScale = Vector3.one * (0.6f + 0.5f * Ease.Bump(t)), null, null, i * 0.35f, true);
                }
            }

            var basePosition = logo.anchoredPosition;
            var shadowRect = shadow.rectTransform;
            var shadowSize = shadowRect.sizeDelta;
            var busy = false;
            var taps = 0;
            var tap = logo.gameObject.AddComponent<Tap>();
            tap.PressFeedback = false;
            tap.ClickSound = false;
            tap.OnClick(() =>
            {
                if (busy)
                {
                    return;
                }

                busy = true;
                taps++;

                // Какая пряжа «в работе» и что даёт — коротко, по первому тапу, без постоянной плашки.
                if (taps == 1 && info != null)
                {
                    var perk = YarnCatalog.PerkOf(info.Id);
                    var power = App.Progress.PerkPower(perk);
                    App.Toast(power > 0 ? $"{info.Name} в работе: {YarnCatalog.PerkShort(perk, power)}" : $"{info.Name} в работе");
                }
                var flip = taps % 7 == 0;
                Sfx.Play(flip ? Sound.Star : Sound.Pick);
                Haptics.Play(flip ? Haptic.Medium : Haptic.Tap);
                if (flip)
                {
                    Fx.Sparkle(Root, Ui.WorldCenter(logo), main, 10, ball * 0.7f);
                }

                var side = taps % 2 == 0 ? 1f : -1f;
                Tweens.Run(logo, flip ? 0.8f : 0.6f, t =>
                {
                    // Сжатие перед прыжком, вытягивание в полёте, сжатие при приземлении.
                    var squash = t < 0.12f ? t / 0.12f : Mathf.Max(0f, 1f - (t - 0.12f) / 0.08f);
                    var stretch = Ease.Bump(Mathf.Clamp01((t - 0.12f) / 0.32f));
                    var land = Ease.Bump(Mathf.Clamp01((t - 0.62f) / 0.2f));
                    var sx = (1f + 0.12f * squash) * (1f - 0.06f * stretch) * (1f + 0.08f * land);
                    var sy = (1f - 0.12f * squash) * (1f + 0.08f * stretch) * (1f - 0.08f * land);
                    logo.localScale = new Vector3(sx, sy, 1f);

                    var air = Ease.Bump(Mathf.Clamp01((t - 0.12f) / 0.5f));
                    logo.anchoredPosition = basePosition + new Vector2(side * 6f * air, ball * 0.16f * air);
                    logo.localRotation = Quaternion.Euler(0, 0, flip ? -360f * side * Ease.InOutCubic(Mathf.Clamp01((t - 0.1f) / 0.6f)) : -14f * side * air);

                    // Чем выше клубок, тем меньше и бледнее тень.
                    shadowRect.sizeDelta = shadowSize * (1f - 0.3f * air);
                }, null, () =>
                {
                    logo.localScale = Vector3.one;
                    logo.anchoredPosition = basePosition;
                    logo.localRotation = Quaternion.identity;
                    shadowRect.sizeDelta = shadowSize;
                    busy = false;
                });
            });
        }

        /// <summary>Какую коллекцию показывает карусель; null — ту, где текущий уровень.</summary>
        private static int? _page;

        private readonly System.Collections.Generic.List<RectTransform> _cards = new System.Collections.Generic.List<RectTransform>();
        private RectTransform _strip = null!;
        private RectTransform _indicator = null!;
        private float[] _dotX = new float[0];
        private float _pos;
        private float _speed;
        private int _target;
        private bool _dragging;

        /// <summary>После уровня главная снова показывает коллекцию текущего уровня.</summary>
        public static void ResetCarousel() => _page = null;

        public override void Tick(float dt)
        {
            if (_cards.Count == 0)
            {
                return;
            }

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
                ApplyCarousel();
            }
        }

        /// <summary>
        /// Карусель коллекций между клубком и «Играть». Лента карточек едет за пальцем и плавно
        /// доводится до ближайшей; соседние карточки чуть меньше и бледнее. Под лентой — точки,
        /// активная «перетекает» к следующей. Тап по картинке — карточка уровня, текущий — сразу игра.
        /// </summary>
        /// <returns>Верх ленты: над ним остаётся место для клубка.</returns>
        private float BuildCollection(float bottom)
        {
            var progress = App.Progress;
            var cardH = PageCardHeight();
            const float dotsGap = 18f;
            var available = CollectionHeight();
            var y = bottom - available;
            var viewport = Ui.Place(Ui.Node(Root, "Carousel"), 0, y, W, available);
            var hit = Ui.Image(viewport, "Hit", null, Color.clear, true);
            Ui.Stretch(hit.rectTransform);
            viewport.gameObject.AddComponent<RectMask2D>();
            _strip = Ui.Place(Ui.Node(viewport, "Strip"), 0, 0, W * Collections.All.Length, available);

            for (var i = 0; i < Collections.All.Length; i++)
            {
                _cards.Add(BuildPageCard(i, out _));
            }

            const float top = 4f;
            for (var i = 0; i < _cards.Count; i++)
            {
                // Центр карточки — опорная точка: соседние уменьшаются к центру, а не к углу.
                Ui.PlaceCenter(_cards[i], i * W + W / 2f, top + cardH / 2f, W - 32f, cardH);
            }

            BuildDots(viewport, top + cardH + dotsGap);

            var pager = viewport.gameObject.AddComponent<Pager>();
            pager.Began += () =>
            {
                _dragging = true;
                _speed = 0f;
            };
            pager.Moved += dx =>
            {
                // За краями лента идёт туже — понятно, что дальше коллекций нет.
                var outside = _pos < 0f || _pos > _cards.Count - 1;
                _pos -= dx / W * (outside ? 0.35f : 1f);
                ApplyCarousel();
            };
            pager.Released += velocity =>
            {
                _dragging = false;

                // Скорость пальца (точки в секунду) — в скорость ленты (страницы в секунду), с запасом против перелёта на две.
                _speed = Mathf.Clamp(-velocity / W, -4f, 4f);
                var next = Mathf.RoundToInt(_pos);
                if (Mathf.Abs(velocity) > 350f)
                {
                    next = velocity < 0 ? Mathf.FloorToInt(_pos) + 1 : Mathf.CeilToInt(_pos) - 1;
                }

                Turn(Mathf.Clamp(next, 0, _cards.Count - 1));
            };

            _target = Mathf.Clamp(_page ?? IndexOf(Collections.Of(progress.CurrentLevel)), 0, _cards.Count - 1);
            _page = _target;
            _pos = _target;
            ApplyCarousel();
            App.SetGround(Palette.GroundOf(_target));
            return y;
        }

        /// <summary>Лента целиком: карточка и точки под ней.</summary>
        private float CollectionHeight() => PageCardHeight() + 30f;

        /// <summary>Высота карточки коллекции (как в BuildPageCard) — ленту ставим над рядом событий.</summary>
        private float PageCardHeight()
        {
            var cardW = W - 32f;
            const float gap = 8f;
            var count = 5;
            foreach (var c in Collections.All)
            {
                count = Mathf.Max(count, c.Count);
            }

            var tile = Mathf.Min(64f, Mathf.Floor((cardW - 24f - gap * (count - 1)) / count));
            return 58f + tile + MiniPicture.StarsOverhang + 34f + 6f;
        }

        private void BuildDots(RectTransform parent, float y)
        {
            var n = Collections.All.Length;
            const float step = 18f;
            _dotX = new float[n];
            for (var i = 0; i < n; i++)
            {
                _dotX[i] = W / 2f + (i - (n - 1) / 2f) * step;
                var dot = Ui.Box(parent, "Dot", 4, Palette.Inactive, true);
                Ui.PlaceCenter(dot.rectTransform, _dotX[i], y, 22, 22);
                dot.color = Color.clear;
                var visible = Ui.Box(dot.rectTransform, "Visible", 4, Palette.Inactive);
                Ui.PlaceCenter(visible.rectTransform, 11, 11, 8, 8);
                var target = i;
                dot.gameObject.AddComponent<Tap>().OnClick(() => Turn(target));
            }

            _indicator = Ui.Box(parent, "Indicator", 4, Palette.Ink).rectTransform;
            Ui.Place(_indicator, _dotX[0] - 11f, y - 4f, 22, 8);
        }

        private static int IndexOf(CollectionInfo collection) => System.Array.IndexOf(Collections.All, collection);

        /// <summary>Сдвинуть ленту, отмасштабировать соседей и перетечь точкой — по текущей позиции.</summary>
        /// <summary>Положение ленты, которое уже показано на экране.</summary>
        private float _appliedPos = float.NaN;

        private void ApplyCarousel()
        {
            _appliedPos = _pos;
            _strip.anchoredPosition = new Vector2(-_pos * W, _strip.anchoredPosition.y);

            // Фон перетекает вслед за лентой: оттенок между соседними коллекциями.
            if (_dragging || !Mathf.Approximately(_pos, _target))
            {
                var p0 = Mathf.Clamp(_pos, 0f, _cards.Count - 1);
                var a = Mathf.FloorToInt(p0);
                var b = Mathf.Min(a + 1, _cards.Count - 1);
                App.SetGround(Color.Lerp(Palette.GroundOf(a), Palette.GroundOf(b), p0 - a), true);
            }
            for (var i = 0; i < _cards.Count; i++)
            {
                var away = Mathf.Min(1f, Mathf.Abs(i - _pos));
                _cards[i].localScale = Vector3.one * (1f - 0.06f * away);
            }

            if (_dotX.Length < 2)
            {
                return;
            }

            // Перетекание: голова точки уходит к следующей раньше, хвост догоняет позже.
            var p = Mathf.Clamp(_pos, 0f, _dotX.Length - 1);
            var i0 = Mathf.Min(Mathf.FloorToInt(p), _dotX.Length - 2);
            var t = p - i0;
            var head = Mathf.Lerp(_dotX[i0], _dotX[i0 + 1], Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.6f)));
            var tail = Mathf.Lerp(_dotX[i0], _dotX[i0 + 1], Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.4f) / 0.6f)));
            var left = Mathf.Min(head, tail) - 11f;
            var right = Mathf.Max(head, tail) + 11f;
            _indicator.anchoredPosition = new Vector2(left, _indicator.anchoredPosition.y);
            _indicator.sizeDelta = new Vector2(right - left, 8f);
        }

        /// <summary>Листнуть к коллекции: лента доедет сама в Tick.</summary>
        private void Turn(int index)
        {
            index = Mathf.Clamp(index, 0, _cards.Count - 1);
            if (index != _target)
            {
                Sfx.Play(Sound.Pick, 0.5f);
            }

            _target = index;
            _page = index;
        }

        /// <summary>Карточка одной коллекции: шапка со стрелками, картинки с подписями, награда.</summary>
        private RectTransform BuildPageCard(int index, out float cardH)
        {
            var progress = App.Progress;
            var collection = Collections.All[index];
            var cardW = W - 32f;
            const float gap = 8f;
            var tile = Mathf.Min(64f, Mathf.Floor((cardW - 24f - gap * (collection.Count - 1)) / collection.Count));
            var tilesW = collection.Count * tile + (collection.Count - 1) * gap;
            var stripY = 58f + tile + MiniPicture.StarsOverhang + 34f;
            cardH = stripY + 6f;
            var card = Ui.Card(_strip, "Collection" + index, index * W + 16f, 0, cardW, cardH, 24, Palette.Card, Palette.CardShadow, 3f, true);

            var done = 0;
            var stars = 0;
            for (var id = collection.FirstLevel; id <= collection.LastLevel; id++)
            {
                done += progress.IsCompleted(id) ? 1 : 0;
                stars += progress.StarsOf(id);
            }

            Ui.Fit(Ui.Label(card, collection.Name, 18, Palette.Ink, 24, 10, cardW - 48, 24), 13);
            // Награда коллекции на главной не занимает отдельную полосу: когда её можно забрать,
            // подпись под названием превращается в золотую кнопку. Прогресс и награда — в альбоме.
            if (progress.CanClaimCollection(index))
            {
                var claim = Ui.Box(card, "Claim", 11, Palette.CoinSoft, true).rectTransform;
                Ui.PlaceCenter(claim, cardW / 2f, 42, Mathf.Min(cardW - 112, 200), 22);
                Ui.Outline(claim, 11, Palette.Coin, 1.5f);
                Ui.Label(claim, "Забрать награду", 12, Palette.CoinInk, 0, 0, Mathf.Min(cardW - 112, 200), 22);
                var collectionIndex = index;
                claim.gameObject.AddComponent<Tap>().OnClick(() => CollectionReward.Claim(App, collectionIndex, App.ShowHome));
            }
            else
            {
                Ui.Fit(Ui.Label(card, $"{done} из {collection.Count} · {stars}/{collection.Count * 3} ★", 13, Palette.Ink2, 24, 32, cardW - 48, 18, false), 10);
            }

            var x0 = (cardW - tilesW) / 2f;
            MiniPicture.Shelf(card, x0 - 8f, 58f + tile - 4f, tilesW + 16f);
            for (var i = 0; i < collection.Count; i++)
            {
                var id = collection.FirstLevel + i;
                if (!App.Levels.Has(id))
                {
                    continue;
                }

                var level = App.Levels.Get(id);
                var state = progress.IsCompleted(id) ? TileState.Done : progress.IsAvailable(id) ? TileState.Current : TileState.Locked;
                var levelId = id;
                var x = x0 + i * (tile + gap);
                var tap = MiniPicture.Tile(card, level, state, x, 58, tile, progress.StarsOf(id));
                var rt = (RectTransform)tap.transform;
                tap.OnClick(() => LevelInfoPopup.OnTile(App, levelId, rt));
                var name = Ui.Label(card, state == TileState.Locked ? $"Ур. {id}" : level.name, 11,
                    state == TileState.Current ? Palette.Primary : state == TileState.Locked ? Palette.Ink3 : Palette.Ink2,
                    x - 6, 58 + tile + MiniPicture.StarsOverhang + 3f, tile + 12, 26, state == TileState.Current, TextAnchor.UpperCenter);
                name.lineSpacing = 0.85f;
            }

            return card;
        }

        /// <summary>
        /// Кнопка «Узор дня»: эскиз сегодняшней картинки, подпись и серия дней. Не связан сегодня —
        /// красная точка; связан — галочка и «завтра новый», тап объясняет, когда ждать следующий.
        /// </summary>
        /// <summary>
        /// Круглые значки событий по бокам от клубка: слева «Подарок» и «Задания», справа «Сундук» и «Узор дня».
        /// Подпись жирная и красная точка — только там, где что-то ждёт. Это не вкладки, а действия на главной.
        /// </summary>
        /// <summary>
        /// Ряд событий над «Играть»: подарок, задания, сундук, узор дня — в зоне большого пальца,
        /// поровну по ширине. Что ещё не открыто по уровню, в ряд не попадает.
        /// </summary>
        private void BuildRails(float top, float railH)
        {
            var progress = App.Progress;
            var items = new System.Collections.Generic.List<(EventKind kind, Color tint, string title, bool waiting, System.Action open)>
            {
                (EventKind.Gift, Palette.Danger, "Подарок", progress.CanClaimDaily(App.Today),
                    () => App.ShowPopup(new DailyGiftPopup(App, App.ShowHome))),
            };
            if (progress.MissionsOpen)
            {
                items.Add((EventKind.Missions, Palette.Primary, "Задания", progress.CanClaimMissions(App.Today),
                    () => App.ShowPopup(new MissionsPopup(App, App.ShowHome))));
            }

            items.Add((EventKind.Chest, Palette.CoinShadow, "Сундук", progress.CanOpenChest, App.ShowChest));
            if (progress.PatternOpen)
            {
                items.Add((EventKind.Pattern, Palette.Link, "Узор дня", !progress.PatternDone(App.Today), () =>
                {
                    if (progress.PatternDone(App.Today))
                    {
                        // Часы переводили назад: узор связан «в будущем» и откроется после той даты, а не завтра.
                        var when = progress.PatternAhead(App.Today)
                            ? "Новый узор появится после даты, когда был связан последний: на телефоне часы переводили назад."
                            : "Новый узор появится завтра.";
                        App.ShowPopup(new InfoPopup(App, "Узор дня связан",
                            $"{when} Серия: {progress.PatternStreak(App.Today)} дн. подряд, каждый {DailyPattern.YarnEvery}-й день моток в подарок."));
                        return;
                    }

                    App.StartPattern();
                }));
            }

            // Поровну по ширине, но не шире 96 на значок: при двух-трёх значках они не разбегаются к краям.
            var step = Mathf.Min(96f, (W - 32f) / items.Count);
            var x0 = W / 2f - step * (items.Count - 1) / 2f;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                Rail(x0 + i * step, top, railH, item.kind, item.tint, item.title, item.waiting, item.open);
            }
        }

        /// <summary>Один значок события: круг 56 и подпись — касание не меньше 48×48.</summary>
        private readonly System.Collections.Generic.List<RectTransform> _rails = new System.Collections.Generic.List<RectTransform>();

        private void Rail(float cx, float top, float h, EventKind kind, Color tint, string title, bool waiting, System.Action open)
        {
            var root = Ui.Place(Ui.Node(Root, "Rail" + title), cx - 40f, top, 80f, h);
            _rails.Add(root);
            var hit = Ui.Image(root, "Hit", null, Color.clear, true);
            Ui.Stretch(hit.rectTransform);
            var circle = Ui.Image(root, "Back", Art.Circle, Palette.Card);
            Ui.PlaceCenter(circle.rectTransform, 40f, 28f, 56f, 56f);
            var tintCircle = Ui.Image(root, "Tint", Art.Circle, Palette.WithAlpha(tint, 0.16f));
            Ui.PlaceCenter(tintCircle.rectTransform, 40f, 28f, 44f, 44f);
            var art = EventArt.Create(root, kind, 40f, 28f, 40f);
            Ui.Fit(Ui.Label(root, title, 12, waiting ? Palette.Ink : Palette.Ink2, 0, 58f, 80f, 16f, waiting), 9);
            if (waiting)
            {
                // Ждёт — мягкий цветной ореол «дышит» за значком, сама иллюстрация покачивается.
                var halo = tintCircle.rectTransform;
                Tweens.Run(halo, 1.8f, t => halo.localScale = Vector3.one * (1f + 0.14f * Ease.Bump(t)), null, null, 0f, true);
                Ui.Badge(root, 62f, 4f, 0);
                var rt = art;
                Tweens.Run(rt, 1.6f, t => rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * Mathf.PI * 4f) * 10f * Ease.Bump(t)), null, null, 0.4f, true);
            }

            root.gameObject.AddComponent<Tap>().OnClick(open);
        }

        /// <summary>
        /// Эскиз следующей картинки на «Играть» при входе на главную «довязывается» снизу вверх по рядам,
        /// как на станке: кнопка показывает, ради чего играть, и приглашает продолжить.
        /// </summary>
        private static void KnitPreview(RectTransform frame, LevelData data, bool animate)
        {
            const float inner = 54f;
            var mask = Ui.Node(frame, "Knit");
            mask.anchorMin = mask.anchorMax = Vector2.zero;
            mask.pivot = Vector2.zero;
            mask.anchoredPosition = new Vector2(5f, 5f);
            mask.sizeDelta = new Vector2(inner, inner);
            mask.gameObject.AddComponent<RectMask2D>();
            var picture = MiniPicture.Create(mask, data, inner / 2f, inner / 2f, inner, faded: true).rectTransform;
            picture.anchorMin = picture.anchorMax = new Vector2(0.5f, 0f);
            picture.pivot = new Vector2(0.5f, 0.5f);
            picture.anchoredPosition = new Vector2(0f, inner / 2f);
            if (!animate)
            {
                return;
            }

            var rows = Mathf.Max(1, data.height);
            mask.sizeDelta = new Vector2(inner, 0f);
            Tweens.Run(mask, 1.1f, t => mask.sizeDelta = new Vector2(inner, inner * Mathf.Ceil(t * rows) / rows), null, null, 0.3f);
        }

        /// <summary>«Играть» с эскизом следующей картинки — видно, ради чего идёшь дальше.</summary>
        private void BuildPlay(float buttonY)
        {
            var progress = App.Progress;
            var playW = W - 40;

            if (progress.AllDone)
            {
                var done = Ui.Button(Root, "", 20, buttonY, playW, 84, Palette.Primary, Palette.PrimaryShadow, Color.white);
                Ui.Fit(Ui.Label((RectTransform)done.transform, "Все картинки связаны!", 22, Color.white, 8, 14, playW - 16, 30), 14);
                Ui.Fit(Ui.Label((RectTransform)done.transform, "Собирай звёзды в альбоме и пряжу", 14, Color.white, 8, 46, playW - 16, 22, false), 10);
                done.OnClick(App.ShowAlbum);
                return;
            }

            var level = progress.CurrentLevel;
            var play = Ui.Button(Root, "", 20, buttonY, playW, 84, Palette.Primary, Palette.PrimaryShadow, Color.white);
            var face = (RectTransform)play.transform;
            if (App.Levels.Has(level))
            {
                var data = App.Levels.Get(level);

                // Клубки этого уровня красятся в фоне, пока игрок на главной: «Играть» открывает уровень без паузы.
                Art2.Prefetch(data);
                var frame = Ui.Box(face, "Preview", 14, Palette.Card);
                Ui.Place(frame.rectTransform, 12, 10, 64, 64);
                KnitPreview(frame.rectTransform, data, App.PreviousScreen != typeof(HomeScreen));
                if (data.mystery)
                {
                    Ui.Label(frame.rectTransform, "?", 30, Palette.Ink2, 0, 0, 64, 64);
                }
            }

            Ui.Fit(Ui.Label(face, "Играть", 28, Color.white, 76, 12, playW - 88, 36), 18);
            var hard = App.Levels.Has(level) && App.Levels.Get(level).hard;
            // Тёплая серия — в подписи кнопки, а не отдельной плашкой: «Уровень 12 · серия ×3».
            var streak = App.Progress.WarmStartFor(level).Any ? $" · серия ×{App.Progress.Streak}" : string.Empty;
            Ui.Fit(Ui.Label(face, (hard ? $"Уровень {level} · сложный" : $"Уровень {level}") + streak, 15, Color.white, 76, 46, playW - 88, 22, false), 10);
            play.OnClick(() => App.StartLevel(level));
            Tweens.Run(play, 1.6f, t => play.transform.localScale = Vector3.one * (1f + 0.025f * Ease.Bump(t)), null, null, 0.6f, true);
        }
    }
}
