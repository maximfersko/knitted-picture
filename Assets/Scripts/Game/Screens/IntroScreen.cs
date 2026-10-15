#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using KnittedPicture.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Вступление на первом запуске (и «Как играть» в настройках): девять коротких сцен.
    /// Вязка в сценах идёт на настоящем ядре игры, поэтому показанное совпадает с правилами.
    /// Три сцены интерактивные: тапнуть клубок, особые клубки, бустеры. Внизу шкала из сегментов,
    /// «Пропустить» всегда под рукой.
    /// </summary>
    public sealed class IntroScreen : ScreenView
    {
        private const float BeadFlight = 0.22f;

        private static readonly string[] Titles =
        {
            L.T("Вязаная картинка"),
            L.T("Тапни клубок"),
            L.T("Станок вяжет сам"),
            L.T("Места мало"),
            L.T("Чужая пряжа"),
            L.T("Темп и комбо"),
            L.T("Особые клубки"),
            L.T("Бустеры"),
            L.T("Звёзды и награды"),
        };

        private static readonly string[] Texts =
        {
            L.T("Клубки пряжи превращаются в картинку петля за петлёй"),
            L.T("Попробуй: тапни передний клубок, и он поедет на станок"),
            L.T("Фон уже связан, вяжется только картинка. Столбики растут снизу вверх, клетка в рамке следующая"),
            L.T("Места кончились, а работы нет? Это затор. Бери клубки, которым сразу есть дело"),
            L.T("Клубок в сеточку картинке не нужен. На станке он займёт место насовсем"),
            L.T("Ставь клубки, пока станок вяжет: комбо растёт. На ×5 включается «Разгон», и станок вяжет вдвое быстрее"),
            L.T("Тапни любой клубок и узнай, как он работает"),
            L.T("Тапни бустер и посмотри, что он делает. Бустеры дают в подарках, сундуках и за монеты"),
            L.T("Три звезды: без бустеров и спасений. Звёзды копят сундук, а новые картинки дарят пряжу"),
        };

        /// <summary>Сцены, где игрок сам тапает элементы: тап по пустому месту сцены их не листает.</summary>
        private static readonly int[] Interactive = { 1, 6, 7 };

        /// <summary>
        /// Какие сцены показываем: только основа — тап, станок вяжет, затор. Чужую пряжу, комбо, особые клубки,
        /// бустеры и звёзды объясняют подсказки на тех уровнях, где это встречается впервые.
        /// </summary>
        private static readonly int[] Shown = { 1, 2, 3 };

        /// <summary>Номер показанной сцены по порядку (для шкалы сегментов).</summary>
        private int Step => System.Array.IndexOf(Shown, _page);

        // Цвета клубков — из общего набора YarnColors, как в уровнях: без оттенков одного цвета.
        private static readonly Color Cream = Palette.Hex("#FFFFFF");

        private static readonly Color Pink = Palette.Hex("#FF6FB7");
        private static readonly Color Sky = Palette.Hex("#4CC3F5");
        private static readonly Color Sun = Palette.Hex("#FFD419");
        private static readonly Color Leaf = Palette.Hex("#2DB34A");
        private static readonly Color Navy = Palette.Hex("#2552D9");

        private readonly RectTransform _stage;
        private readonly float _sw;
        private readonly float _sh;
        private readonly Text _title;
        private readonly Text _text;
        private readonly RectTransform[] _segments = new RectTransform[Shown.Length];
        private float _segmentW;
        private readonly Text _nextLabel;
        private RectTransform _fx;
        private RectTransform? _scene;
        private Demo? _demo;
        private int _page = -1;

        public IntroScreen(App app)
            : base(app, "Intro")
        {
            Ui.TextButton(Root, L.T("Пропустить"), W - 136, 8, 120, 44, Palette.Ink2, 15).OnClick(Finish);

            _sw = W - 32f;
            _sh = Mathf.Clamp(H * 0.5f, 300f, 430f);
            var stageCard = Ui.Card(Root, "Stage", 16, 60, _sw, _sh, 28, Palette.Card, Palette.CardShadow);
            _stage = Ui.Stretch(Ui.Node(stageCard, "Content"));
            var hit = Ui.Image(_stage, "Hit", null, Color.clear, true);
            Ui.Stretch(hit.rectTransform);
            hit.gameObject.AddComponent<Tap>().OnClick(() =>
            {
                if (System.Array.IndexOf(Interactive, _page) < 0)
                {
                    Next();
                }
            }).PressFeedback = false;

            var y = 60f + _sh + 22f;
            _title = Ui.Label(Root, "", 28, Palette.Ink, 20, y, W - 40, 38);
            _text = Ui.Label(Root, "", 16, Palette.Ink2, 28, y + 44f, W - 56, 66, false, TextAnchor.UpperCenter);
            _text.lineSpacing = 1.05f;

            BuildSegments(H - 24f - 64f - 26f);

            var next = Ui.Button(Root, "", 20, H - 24f - 64f, W - 40, 64, Palette.Primary, Palette.PrimaryShadow, Color.white, 22);
            _nextLabel = Ui.Label((RectTransform)next.transform, L.T("Дальше"), 22, Color.white, 0, 0, W - 40, 64);
            next.OnClick(Next);

            _fx = Ui.Layer(Ui.Stretch(Ui.Node(Root, "Fx")), false);
            Go(Shown[0]);
        }

        public override void Tick(float dt)
        {
            var demo = _demo;
            if (demo == null || !demo.Running || demo.Owner == null)
            {
                return;
            }

            demo.Clock += dt;
            if (demo.Clock < demo.Interval)
            {
                return;
            }

            demo.Clock = 0;
            var step = demo.Game.Step();
            foreach (var stitch in step.Stitches)
            {
                demo.Picture.MarkPending(stitch.X, stitch.Y);
                var to = demo.Picture.CellWorld(stitch.X, stitch.Y);
                var color = demo.Colors[stitch.Color];
                var x = stitch.X;
                var y = stitch.Y;
                var picture = demo.Picture;
                Fx.Bead(_fx, demo.Source(stitch.Slot), to, color, 11f, BeadFlight, () =>
                {
                    if (picture.Land(x, y))
                    {
                        Fx.Sparkle(_fx, to, color);
                        Sfx.Play(Sound.Stitch, 0.35f);
                    }
                });
                demo.Wobble?.Invoke(stitch.Slot);
            }

            foreach (var done in step.Completed)
            {
                demo.Done?.Invoke(done);
            }

            foreach (var x in step.Stitches.Select(s => s.X).Distinct())
            {
                demo.Picture.RefreshColumn(x);
            }

            demo.Sync?.Invoke();

            if (step.Status == GameStatus.Won)
            {
                demo.Running = false;
                var won = demo;
                Tweens.Delay(won.Owner, BeadFlight + 0.12f, () =>
                {
                    won.Picture.Refresh();
                    Sfx.Play(Sound.Unlock, 0.6f);
                    won.Picture.Wave(() => won.Won?.Invoke());
                });
            }
            else if (!step.Progress)
            {
                demo.Running = false;
                var idle = demo;
                Tweens.Delay(idle.Owner, BeadFlight + 0.12f, () =>
                {
                    if (idle.Idle != null)
                    {
                        idle.Idle();
                    }
                    else
                    {
                        Loop(idle.Owner, 0.8f);
                    }
                });
            }
        }

        /// <summary>У экрана своя постановка входа: волна блоков ей бы мешала.</summary>
        protected override bool CascadeOnEnter => false;

        public override bool OnBack()
        {
            Finish();
            return true;
        }

        // ---------- Листание ----------

        private void Next()
        {
            if (Step >= Shown.Length - 1)
            {
                Finish();
            }
            else
            {
                Go(Shown[Step + 1]);
            }
        }

        private void Finish()
        {
            App.FinishIntro();
        }

        private void Go(int page)
        {
            var old = _scene;
            if (old != null)
            {
                var group = old.GetComponent<CanvasGroup>();
                Tweens.Run(old, 0.22f, t =>
                {
                    old.anchoredPosition = new Vector2(-50f * t, 0);
                    group.alpha = 1f - t;
                }, Ease.OutCubic, () => UnityEngine.Object.Destroy(old.gameObject));
            }

            _page = page;
            _title.text = Titles[page];
            _text.text = Texts[page];
            _nextLabel.text = page == Shown[Shown.Length - 1] ? L.T("Начать вязать") : L.T("Дальше");
            Tweens.Scale(_title.transform, 0.9f, 1f, 0.25f);
            UpdateSegments();
            BuildScene(true);
        }

        private void BuildScene(bool slideIn)
        {
            _demo = null;
            var scene = Ui.Stretch(Ui.Node(_stage, "Scene" + _page));
            var group = scene.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = System.Array.IndexOf(Interactive, _page) >= 0;
            _scene = scene;
            if (slideIn)
            {
                Tweens.Run(scene, 0.28f, t =>
                {
                    scene.anchoredPosition = new Vector2(50f * (1f - t), 0);
                    group.alpha = t;
                }, Ease.OutCubic);
            }

            switch (_page)
            {
                case 0: SceneWelcome(scene); break;
                case 1: SceneTap(scene); break;
                case 2: SceneKnit(scene); break;
                case 3: SceneJam(scene); break;
                case 4: SceneForeign(scene); break;
                case 5: SceneCombo(scene); break;
                case 6: SceneSpools(scene); break;
                case 7: SceneBoosters(scene); break;
                default: SceneStars(scene); break;
            }
        }

        /// <summary>Сцена доиграла — немного подождать и показать её снова.</summary>
        private void Loop(RectTransform scene, float delay)
        {
            Tweens.Delay(scene, delay, () =>
            {
                if (_scene != scene)
                {
                    return;
                }

                var group = scene.GetComponent<CanvasGroup>();
                Tweens.Fade(group, 1f, 0f, 0.2f, () =>
                {
                    if (_scene != scene)
                    {
                        return;
                    }

                    UnityEngine.Object.Destroy(scene.gameObject);
                    BuildScene(false);
                    var fresh = _scene!.GetComponent<CanvasGroup>();
                    Tweens.Fade(fresh, 0f, 1f, 0.2f);
                });
            });
        }

        /// <summary>Шкала из сегментов: пройденные — зелёные, текущий заполняется, впереди — серые.</summary>
        private void BuildSegments(float y)
        {
            const float gap = 6f;
            var total = Mathf.Min(W - 64f, 320f);
            _segmentW = (total - gap * (_segments.Length - 1)) / _segments.Length;
            var x = W / 2f - total / 2f;
            for (var i = 0; i < _segments.Length; i++)
            {
                var track = Ui.Box(Root, "Segment", 3, Palette.SlotEmpty);
                Ui.Place(track.rectTransform, x + i * (_segmentW + gap), y, _segmentW, 6);
                var fill = Ui.Box(track.rectTransform, "Fill", 3, Palette.Primary).rectTransform;
                Ui.Place(fill, 0, 0, 0, 6);
                _segments[i] = fill;
            }
        }

        private void UpdateSegments()
        {
            for (var i = 0; i < _segments.Length; i++)
            {
                var fill = _segments[i];
                if (i < Step)
                {
                    fill.sizeDelta = new Vector2(_segmentW, 6);
                }
                else if (i > Step)
                {
                    fill.sizeDelta = new Vector2(0, 6);
                }
                else
                {
                    Tweens.Run(fill, 0.35f, t => fill.sizeDelta = new Vector2(Mathf.Max(6f, _segmentW * t), 6), Ease.OutCubic);
                }
            }
        }

        // ---------- Сцены ----------

        /// <summary>Два клубка вкатываются и вывязывают сердечко.</summary>
        private void SceneWelcome(RectTransform scene)
        {
            var rows = new[] { "110011", "111111", "111111", "011110", "001100", "000000" };
            var colors = new[] { Sky, Pink };
            var game = DemoGame(rows, colors, 2, false, 0, 1);
            var picture = Picture(scene, game, colors, 26f, _sw / 2f, 22f);

            var ballSize = 64f;
            var blue = Ui.Image(scene, "Blue", Art2.YarnBall(Sky), Color.white);
            var pink = Ui.Image(scene, "Pink", Art2.YarnBall(Pink), Color.white);
            var left = new Vector2(_sw / 2f - 104f, _sh - 62f);
            var right = new Vector2(_sw / 2f + 104f, _sh - 62f);
            Roll(blue.rectTransform, new Vector2(-60f, left.y), left, ballSize, 1);
            Roll(pink.rectTransform, new Vector2(_sw + 60f, right.y), right, ballSize, -1);

            var demo = new Demo(game, picture, colors, scene, 0.06f, slot => Ui.WorldCenter(slot == 0 ? blue.rectTransform : pink.rectTransform))
            {
                Wobble = slot => Tweens.Punch(slot == 0 ? blue.rectTransform : pink.rectTransform, 0.05f, 0.1f),
                Won = () =>
                {
                    Fx.Poof(_fx, Ui.WorldCenter(picture.Root), Pink, picture.Width * 0.8f);
                    Sfx.Play(Sound.Star, 0.7f);
                    Tweens.Punch(picture.Root, 0.1f, 0.35f);
                    Loop(scene, 1.8f);
                },
            };
            Tweens.Delay(scene, 0.9f, () => Start(demo));
        }

        /// <summary>
        /// Интерактив: игрок сам тапает передний клубок колонки — он летит на станок, колонка
        /// подтягивается, следующий становится передним. Рука подсказывает первый тап.
        /// </summary>
        private void SceneTap(RectTransform scene)
        {
            var frames = Loom(scene, 3, 46f);
            const float size = 52f;
            var cx = _sw / 2f;
            var front = _sh - 170f;
            var colors = new[] { Pink, Sky, Sun };
            var queue = new List<SpoolView>();
            for (var i = 0; i < colors.Length; i++)
            {
                var view = Spool(scene, colors[i], 6 + i * 2, size);
                Ui.PlaceCenter(view.Root, cx, front + i * 60f, size, size);
                view.Root.GetComponent<CanvasGroup>().alpha = i == 0 ? 1f : 0.5f;
                queue.Add(view);
            }

            // Рука «нажимает» на передний клубок, пока игрок не тапнет сам.
            var hand = Hand(scene, new Vector2(cx + 22f, front + 30f));
            var handAt = hand.anchoredPosition;
            Tweens.Run(hand, 0.9f, t =>
            {
                hand.anchoredPosition = handAt + new Vector2(6f, -6f) * Ease.Bump(t);
                hand.localScale = Vector3.one * (1f - 0.1f * Ease.Bump(t));
            }, null, null, 0f, true);

            var placed = 0;
            void Arm()
            {
                var current = queue[0];
                current.Tap.ClickSound = false;
                current.Tap.OnClick(() =>
                {
                    if (queue.Count == 0 || queue[0] != current || placed >= frames.Length)
                    {
                        return;
                    }

                    if (hand != null)
                    {
                        UnityEngine.Object.Destroy(hand.gameObject);
                        hand = null!;
                    }

                    queue.RemoveAt(0);
                    var slot = frames[placed++];
                    Sfx.Play(Sound.Pick, 0.8f);
                    var target = Ui.WorldCenter(slot);
                    var root = current.Root;
                    Tweens.Move(root, Ui.WorldCenter(root), target, 0.32f, Ease.OutCubic, () =>
                    {
                        Tweens.Scale(root, 1.2f, 0.85f, 0.2f);
                        Fx.Sparkle(_fx, target, colors[placed - 1], 6, 30f);
                    });

                    for (var i = 0; i < queue.Count; i++)
                    {
                        var spool = queue[i].Root;
                        var from = spool.anchoredPosition;
                        var group = spool.GetComponent<CanvasGroup>();
                        var first = i == 0;
                        Tweens.Run(spool, 0.3f, t =>
                        {
                            spool.anchoredPosition = Vector2.Lerp(from, from + new Vector2(0, 60f), t);
                            if (first)
                            {
                                group.alpha = Mathf.Lerp(0.5f, 1f, t);
                            }
                        }, Ease.OutCubic, null, 0.1f);
                    }

                    if (placed == 1)
                    {
                        Fx.FloatText(_fx, target + new Vector3(0, 40f * _fx.lossyScale.y, 0), L.T("Вот так!"), Palette.Primary, 22f);
                    }

                    if (queue.Count == 0)
                    {
                        Tweens.Delay(scene, 0.5f, () => Fx.FloatText(_fx, Ui.WorldCenter((RectTransform)frames[0].parent), L.T("Станок полон!"), Palette.Ink, 22f));
                        Loop(scene, 1.6f);
                    }
                    else
                    {
                        Arm();
                    }
                });
            }

            Arm();
        }

        /// <summary>Станок с двумя клубками вяжет: фронт столбиков подсвечен, бусины летят к петлям.</summary>
        private void SceneKnit(RectTransform scene)
        {
            var rows = new[] { "01010", "10101", "01010", "10101" };
            var colors = new[] { Pink, Sky };
            var game = DemoGame(rows, colors, 2, false, 0, 1);
            var picture = Picture(scene, game, colors, 30f, _sw / 2f, 20f);
            var loomY = 20f + picture.Height + 30f;
            var frames = Loom(scene, 2, loomY);
            var chip = Ui.Card(scene, "Front", _sw / 2f - 92f, loomY - 26f, 184, 22, 11, Palette.Track, Palette.Track, 0f);
            Ui.Label(chip, L.T("в рамке: вяжутся следующими"), 12, Palette.Ink2, 0, 0, 184, 22);

            var views = SlotViews(frames, game, colors);
            var demo = new Demo(game, picture, colors, scene, 0.12f, slot => Ui.WorldCenter(frames[slot]))
            {
                Wobble = slot => Wobble(views[slot]),
                Sync = () => SyncSlots(views, game, frames, colors),
                Won = () => Loop(scene, 1.4f),
            };
            Tweens.Delay(scene, 0.5f, () => Start(demo));
        }

        /// <summary>Три «не тех» клубка забивают станок — затор. Потом правильный клубок сразу работает.</summary>
        private void SceneJam(RectTransform scene)
        {
            var rows = new[] { "11111", "11111", "00000" };
            var colors = new[] { Pink, Sky };
            var game = DemoGame(rows, colors, 3, false);
            var picture = Picture(scene, game, colors, 28f, _sw / 2f, 22f);
            var loomY = 22f + picture.Height + 26f;
            var frames = Loom(scene, 3, loomY);
            var fieldY = loomY + 74f + 52f;
            var wrong = new[] { Sun, Leaf, Navy };
            var flown = new List<RectTransform>();
            for (var i = 0; i < wrong.Length; i++)
            {
                var view = Spool(scene, wrong[i], 5, 44f);
                Ui.PlaceCenter(view.Root, _sw / 2f + (i - 1) * 64f, fieldY, 44, 44);
                var index = i;
                var root = view.Root;
                flown.Add(root);
                Tweens.Delay(root, 0.5f + i * 0.45f, () =>
                {
                    Sfx.Play(Sound.Pick, 0.6f);
                    Tweens.Move(root, Ui.WorldCenter(root), Ui.WorldCenter(frames[index]), 0.3f, Ease.OutCubic);
                });
            }

            Tweens.Delay(scene, 0.5f + 3 * 0.45f + 0.2f, () =>
            {
                Sfx.Play(Sound.Jam, 0.8f);
                var loom = (RectTransform)frames[0].parent;
                Tweens.Shake(loom, 9f, 0.4f);
                foreach (var frame in frames)
                {
                    var face = frame.GetComponent<Image>();
                    Tweens.Run(face, 0.6f, t => face.color = Color.Lerp(Palette.DangerSoft, Color.white, t));
                }

                Fx.FloatText(_fx, Ui.WorldCenter(loom), L.T("Затор!"), Palette.Danger, 26f);
            });

            Tweens.Delay(scene, 0.5f + 3 * 0.45f + 1.4f, () =>
            {
                foreach (var spool in flown)
                {
                    Fx.Poof(_fx, Ui.WorldCenter(spool), Palette.Ink3, 40f);
                    UnityEngine.Object.Destroy(spool.gameObject);
                }

                var right = Spool(scene, Pink, 5, 44f);
                Ui.PlaceCenter(right.Root, _sw / 2f, fieldY, 44, 44);
                Tweens.Scale(right.Root, 0.3f, 1f, 0.3f);
                var hand = Hand(scene, new Vector2(_sw / 2f + 90f, fieldY + 80f));
                var press = new Vector2(_sw / 2f + 18f, fieldY + 26f);
                Tweens.Run(hand, 0.45f, t => hand.anchoredPosition = Vector2.Lerp(new Vector2(_sw / 2f + 90f, -(fieldY + 80f)), new Vector2(press.x, -press.y), t),
                    Ease.InOutCubic, () =>
                    {
                        Tweens.Run(hand, 0.15f, t => hand.localScale = Vector3.one * (1f - 0.15f * Ease.Bump(t)));
                        Sfx.Play(Sound.Pick, 0.8f);
                        UnityEngine.Object.Destroy(right.Root.gameObject);
                        game.TapColumn(0);
                        var views = SlotViews(frames, game, colors);
                        var demo = new Demo(game, picture, colors, scene, 0.08f, slot => Ui.WorldCenter(frames[slot]))
                        {
                            Wobble = slot => Wobble(views[slot]),
                            Sync = () => SyncSlots(views, game, frames, colors),
                            Idle = () =>
                            {
                                Sfx.Play(Sound.Star, 0.7f);
                                Fx.FloatText(_fx, Ui.WorldCenter((RectTransform)frames[0].parent), L.T("Вот так!"), Palette.Primary, 24f);
                                Loop(scene, 1.6f);
                            },
                        };
                        Start(demo);
                        Tweens.Run(hand, 0.4f, t => hand.anchoredPosition = Vector2.Lerp(new Vector2(press.x, -press.y), new Vector2(_sw + 40f, -(fieldY + 90f)), t), Ease.InOutCubic, null, 0.2f);
                    });
            });
        }

        /// <summary>Клубки ставятся один за другим без простоя — комбо растёт, в конце — три звезды.</summary>
        private void SceneCombo(RectTransform scene)
        {
            var rows = new[] { "101010", "010101", "101010", "010101" };
            var colors = new[] { Pink, Sky };
            var game = DemoGame(rows, colors, 2, true, 0, 1);
            var picture = Picture(scene, game, colors, 28f, _sw / 2f, 56f);
            var loomY = 56f + picture.Height + 26f;
            var frames = Loom(scene, 2, loomY);
            var views = SlotViews(frames, game, colors);
            // Два клубка уже на станке — серия начинается с ×2.
            var combo = 2;
            var next = 2;
            var demo = new Demo(game, picture, colors, scene, 0.07f, slot => Ui.WorldCenter(frames[slot]))
            {
                Wobble = slot => Wobble(views[slot]),
                Sync = () => SyncSlots(views, game, frames, colors),
            };
            demo.Done = done =>
            {
                if (next < game.ColumnCount && game.Column(next).Count > 0)
                {
                    game.TapColumn(next++);
                    demo.Running = true;
                    combo++;
                    Fx.FloatText(_fx, Ui.WorldCenter((RectTransform)frames[0].parent) + new Vector3(0, 40f * _fx.lossyScale.y, 0),
                        L.F("Комбо ×{0}", combo), combo >= 4 ? Palette.Primary : Palette.Link, 22f);
                    Sfx.Play(Sound.Combo, 0.5f);
                }
            };
            demo.Won = () =>
            {
                for (var i = 0; i < 3; i++)
                {
                    var star = Ui.Image(scene, "Star", Art.Icon(Icon.Star), Palette.Coin);
                    var x = _sw / 2f + (i - 1) * 52f;
                    Ui.PlaceCenter(star.rectTransform, x, 28f - (i == 1 ? 4f : 0f), i == 1 ? 46f : 38f, i == 1 ? 46f : 38f);
                    star.rectTransform.localScale = Vector3.zero;
                    var rt = star.rectTransform;
                    Tweens.Delay(rt, i * 0.2f, () =>
                    {
                        Sfx.Play(Sound.Star, 0.7f);
                        Tweens.Scale(rt, 0.2f, 1f, 0.3f);
                        Fx.Sparkle(_fx, Ui.WorldCenter(rt), Palette.Coin, 8, 30f);
                    });
                }

                Loop(scene, 2.2f);
            };
            Tweens.Delay(scene, 0.5f, () => Start(demo));
        }

        /// <summary>
        /// Клубок цвета ткани уезжает на станок и встаёт с красным ✕: работы для него нет.
        /// Следом розовый клубок вяжет рисунок, а чужой так и занимает слот.
        /// </summary>
        private void SceneForeign(RectTransform scene)
        {
            var rows = new[] { "00000", "01010", "01110", "00100" };
            var canvas = new[] { "11111", "10101", "10001", "11011" };
            var colors = new[] { Cream, Pink };
            var level = new LevelData
            {
                id = 0,
                width = 5,
                height = 4,
                colors = colors.Select(c => "#" + ColorUtility.ToHtmlStringRGB(c)).ToArray(),
                picture = rows,
                canvas = canvas,
                slots = 3,
                spools = new[]
                {
                    new SpoolData { column = 0, color = 0, loops = 4 },
                    new SpoolData { column = 1, color = 1, loops = 6 },
                },
            };
            var game = new LoomGame(level);
            var picture = Picture(scene, game, colors, 30f, _sw / 2f, 20f);
            var loomY = 20f + picture.Height + 24f;
            var frames = Loom(scene, 3, loomY);
            var fieldY = loomY + 74f + 50f;

            var cream = Spool(scene, Cream, 4, 46f);
            Ui.PlaceCenter(cream.Root, _sw / 2f - 40f, fieldY, 46, 46);
            cream.SetFabric(true);
            var pink = Spool(scene, Pink, 6, 46f);
            Ui.PlaceCenter(pink.Root, _sw / 2f + 40f, fieldY, 46, 46);

            var views = new SpoolView?[frames.Length];
            var hand = Hand(scene, new Vector2(_sw - 30f, fieldY + 70f));
            void PressAt(Vector2 at, float delay, Action pressed)
            {
                var start = hand.anchoredPosition;
                Tweens.Run(hand, 0.45f, t => hand.anchoredPosition = Vector2.Lerp(start, new Vector2(at.x, -at.y), t), Ease.InOutCubic, () =>
                {
                    Tweens.Run(hand, 0.15f, t => hand.localScale = Vector3.one * (1f - 0.15f * Ease.Bump(t)));
                    pressed();
                }, delay);
            }

            PressAt(new Vector2(_sw / 2f - 22f, fieldY + 26f), 0.5f, () =>
            {
                Sfx.Play(Sound.Pick, 0.8f);
                UnityEngine.Object.Destroy(cream.Root.gameObject);
                game.TapColumn(0);
                SyncSlots(views, game, frames, colors);
                Tweens.Delay(scene, 0.35f, () =>
                {
                    Sfx.Play(Sound.Deny, 0.7f);
                    Tweens.Shake((RectTransform)frames[0].parent, 6f, 0.3f);
                    Fx.FloatText(_fx, Ui.WorldCenter(frames[0]) + new Vector3(0, 46f * _fx.lossyScale.y, 0), L.T("Не нужна!"), Palette.Danger, 20f);
                });

                PressAt(new Vector2(_sw / 2f + 58f, fieldY + 26f), 0.9f, () =>
                {
                    Sfx.Play(Sound.Pick, 0.8f);
                    UnityEngine.Object.Destroy(pink.Root.gameObject);
                    game.TapColumn(1);
                    SyncSlots(views, game, frames, colors);
                    var demo = new Demo(game, picture, colors, scene, 0.09f, slot => Ui.WorldCenter(frames[slot]))
                    {
                        Wobble = slot => Wobble(views[slot]),
                        Sync = () => SyncSlots(views, game, frames, colors),
                        Idle = () =>
                        {
                            Fx.FloatText(_fx, Ui.WorldCenter(frames[0]) + new Vector3(0, 46f * _fx.lossyScale.y, 0), L.T("Место занято"), Palette.Danger, 18f);
                            Loop(scene, 1.8f);
                        },
                        Won = () => Loop(scene, 1.8f),
                    };
                    Start(demo);
                    var start = hand.anchoredPosition;
                    Tweens.Run(hand, 0.4f, t => hand.anchoredPosition = Vector2.Lerp(start, new Vector2(_sw + 40f, -(fieldY + 90f)), t), Ease.InOutCubic, null, 0.2f);
                });
            });
        }

        /// <summary>
        /// Интерактив: семь особых клубков сеткой. Тап по клубку подсвечивает его кольцом
        /// и показывает под сеткой, как он работает.
        /// </summary>
        private void SceneSpools(RectTransform scene)
        {
            var items = new (string name, string text, Spool spool, Color color)[]
            {
                (L.T("Скрытый"), L.T("Цвет откроется, когда клубок выйдет вперёд"), new Spool(0, 0, 6, hidden: true), Sky),
                (L.T("Узелок"), L.T("Первый тап развязывает, второй отправляет на станок"), new Spool(0, 0, 5, knots: 1), Pink),
                (L.T("Замок"), L.T("Откроется, когда на станок встанет клубок с ключом"), new Spool(0, 0, 7, locked: true), Leaf),
                (L.T("Связка"), L.T("Едут на станок только вдвоём: нужно 2 свободных места"), new Spool(0, 0, 4, link: 1), Sun),
                (L.T("Радужный"), L.T("Вяжет любой цвет. Выручает, когда нужного нет"), new Spool(0, 0, 5, rainbow: true), Color.white),
                (L.T("Толстый"), L.T("Занимает 2 места, зато вяжет вдвое быстрее"), new Spool(0, 0, 24, thick: true), Navy),
                (L.T("Ледяной"), L.T("Оттает, когда станок свяжет столько петель его цвета, сколько на льду"), new Spool(0, 0, 6, ice: 4), Sky),
            };

            const float size = 48f;
            const float stepX = 70f;
            const float rowY0 = 46f;
            const float stepY = 84f;
            var positions = new Vector2[items.Length];
            var views = new SpoolView[items.Length];
            for (var i = 0; i < items.Length; i++)
            {
                var row = i < 4 ? 0 : 1;
                var inRow = row == 0 ? 4 : 3;
                var col = row == 0 ? i : i - 4;
                var x = _sw / 2f + (col - (inRow - 1) / 2f) * stepX;
                var y = rowY0 + row * stepY;
                positions[i] = new Vector2(x, y);
                var (name, _, spool, color) = items[i];
                var view = SpoolView.Create(scene, size);
                Ui.PlaceCenter(view.Root, x, y, size, size);
                view.Bind(spool, color, true);
                views[i] = view;
                Ui.Label(scene, name, 12, Palette.Ink2, x - 40f, y + size / 2f + 6f, 80, 18);
            }

            var ring = Ui.Image(scene, "Selected", Art.Ring, Palette.Primary);
            ring.rectTransform.SetSiblingIndex(0);
            var captionY = rowY0 + stepY + size / 2f + 36f;
            var title = Ui.Label(scene, "", 17, Palette.Ink, 20, captionY, _sw - 40, 24);
            var text = Ui.Label(scene, "", 14, Palette.Ink2, 24, captionY + 28f, _sw - 48, 44, false, TextAnchor.UpperCenter);
            text.lineSpacing = 0.95f;

            void Select(int index, bool sound)
            {
                var at = positions[index];
                Ui.PlaceCenter(ring.rectTransform, at.x, at.y, size + 18f, size + 18f);
                Tweens.Scale(ring.rectTransform, 0.7f, 1f, 0.25f);
                Tweens.Punch(views[index].Root, 0.15f, 0.25f);
                title.text = items[index].name;
                text.text = items[index].text;
                if (sound)
                {
                    Sfx.Play(Sound.Pick, 0.6f);
                }
            }

            for (var i = 0; i < items.Length; i++)
            {
                var index = i;
                var tap = views[i].Tap;
                tap.ClickSound = false;
                tap.OnClick(() => Select(index, true));
            }

            Select(0, false);
        }

        /// <summary>
        /// Интерактив: на станке чужая пряжа и два клубка. Тап по бустеру показывает его действие:
        /// ножницы срезают чужую пряжу, «Отменить» возвращает последний клубок, «+1 слот» добавляет место.
        /// </summary>
        private void SceneBoosters(RectTransform scene)
        {
            const float slot = 58f;
            const float gap = 8f;
            var loomY = 34f;
            var frames = new List<RectTransform>(Loom(scene, 3, loomY));
            var loom = (RectTransform)frames[0].parent;
            var colors = new[] { Cream, Pink, Sky };
            var inSlots = new SpoolView?[3];
            for (var i = 0; i < 3; i++)
            {
                var view = SpoolView.Create(frames[i], 44f);
                Ui.PlaceCenter(view.Root, slot / 2f, slot / 2f, 44, 44);
                view.Bind(new Spool(0, 0, 4 + i), colors[i], true, false, i == 0);
                inSlots[i] = view;
            }

            // Снизу вверх: кнопки бустеров, над ними пояснение, между станком и пояснением — место для «отменённого» клубка.
            var by = _sh - 16f - 56f;
            var titleY = by - 10f - 44f - 26f;
            var fieldY = (loomY + 74f + titleY) / 2f;
            var title = Ui.Label(scene, L.T("Выбери бустер"), 17, Palette.Ink, 20, titleY, _sw - 40, 24);
            var text = Ui.Fit(Ui.Label(scene, L.T("Ножницы, отмена хода или ещё одно место на станке"), 14, Palette.Ink2, 24, titleY + 26f, _sw - 48, 44,
                false, TextAnchor.UpperCenter), 11);
            text.lineSpacing = 0.95f;

            var busy = false;
            var types = new[] { BoosterType.Scissors, BoosterType.Undo, BoosterType.Slot };
            var bw = (_sw - 32f - 2 * 8f) / 3f;
            for (var i = 0; i < types.Length; i++)
            {
                var type = types[i];
                var card = Ui.Card(scene, "Booster" + type, 16 + i * (bw + 8f), by, bw, 56, 16, Palette.Card, Palette.CardShadow, 3f, true);
                Ui.IconImage(card, BoosterInfo.Icon(type), Palette.Ink, bw / 2f, 20, 20);
                Ui.Label(card, type == BoosterType.Slot ? L.T("+1 место") : BoosterInfo.Name(type), 12, Palette.Ink, 0, 34, bw, 18);
                var tap = card.gameObject.AddComponent<Tap>();
                tap.OnClick(() =>
                {
                    if (busy)
                    {
                        return;
                    }

                    busy = true;
                    title.text = BoosterInfo.Name(type);
                    text.text = BoosterInfo.Description(type);
                    switch (type)
                    {
                        case BoosterType.Scissors:
                            var cut = inSlots[0];
                            if (cut != null)
                            {
                                Sfx.Play(Sound.Knot, 0.8f);
                                Fx.Poof(_fx, Ui.WorldCenter(cut.Root), Palette.Ink3, 56f);
                                UnityEngine.Object.Destroy(cut.Root.gameObject);
                                inSlots[0] = null;
                            }

                            break;
                        case BoosterType.Undo:
                            var back = inSlots[2];
                            if (back != null)
                            {
                                Sfx.Play(Sound.Pick, 0.8f);
                                var root = back.Root;
                                root.SetParent(scene, true);
                                var to = Ui.WorldCenter(scene) + new Vector3(0, (_sh / 2f - fieldY) * scene.lossyScale.y, 0);
                                Tweens.Move(root, root.position, to, 0.4f, Ease.OutCubic, () => Tweens.Scale(root, 0.8f, 1f, 0.2f));
                                inSlots[2] = null;
                            }

                            break;
                        default:
                            Sfx.Play(Sound.Unlock, 0.8f);
                            var w = loom.sizeDelta.x + slot + gap;
                            loom.sizeDelta = new Vector2(w, loom.sizeDelta.y);
                            loom.anchoredPosition = new Vector2(_sw / 2f - w / 2f, loom.anchoredPosition.y);
                            var frame = Ui.Box(loom, "Slot", 14, Palette.Card);
                            Ui.Place(frame.rectTransform, 8 + 3 * (slot + gap), 8, slot, slot);
                            Tweens.Scale(frame.rectTransform, 0.3f, 1f, 0.3f);
                            Fx.Sparkle(_fx, Ui.WorldCenter(frame.rectTransform), Palette.Primary, 8, 34f);
                            break;
                    }

                    Loop(scene, 2.6f);
                });
            }
        }

        /// <summary>Как получить три звезды, цели уровня и награды, которые копятся между уровнями.</summary>
        private void SceneStars(RectTransform scene)
        {
            var rows = new[]
            {
                (1, L.T("Довязать картинку")),
                (2, L.T("Без спасения после затора")),
                (3, L.T("Без бустеров")),
            };
            const float rowH = 40f;
            var y = 20f;
            for (var i = 0; i < rows.Length; i++)
            {
                var (count, label) = rows[i];
                var row = Ui.Box(scene, "Row", 14, Palette.Surface).rectTransform;
                Ui.Place(row, 20, y + i * (rowH + 6f), _sw - 40, rowH);
                var stars = new List<Image>();
                for (var s = 0; s < count; s++)
                {
                    stars.Add(Ui.IconImage(row, Icon.Star, Palette.Ink3, 16 + s * 13, rowH / 2f, 14));
                }

                Ui.Label(row, label, 13, Palette.Ink, 58, 0, _sw - 40 - 100, rowH, true, TextAnchor.MiddleLeft);
                var mark = Ui.Image(row, "Mark", Art.Circle, Palette.Primary);
                Ui.PlaceCenter(mark.rectTransform, _sw - 40 - 22, rowH / 2f, 24, 24);
                var check = Ui.IconImage(row, Icon.Check, Color.white, _sw - 40 - 22, rowH / 2f, 15);
                mark.rectTransform.localScale = Vector3.zero;
                check.rectTransform.localScale = Vector3.zero;
                var box = Ui.Box(row, "Glow", 14, Palette.PrimarySoft);
                Ui.Stretch(box.rectTransform);
                box.rectTransform.SetAsFirstSibling();
                box.color = Color.clear;
                var delay = 0.4f + i * 0.45f;
                Tweens.Delay(row, delay, () =>
                {
                    Sfx.Play(Sound.Star, 0.6f);
                    box.color = Palette.PrimarySoft;
                    foreach (var star in stars)
                    {
                        star.color = Palette.Coin;
                    }

                    Tweens.Scale(mark.rectTransform, 0.2f, 1f, 0.25f);
                    Tweens.Scale(check.rectTransform, 0.2f, 1f, 0.25f);
                });
            }

            y += 3 * (rowH + 6f) + 12f;
            var chips = new[] { (Icon.Star, Palette.Coin, L.T("Золото 2/3")) };
            const float chipW = 150f;
            for (var i = 0; i < chips.Length; i++)
            {
                var (icon, tint, label) = chips[i];
                var chip = Ui.Card(scene, "Goal", _sw / 2f - chipW / 2f, y, chipW, 28, 14, Palette.Card, Palette.CardShadow, 2f);
                Ui.IconImage(chip, icon, tint, 18, 14, 18);
                Ui.Label(chip, label, 13, Palette.Ink, 32, 0, chipW - 38, 28, true, TextAnchor.MiddleLeft);
                chip.localScale = Vector3.zero;
                Tweens.Delay(chip, 1.8f + i * 0.15f, () => Tweens.Scale(chip, 0.3f, 1f, 0.3f));
            }

            y += 46f;
            var rewards = new[] { (Icon.Gift, Palette.Danger, L.T("Подарок дня")), (Icon.Chest, Palette.CoinShadow, L.T("Сундук за ★")), (Icon.Trophy, Palette.Link, L.T("Коллекции")) };
            var rw = (_sw - 40f) / 3f;
            for (var i = 0; i < rewards.Length; i++)
            {
                var (icon, tint, label) = rewards[i];
                var cell = Ui.Place(Ui.Node(scene, "Reward"), 20 + i * rw, y, rw, 70);
                var circle = Ui.Image(cell, "Back", Art.Circle, Palette.WithAlpha(tint, 0.14f));
                Ui.PlaceCenter(circle.rectTransform, rw / 2f, 22, 42, 42);
                Ui.IconImage(cell, icon, tint, rw / 2f, 22, 24);
                Ui.Label(cell, label, 12, Palette.Ink2, 0, 48, rw, 18);
                cell.localScale = Vector3.zero;
                Tweens.Delay(cell, 2.3f + i * 0.15f, () =>
                {
                    Tweens.Scale(cell, 0.3f, 1f, 0.3f);
                    Sfx.Play(Sound.Coin, 0.4f);
                });
            }

            Loop(scene, 5.5f);
        }

        // ---------- Детали сцен ----------

        private void Start(Demo demo)
        {
            _demo = demo;
            demo.Running = true;
        }

        /// <summary>Мини-уровень: картинка буквами-цифрами и по клубку на цвет; клубки из колонок place уже на станке.</summary>
        private static LoomGame DemoGame(string[] rows, Color[] colors, int slots, bool split, params int[] place)
        {
            var spools = new List<SpoolData>();
            var column = 0;
            for (var c = 0; c < colors.Length; c++)
            {
                var count = rows.Sum(r => r.Count(ch => ch - '0' == c));
                if (count == 0)
                {
                    continue;
                }

                // В сцене с комбо цвет делится на два клубка, чтобы было что менять на станке.
                var first = split ? count / 2 : count;
                spools.Add(new SpoolData { column = column++, color = c, loops = first });
                if (first < count)
                {
                    spools.Add(new SpoolData { column = column++, color = c, loops = count - first });
                }
            }

            // Для сцены с комбо порядок колонок: розовый, голубой, розовый, голубой.
            var ordered = spools.OrderBy(s => spools.Where(o => o.color == s.color).ToList().IndexOf(s)).ThenBy(s => s.color).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                ordered[i].column = i;
            }

            var level = new LevelData
            {
                id = 0,
                width = rows[0].Length,
                height = rows.Length,
                colors = colors.Select(c => "#" + ColorUtility.ToHtmlStringRGB(c)).ToArray(),
                picture = rows,
                slots = slots,
                spools = ordered.ToArray(),
            };
            var game = new LoomGame(level);
            foreach (var p in place)
            {
                game.TapColumn(p);
            }

            return game;
        }

        private static PictureView Picture(RectTransform scene, LoomGame game, Color[] colors, float cell, float cx, float y)
        {
            var gap = 3f;
            var picture = new PictureView(scene, game, colors, cell, gap);
            Ui.Place(picture.Root, cx - picture.Width / 2f, y, picture.Width, picture.Height);
            return picture;
        }

        private RectTransform[] Loom(RectTransform scene, int slots, float y)
        {
            const float slot = 58f;
            const float gap = 8f;
            var w = slots * slot + (slots - 1) * gap + 16f;
            var loom = Ui.Box(scene, "Loom", 20, Palette.LoomBg).rectTransform;
            Ui.Place(loom, _sw / 2f - w / 2f, y, w, 74);
            var frames = new RectTransform[slots];
            for (var i = 0; i < slots; i++)
            {
                var frame = Ui.Box(loom, "Slot", 14, Palette.Card);
                Ui.Place(frame.rectTransform, 8 + i * (slot + gap), 8, slot, slot);
                frames[i] = frame.rectTransform;
            }

            return frames;
        }

        private static SpoolView Spool(RectTransform parent, Color color, int loops, float size)
        {
            var view = SpoolView.Create(parent, size);
            view.Bind(new Spool(0, 0, loops), color, true);
            return view;
        }

        private static SpoolView?[] SlotViews(RectTransform[] frames, LoomGame game, Color[] colors)
        {
            var views = new SpoolView?[frames.Length];
            SyncSlots(views, game, frames, colors);
            return views;
        }

        private static void SyncSlots(SpoolView?[] views, LoomGame game, RectTransform[] frames, Color[] colors)
        {
            for (var i = 0; i < frames.Length; i++)
            {
                var spool = i < game.Slots.Count ? game.Slots[i] : null;
                var view = views[i];
                if (spool == null && view != null)
                {
                    Fx.Sparkle((RectTransform)frames[i].parent, Ui.WorldCenter(view.Root), Palette.Coin, 6, 26f);
                    UnityEngine.Object.Destroy(view.Root.gameObject);
                    views[i] = null;
                }
                else if (spool != null && view == null)
                {
                    var created = SpoolView.Create(frames[i], 44f);
                    Ui.PlaceCenter(created.Root, frames[i].sizeDelta.x / 2f, frames[i].sizeDelta.y / 2f, 44, 44);
                    created.Bind(spool, colors[spool.Color], true, false, game.IsForeign(spool));
                    Tweens.Scale(created.Root, 1.25f, 1f, 0.2f);
                    views[i] = created;
                }
                else if (spool != null && view != null)
                {
                    view.SetLoops(spool.Loops);
                }
            }
        }

        private static void Wobble(SpoolView? view)
        {
            if (view == null)
            {
                return;
            }

            var root = view.Root;
            Tweens.Run(root, 0.12f, t => root.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * Mathf.PI * 2f) * 9f), null,
                () => root.localRotation = Quaternion.identity);
        }

        private static void Roll(RectTransform ball, Vector2 from, Vector2 to, float size, int direction)
        {
            Ui.PlaceCenter(ball, from.x, from.y, size, size);
            Tweens.Run(ball, 0.75f, t =>
            {
                var p = Vector2.Lerp(from, to, t);
                ball.anchoredPosition = new Vector2(p.x, -p.y - Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f)) * 18f * (1f - t));
                ball.localRotation = Quaternion.Euler(0, 0, -direction * 540f * t);
            }, Ease.OutCubic, () => Tweens.Scale(ball, 1.15f, 1f, 0.2f));
        }

        /// <summary>Рука-указатель: тёмная, с белой обводкой, чтобы читалась и на белом, и на цветном.</summary>
        private static RectTransform Hand(RectTransform scene, Vector2 at)
        {
            var root = Ui.PlaceCenter(Ui.Node(scene, "Hand"), at.x, at.y, 56, 56);
            var halo = Ui.Image(root, "Halo", Art.Icon(Icon.Hand), Color.white);
            Ui.PlaceCenter(halo.rectTransform, 28, 28, 64, 64);
            var hand = Ui.Image(root, "Hand", Art.Icon(Icon.Hand), Palette.Ink);
            Ui.PlaceCenter(hand.rectTransform, 28, 28, 56, 56);
            root.pivot = new Vector2(0.35f, 0.85f);
            return root;
        }

        private sealed class Demo
        {
            public Demo(LoomGame game, PictureView picture, Color[] colors, RectTransform owner, float interval, Func<int, Vector3> source)
            {
                Game = game;
                Picture = picture;
                Colors = colors;
                Owner = owner;
                Interval = interval;
                Source = source;
            }

            public LoomGame Game { get; }

            public PictureView Picture { get; }

            public Color[] Colors { get; }

            public RectTransform Owner { get; }

            public float Interval { get; }

            public Func<int, Vector3> Source { get; }

            public float Clock { get; set; }

            public bool Running { get; set; }

            public Action<int>? Wobble { get; set; }

            public Action? Sync { get; set; }

            public Action<Completion>? Done { get; set; }

            public Action? Won { get; set; }

            public Action? Idle { get; set; }
        }
    }
}
