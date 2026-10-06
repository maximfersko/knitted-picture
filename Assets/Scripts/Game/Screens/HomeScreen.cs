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

            // Логотип: клубок и название.
            var compact = H < 740f;
            var ball = compact ? 64f : 84f;
            var logoY = compact ? 56f : 66f;
            var yarn = Ui.Image(Root, "Logo", Art.YarnBall, Palette.Hex("#FF6FA5"));
            Ui.PlaceCenter(yarn.rectTransform, W / 2f, logoY + ball / 2f, ball, ball);
            Tweens.Run(yarn, 3.2f, t => yarn.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * Mathf.PI * 2f) * 4f), null, null, 0, true);
            var titleY = logoY + ball + 6f;
            Ui.Label(Root, "Вязаная картинка", compact ? 28 : 32, Palette.Ink, 16, titleY, W - 32, 40);
            // Без подзаголовка: на главной только то, что нужно для действия.
            var y = BuildMetaRow(titleY + 52f);
            BuildCollection(y + 16f);
            BuildPlay();
        }

        /// <summary>Подарок дня, сундук, задания дня (с 4-го уровня) и коллекции — плитки с отметками.</summary>
        private float BuildMetaRow(float y)
        {
            var progress = App.Progress;
            const float gap = 8f;
            const float h = 92f;
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
                $"пряжа {kinds}/{YarnCatalog.Count}", pending);
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

        private Tap Tile(float x, float y, float w, float h, Icon icon, Color tint, string title, string caption, int badge, float fill = -1f)
        {
            var card = Ui.Card(Root, title, x, y, w, h, 20, Palette.Card, Palette.CardShadow, 3f, true);
            var circle = Ui.Image(card, "Back", Art.Circle, Palette.WithAlpha(tint, 0.14f));
            Ui.PlaceCenter(circle.rectTransform, w / 2f, 30, 44, 44);
            var iconImage = Ui.IconImage(card, icon, tint, w / 2f, 30, 26);
            Ui.Fit(Ui.Label(card, title, 14, Palette.Ink, 2, 54, w - 4, 18), 10);
            if (fill >= 0f && badge == 0)
            {
                Ui.ProgressBar(card, 14, 76, w - 28, 6, fill, Palette.Coin);
            }
            else
            {
                Ui.Fit(Ui.Label(card, caption, 12, badge > 0 ? Palette.Danger : Palette.Ink2, 2, 71, w - 4, 16, badge > 0), 9);
            }

            if (badge > 0)
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
            var progress = App.Progress;
            var warm = progress.WarmStartFor(progress.CurrentLevel);
            var showStreak = warm.Any && !progress.AllDone;
            var bottom = H - 24f - 84f - 16f - (showStreak ? 44f : 0f);
            var available = Mathf.Max(220f, bottom - y);
            var viewport = Ui.Place(Ui.Node(Root, "Carousel"), 0, y, W, available);
            var hit = Ui.Image(viewport, "Hit", null, Color.clear, true);
            Ui.Stretch(hit.rectTransform);
            viewport.gameObject.AddComponent<RectMask2D>();
            _strip = Ui.Place(Ui.Node(viewport, "Strip"), 0, 0, W * Collections.All.Length, available);

            var cardH = 0f;
            for (var i = 0; i < Collections.All.Length; i++)
            {
                var card = BuildPageCard(i, out cardH);
                _cards.Add(card);
            }

            // Карточки и точки — по центру свободного места.
            const float dotsGap = 18f;
            var blockH = cardH + dotsGap + 8f;
            var top = Mathf.Max(4f, (available - blockH) / 2f);
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

            // Серия побед подряд: чем длиннее, тем больше монет — повод не перезапускать.
            if (showStreak)
            {
                var chipW = Mathf.Min(W - 32f, 300f);
                var chip = Ui.Card(Root, "Streak", (W - chipW) / 2f, bottom + 4f, chipW, 32, 16, Palette.CoinSoft, Palette.CoinSoft, 0f);
                Ui.IconImage(chip, Icon.Star, Palette.CoinShadow, 22, 16, 18);
                // Что даст серия на старте следующего уровня — повод не перезапускать и вернуться за продолжением.
                Ui.Fit(Ui.Label(chip, $"Тёплая серия ×{progress.Streak}: на старте {GameScreen.WarmGifts(warm)}", 14, Palette.Ink, 36, 0, chipW - 44, 32,
                    true, TextAnchor.MiddleLeft), 10);
            }
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

        private void BuildDots(RectTransform parent, float y)
        {
            var n = Collections.All.Length;
            const float step = 18f;
            _dotX = new float[n];
            for (var i = 0; i < n; i++)
            {
                _dotX[i] = W / 2f + (i - (n - 1) / 2f) * step;
                var dot = Ui.Box(parent, "Dot", 4, Palette.SlotEmpty, true);
                Ui.PlaceCenter(dot.rectTransform, _dotX[i], y, 22, 22);
                dot.color = Color.clear;
                var visible = Ui.Box(dot.rectTransform, "Visible", 4, Palette.SlotEmpty);
                Ui.PlaceCenter(visible.rectTransform, 11, 11, 8, 8);
                var target = i;
                dot.gameObject.AddComponent<Tap>().OnClick(() => Turn(target));
            }

            _indicator = Ui.Box(parent, "Indicator", 4, Palette.Ink).rectTransform;
            Ui.Place(_indicator, _dotX[0] - 11f, y - 4f, 22, 8);
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
            var page = index;
            Arrow(card, 10, index > 0, () => Turn(page - 1), false);
            Arrow(card, cardW - 50, index < Collections.All.Length - 1, () => Turn(page + 1), true);

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

        /// <summary>«Играть» с эскизом следующей картинки — видно, ради чего идёшь дальше.</summary>
        private void BuildPlay()
        {
            var progress = App.Progress;
            var buttonY = H - 24f - 84f;
            if (progress.AllDone)
            {
                var done = Ui.Button(Root, "", 20, buttonY, W - 40, 84, Palette.Primary, Palette.PrimaryShadow, Color.white);
                Ui.Label((RectTransform)done.transform, "Все картинки связаны!", 22, Color.white, 0, 14, W - 40, 30);
                Ui.Label((RectTransform)done.transform, "Собирай звёзды и пряжу в коллекциях", 14, Color.white, 0, 46, W - 40, 22, false);
                done.OnClick(App.ShowAlbum);
                return;
            }

            var level = progress.CurrentLevel;
            var play = Ui.Button(Root, "", 20, buttonY, W - 40, 84, Palette.Primary, Palette.PrimaryShadow, Color.white);
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

            Ui.Label(face, "Играть", 28, Color.white, 76, 12, W - 40 - 88, 36);
            var hard = App.Levels.Has(level) && App.Levels.Get(level).hard;
            Ui.Label(face, hard ? $"Уровень {level} · сложный" : $"Уровень {level}", 15, Color.white, 76, 46, W - 40 - 88, 22, false);
            play.OnClick(() => App.StartLevel(level));
            Tweens.Run(play, 1.6f, t => play.transform.localScale = Vector3.one * (1f + 0.025f * Ease.Bump(t)), null, null, 0.6f, true);
        }
    }
}
