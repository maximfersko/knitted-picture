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
    /// Вступление на первом запуске: пять коротких сцен «как играть». Вязка в сценах идёт
    /// на настоящем ядре игры, поэтому показанное в точности совпадает с правилами.
    /// Листается тапом, сцены зациклены, «Пропустить» — всегда под рукой.
    /// </summary>
    public sealed class IntroScreen : ScreenView
    {
        private const float BeadFlight = 0.22f;

        private static readonly string[] Titles =
        {
            "Вязаная картинка",
            "Тапни клубок",
            "Станок вяжет сам",
            "Места мало",
            "Темп и комбо",
        };

        private static readonly string[] Texts =
        {
            "Клубки пряжи превращаются в картинку петля за петлёй",
            "Передний клубок колонки поедет на станок",
            "Клубок вяжет свой цвет на фронте, внизу столбиков. Фронт подсвечен ярче",
            "Нет мест и нет работы? Это затор. Бери клубки, которым сразу есть дело",
            "Ставь клубок, пока станок вяжет, и комбо растёт. На ×5 включается Разгон: станок вяжет вдвое быстрее",
        };

        private static readonly Color Pink = Palette.Hex("#FF5C93");
        private static readonly Color Sky = Palette.Hex("#6CB8FF");
        private static readonly Color Sun = Palette.Hex("#FFC23C");
        private static readonly Color Leaf = Palette.Hex("#2EBD7E");

        private readonly RectTransform _stage;
        private readonly float _sw;
        private readonly float _sh;
        private readonly Text _title;
        private readonly Text _text;
        private readonly Image[] _dots = new Image[Titles.Length];
        private readonly Text _nextLabel;
        private RectTransform _fx;
        private RectTransform? _scene;
        private Demo? _demo;
        private int _page = -1;

        public IntroScreen(App app)
            : base(app, "Intro")
        {
            Ui.TextButton(Root, "Пропустить", W - 136, 8, 120, 44, Palette.Ink2, 15).OnClick(Finish);

            _sw = W - 32f;
            _sh = Mathf.Clamp(H * 0.5f, 300f, 430f);
            var stageCard = Ui.Card(Root, "Stage", 16, 60, _sw, _sh, 28, Palette.Card, Palette.CardShadow);
            _stage = Ui.Stretch(Ui.Node(stageCard, "Content"));
            var hit = Ui.Image(_stage, "Hit", null, Color.clear, true);
            Ui.Stretch(hit.rectTransform);
            hit.gameObject.AddComponent<Tap>().OnClick(Next).PressFeedback = false;

            var y = 60f + _sh + 22f;
            _title = Ui.Label(Root, "", 28, Palette.Ink, 20, y, W - 40, 38);
            _text = Ui.Label(Root, "", 16, Palette.Ink2, 28, y + 44f, W - 56, 66, false, TextAnchor.UpperCenter);
            _text.lineSpacing = 1.05f;

            var dotsY = H - 24f - 64f - 30f;
            for (var i = 0; i < _dots.Length; i++)
            {
                _dots[i] = Ui.Box(Root, "Dot", 4, Palette.Track);
            }

            var next = Ui.Button(Root, "", 20, H - 24f - 64f, W - 40, 64, Palette.Primary, Palette.PrimaryShadow, Color.white, 22);
            _nextLabel = Ui.Label((RectTransform)next.transform, "Дальше", 22, Color.white, 0, 0, W - 40, 64);
            next.OnClick(Next);
            LayoutDots(dotsY);

            _fx = Ui.Stretch(Ui.Node(Root, "Fx"));
            Go(0);
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

        public override bool OnBack()
        {
            Finish();
            return true;
        }

        // ---------- Листание ----------

        private void Next()
        {
            if (_page >= Titles.Length - 1)
            {
                Finish();
            }
            else
            {
                Go(_page + 1);
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
            _nextLabel.text = page == Titles.Length - 1 ? "Начать вязать" : "Дальше";
            Tweens.Scale(_title.transform, 0.9f, 1f, 0.25f);
            LayoutDots(H - 24f - 64f - 30f);
            BuildScene(true);
        }

        private void BuildScene(bool slideIn)
        {
            _demo = null;
            var scene = Ui.Stretch(Ui.Node(_stage, "Scene" + _page));
            var group = scene.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
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
                default: SceneCombo(scene); break;
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

        private void LayoutDots(float y)
        {
            var widths = _dots.Select((_, i) => i == Math.Max(0, _page) ? 22f : 8f).ToArray();
            var total = widths.Sum() + 8f * (_dots.Length - 1);
            var x = W / 2f - total / 2f;
            for (var i = 0; i < _dots.Length; i++)
            {
                Ui.Place(_dots[i].rectTransform, x, y, widths[i], 8);
                _dots[i].color = i == _page ? Palette.Primary : Palette.Track;
                x += widths[i] + 8f;
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
            var blue = Ui.Image(scene, "Blue", Art.YarnBall, Sky);
            var pink = Ui.Image(scene, "Pink", Art.YarnBall, Pink);
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

        /// <summary>Рука тапает передний клубок — он летит на станок, колонка подтягивается.</summary>
        private void SceneTap(RectTransform scene)
        {
            var frames = Loom(scene, 3, 46f);
            var size = 52f;
            var cx = _sw / 2f;
            var front = _sh - 156f;
            var colors = new[] { Pink, Sky, Sun };
            var spools = new List<RectTransform>();
            for (var i = 0; i < 3; i++)
            {
                var view = Spool(scene, colors[i], 6 + i * 2, size);
                Ui.PlaceCenter(view.Root, cx, front + i * 60f, size, size);
                view.Root.GetComponent<CanvasGroup>().alpha = i == 0 ? 1f : 0.5f;
                spools.Add(view.Root);
            }

            var hand = Hand(scene, new Vector2(_sw - 30f, _sh + 30f));
            var press = new Vector2(cx + 20f, front + 30f);
            Tweens.Run(hand, 0.6f, t => hand.anchoredPosition = Vector2.Lerp(new Vector2(_sw - 30f, -(_sh + 30f)), new Vector2(press.x, -press.y), t), Ease.InOutCubic, () =>
            {
                Tweens.Run(hand, 0.18f, t => hand.localScale = Vector3.one * (1f - 0.15f * Ease.Bump(t)));
                Tweens.Run(spools[0], 0.18f, t => spools[0].localScale = Vector3.one * (1f - 0.1f * Ease.Bump(t)), null, () =>
                {
                    Sfx.Play(Sound.Pick, 0.8f);
                    var target = Ui.WorldCenter(frames[0]);
                    Tweens.Move(spools[0], Ui.WorldCenter(spools[0]), target, 0.32f, Ease.OutCubic, () =>
                    {
                        Tweens.Scale(spools[0], 1.25f, 0.88f, 0.2f);
                        Fx.Sparkle(_fx, target, Pink, 6, 30f);
                    });
                    for (var i = 1; i < spools.Count; i++)
                    {
                        var spool = spools[i];
                        var from = spool.anchoredPosition;
                        var group = spool.GetComponent<CanvasGroup>();
                        var first = i == 1;
                        Tweens.Run(spool, 0.3f, t =>
                        {
                            spool.anchoredPosition = Vector2.Lerp(from, from + new Vector2(0, 60f), t);
                            if (first)
                            {
                                group.alpha = Mathf.Lerp(0.5f, 1f, t);
                            }
                        }, Ease.OutCubic, null, 0.1f);
                    }

                    Tweens.Run(hand, 0.5f, t => hand.anchoredPosition = Vector2.Lerp(new Vector2(press.x, -press.y), new Vector2(_sw + 40f, -(_sh + 40f)), t), Ease.InOutCubic, null, 0.25f);
                    Loop(scene, 1.6f);
                });
            });
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
            Ui.Label(chip, "яркие клетки: фронт", 12, Palette.Ink2, 0, 0, 184, 22);

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
            var wrong = new[] { Sun, Leaf, Palette.Hex("#8B5CF6") };
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

                Fx.FloatText(_fx, Ui.WorldCenter(loom), "Затор!", Palette.Danger, 26f);
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
                                Fx.FloatText(_fx, Ui.WorldCenter((RectTransform)frames[0].parent), "Вот так!", Palette.Primary, 24f);
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
                        $"Комбо ×{combo}", combo >= 4 ? Palette.Primary : Palette.Link, 22f);
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
                    created.Bind(spool, colors[spool.Color], true);
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
