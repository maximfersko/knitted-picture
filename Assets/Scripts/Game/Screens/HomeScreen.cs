#nullable enable

using KnittedPicture.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Главная: логотип, ряд «зачем заходить» (подарок дня, звёздный сундук, коллекции —
    /// с красными точками, когда что-то ждёт), текущая коллекция и большая кнопка «Играть»
    /// с эскизом следующей картинки.
    /// </summary>
    public sealed class HomeScreen : ScreenView
    {
        public HomeScreen(App app)
            : base(app, "Home")
        {
            var progress = app.Progress;
            Ui.RoundButton(Root, Icon.Settings, 16, 8).OnClick(() => app.ShowPopup(new SettingsPopup(app)));
            Ui.CoinPill(Root, progress.Coins, W - 16, 8);

            // Логотип: большой клубок пряжи «в работе» со спицами, мягкая тень, название.
            var compact = H < 740f;
            var ball = compact ? 72f : 96f;
            var logoY = compact ? 50f : 56f;
            BuildLogo(logoY, ball);
            var titleY = logoY + ball + 6f;
            Ui.Label(Root, "Вязаная картинка", compact ? 28 : 32, Palette.Ink, 16, titleY, W - 32, 40);
            // Без подзаголовка: на главной только то, что нужно для действия.
            var y = BuildMetaRow(titleY + 52f);
            BuildCollection(y + 16f);
            BuildPlay();
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

        /// <summary>Подарок дня, сундук, задания дня (с 4-го уровня) и коллекции — плитки с отметками.</summary>
        private float BuildMetaRow(float y)
        {
            var progress = App.Progress;
            const float gap = 8f;
            const float h = 64f;
            var missions = progress.MissionsOpen;
            var count = missions ? 4 : 3;
            var w = (W - 32f - gap * (count - 1)) / count;
            var x = 16f;

            var canDaily = progress.CanClaimDaily(App.Today);
            var gift = Tile(x, y, w, h, Icon.Gift, Palette.Danger, "Подарок", canDaily ? "забери!" : "завтра", canDaily ? 1 : 0);
            gift.OnClick(() => App.ShowPopup(new DailyGiftPopup(App, App.ShowHome)));
            x += w + gap;

            var chestReady = progress.CanOpenChest;
            var chestProgress = Mathf.Min(progress.ChestProgress, ProgressService.ChestEvery);
            var chest = Tile(x, y, w, h, Icon.Chest, Palette.CoinShadow, "Сундук",
                chestReady ? "открой!" : $"{chestProgress}/{ProgressService.ChestEvery} ★", chestReady ? 1 : 0,
                (float)chestProgress / ProgressService.ChestEvery);
            chest.OnClick(App.ShowChest);
            x += w + gap;

            if (missions)
            {
                var done = progress.MissionsDone(App.Today);
                var claimed = progress.MissionsClaimed(App.Today);
                var ready = progress.CanClaimMissions(App.Today);
                var tile = Tile(x, y, w, h, Icon.Check, Palette.Primary, "Задания",
                    ready ? "забери!" : claimed ? "завтра" : $"{done}/{DailyMissions.Count}", ready ? 1 : 0,
                    claimed ? -1f : (float)done / DailyMissions.Count);
                tile.OnClick(() => App.ShowPopup(new MissionsPopup(App, App.ShowHome)));
                x += w + gap;
            }

            var pending = progress.PendingClaims;
            var kinds = progress.YarnKinds;
            var collections = Tile(x, y, w, h, Icon.Trophy, Palette.Link, "Коллекции",
                pending > 0 ? "забери!" : string.Empty, pending);
            collections.OnClick(() => App.ShowCollections(FirstPendingTab()));

            return y + h;
        }

        /// <summary>Открыть коллекции там, где ждёт награда.</summary>
        private CollectionsTab FirstPendingTab()
        {
            var progress = App.Progress;
            foreach (var a in Achievements.All)
            {
                if (progress.CanClaimAchievement(a))
                {
                    return CollectionsTab.Achievements;
                }
            }

            foreach (var set in YarnCatalog.Sets)
            {
                if (progress.CanClaimSet(set))
                {
                    return CollectionsTab.Yarn;
                }
            }

            return CollectionsTab.Album;
        }

        /// <summary>
        /// Плитка «зачем заходить» — компактная: только значок. Подпись появляется, только когда там
        /// что-то ждёт игрока («забери!», «открой!»), тогда же красная точка и покачивание.
        /// У сундука и заданий без награды — тонкая полоска прогресса под значком.
        /// </summary>
        private Tap Tile(float x, float y, float w, float h, Icon icon, Color tint, string title, string caption, int badge, float fill = -1f)
        {
            var card = Ui.Card(Root, title, x, y, w, h, 18, Palette.Card, Palette.CardShadow, 3f, true);
            var waiting = badge > 0;
            var iconY = waiting ? h / 2f - 8f : h / 2f - (fill >= 0f ? 4f : 0f);
            var circle = Ui.Image(card, "Back", Art.Circle, Palette.WithAlpha(tint, 0.14f));
            Ui.PlaceCenter(circle.rectTransform, w / 2f, iconY, 38, 38);
            var iconImage = Ui.IconImage(card, icon, tint, w / 2f, iconY, 24);
            if (waiting)
            {
                Ui.Fit(Ui.Label(card, caption, 12, Palette.Danger, 2, h - 22f, w - 4, 16, true), 9);
            }
            else if (fill >= 0f)
            {
                Ui.ProgressBar(card, 16, h - 12f, w - 32, 4, fill, Palette.Coin);
            }

            if (waiting)
            {
                Ui.Badge(card, w - 10, 8, badge);
                var rt = iconImage.rectTransform;
                Tweens.Run(rt, 1.6f, t => rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * Mathf.PI * 4f) * 10f * Ease.Bump(t)), null, null, 0.4f, true);
            }

            return card.gameObject.AddComponent<Tap>();
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
                _pos = Mathf.SmoothDamp(_pos, _target, ref _speed, 0.14f, Mathf.Infinity, dt);
                if (Mathf.Abs(_pos - _target) < 0.0005f)
                {
                    _pos = _target;
                    _speed = 0f;
                }
            }

            ApplyCarousel();
        }

        /// <summary>
        /// Карусель коллекций между плитками и «Играть». Лента карточек едет за пальцем и плавно
        /// доводится до ближайшей; соседние карточки чуть меньше и бледнее. Под лентой — точки,
        /// активная «перетекает» к следующей. Тап по картинке — карточка уровня, текущий — сразу игра.
        /// </summary>
        private void BuildCollection(float y)
        {
            // Только коллекция текущего уровня: без ленты, стрелок и точек. Остальные — в «Коллекциях».
            var progress = App.Progress;
            var bottom = H - 24f - 84f - 16f;
            var available = Mathf.Max(220f, bottom - y);
            var viewport = Ui.Place(Ui.Node(Root, "Collection"), 0, y, W, available);
            _strip = Ui.Place(Ui.Node(viewport, "Strip"), 0, 0, W, available);
            var index = Mathf.Clamp(IndexOf(Collections.Of(progress.CurrentLevel)), 0, Collections.All.Length - 1);
            var card = BuildPageCard(index, out var cardH, true);
            _cards.Add(card);
            var top = Mathf.Max(4f, (available - cardH) / 2f);
            Ui.PlaceCenter(card, W / 2f, top + cardH / 2f, W - 32f, cardH);
            _page = null;
            _target = 0;
            _pos = 0;
        }

        private static int IndexOf(CollectionInfo collection) => System.Array.IndexOf(Collections.All, collection);

        /// <summary>Сдвинуть ленту, отмасштабировать соседей и перетечь точкой — по текущей позиции.</summary>
        private void ApplyCarousel()
        {
            _strip.anchoredPosition = new Vector2(-_pos * W, _strip.anchoredPosition.y);
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
        private RectTransform BuildPageCard(int index, out float cardH, bool single = false)
        {
            var progress = App.Progress;
            var collection = Collections.All[index];
            var cardW = W - 32f;
            const float gap = 8f;
            var tile = Mathf.Min(64f, Mathf.Floor((cardW - 24f - gap * (collection.Count - 1)) / collection.Count));
            var tilesW = collection.Count * tile + (collection.Count - 1) * gap;
            var stripY = 58f + tile + MiniPicture.StarsOverhang + 34f;
            cardH = stripY + 6f;
            var card = Ui.Card(_strip, "Collection" + index, (single ? 0 : index * W) + 16f, 0, cardW, cardH, 24, Palette.Card, Palette.CardShadow, 3f, true);

            var done = 0;
            var stars = 0;
            for (var id = collection.FirstLevel; id <= collection.LastLevel; id++)
            {
                done += progress.IsCompleted(id) ? 1 : 0;
                stars += progress.StarsOf(id);
            }

            Ui.Fit(Ui.Label(card, collection.Name, 18, Palette.Ink, 56, 10, cardW - 112, 24), 13);
            // Награда коллекции на главной не занимает отдельную полосу: когда её можно забрать,
            // подпись под названием превращается в золотую кнопку. Прогресс и награда — в альбоме.
            if (progress.CanClaimCollection(index))
            {
                var claim = Ui.Box(card, "Claim", 11, Palette.CoinSoft, true).rectTransform;
                Ui.PlaceCenter(claim, cardW / 2f, 42, Mathf.Min(cardW - 112, 200), 22);
                Ui.Outline(claim, 11, Palette.Coin, 1.5f);
                Ui.Label(claim, "Забрать награду", 12, Palette.CoinShadow, 0, 0, Mathf.Min(cardW - 112, 200), 22);
                var collectionIndex = index;
                claim.gameObject.AddComponent<Tap>().OnClick(() => CollectionReward.Claim(App, collectionIndex, App.ShowHome));
            }
            else
            {
                Ui.Fit(Ui.Label(card, $"{done} из {collection.Count} · {stars}/{collection.Count * 3} ★", 13, Palette.Ink2, 56, 32, cardW - 112, 18, false), 10);
            }
            if (!single)
            {
                var page = index;
                Arrow(card, 10, index > 0, () => Turn(page - 1), false);
                Arrow(card, cardW - 50, index < Collections.All.Length - 1, () => Turn(page + 1), true);
            }

            var x0 = (cardW - tilesW) / 2f;
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

        private static void Arrow(RectTransform card, float x, bool enabled, System.Action action, bool forward)
        {
            var button = Ui.RoundButton(card, Icon.Back, x, 8, 40);
            if (forward)
            {
                // Отражаем только значок: масштаб самой кнопки нужен для отклика на нажатие.
                var icon = button.transform.GetChild(button.transform.childCount - 1);
                icon.localScale = new Vector3(-1f, 1f, 1f);
            }

            var group = button.gameObject.AddComponent<CanvasGroup>();
            group.alpha = enabled ? 1f : 0.3f;
            button.Interactable = enabled;
            button.OnClick(action);
        }

        /// <summary>
        /// Кнопка «Узор дня»: эскиз сегодняшней картинки, подпись и серия дней. Не связан сегодня —
        /// красная точка; связан — галочка и «завтра новый», тап объясняет, когда ждать следующий.
        /// </summary>
        private void BuildPattern(float x, float y, float size)
        {
            var progress = App.Progress;
            var done = progress.PatternDone(App.Today);
            var streak = progress.PatternStreak(App.Today);
            var card = Ui.Card(Root, "Pattern", x, y, size, size, 22, done ? Palette.Surface : Palette.Card, Palette.CardShadow, 5f, true);
            var level = App.TodayPattern();
            var frame = Ui.Box(card, "Preview", 10, Palette.Surface).rectTransform;
            Ui.PlaceCenter(frame, size / 2f, 30, 44, 44);
            MiniPicture.Create(frame, level, 22, 22, 38, faded: !done);
            if (done)
            {
                var mark = Ui.Image(card, "Done", Art.Circle, Palette.Primary);
                Ui.PlaceCenter(mark.rectTransform, size / 2f + 20, 48, 20, 20);
                Ui.IconImage(card, Icon.Check, Color.white, size / 2f + 20, 48, 12);
            }
            else
            {
                Ui.Badge(card, size - 10, 8, 0);
            }

            Ui.Fit(Ui.Label(card, "Узор дня", 12, Palette.Ink, 2, 56, size - 4, 16), 9);
            Ui.Fit(Ui.Label(card, streak > 0 ? $"серия {streak} дн." : "новый!", 11, streak > 0 ? Palette.CoinShadow : Palette.Danger, 2, 70, size - 4, 14,
                streak > 0), 8);
            card.gameObject.AddComponent<Tap>().OnClick(() =>
            {
                if (progress.PatternDone(App.Today))
                {
                    App.ShowPopup(new InfoPopup(App, "Узор дня связан",
                        $"Новый узор появится завтра. Серия: {progress.PatternStreak(App.Today)} дн. подряд, каждый {DailyPattern.YarnEvery}-й день — моток в подарок."));
                    return;
                }

                App.StartPattern();
            });
        }

        /// <summary>«Играть» с эскизом следующей картинки — видно, ради чего идёшь дальше.</summary>
        private void BuildPlay()
        {
            var progress = App.Progress;
            var buttonY = H - 24f - 84f;

            // «Узор дня» — квадратная кнопка справа от «Играть»: новый повод зайти каждый день.
            var playW = W - 40;
            if (progress.PatternOpen)
            {
                const float size = 84f;
                playW -= size + 10f;
                BuildPattern(W - 20 - size, buttonY, size);
            }

            if (progress.AllDone)
            {
                var done = Ui.Button(Root, "", 20, buttonY, playW, 84, Palette.Primary, Palette.PrimaryShadow, Color.white);
                Ui.Fit(Ui.Label((RectTransform)done.transform, "Все картинки связаны!", 22, Color.white, 8, 14, playW - 16, 30), 14);
                Ui.Fit(Ui.Label((RectTransform)done.transform, "Собирай звёзды и пряжу в коллекциях", 14, Color.white, 8, 46, playW - 16, 22, false), 10);
                done.OnClick(App.ShowAlbum);
                return;
            }

            var level = progress.CurrentLevel;
            var play = Ui.Button(Root, "", 20, buttonY, playW, 84, Palette.Primary, Palette.PrimaryShadow, Color.white);
            var face = (RectTransform)play.transform;
            if (App.Levels.Has(level))
            {
                var data = App.Levels.Get(level);
                var frame = Ui.Box(face, "Preview", 14, Palette.Card);
                Ui.Place(frame.rectTransform, 12, 10, 64, 64);
                MiniPicture.Create(frame.rectTransform, data, 32, 32, 54, faded: true);
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
