#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using KnittedPicture.Core;
using UnityEngine;

namespace KnittedPicture.Game
{
    /// <summary>
    /// «Выставка недели»: тема и её цвета, три места для картинок в рамках, очки каждой и сумма с медалью.
    /// Сначала места заполнены лучшим набором (картинки ближе всего к теме, в самых ценных рамках): игрок
    /// может сразу выставить или поменять картинку (тап по ней) и рамку (кнопка под ней). Оценка видна до
    /// того, как выставить. Выставлено — показываем итог и когда новая тема.
    /// </summary>
    public sealed class ExhibitionPopup : Popup
    {
        private readonly int[] _levels;
        private readonly int[] _frames;

        public ExhibitionPopup(App app, Action changed, int[]? levels = null, int[]? frames = null)
            : base(app, bottomSheet: true)
        {
            Closed = changed;
            var progress = app.Progress;
            var today = app.Today;
            var week = WeeklyMarathon.WeekOf(today);
            var theme = Exhibition.ThemeOf(week);
            var done = progress.ExhibitionDone(today);

            if (done && progress.Data.exhibitLevels.Count == Exhibition.Pictures && progress.Data.exhibitFrames.Count == Exhibition.Pictures
                && progress.Data.exhibitLevels.All(app.Levels.Has))
            {
                _levels = progress.Data.exhibitLevels.ToArray();
                _frames = progress.Data.exhibitFrames.ToArray();
            }
            else if (levels != null && frames != null)
            {
                _levels = levels;
                _frames = frames;
            }
            else
            {
                (_levels, _frames) = BestSet(app, theme);
            }

            AddTitle(L.T("Выставка недели"), 24);
            var autoPicked = !done && levels == null;
            AddText(L.F("Тема: «{0}»", theme.Name), 14, 1);
            AddLimit(() => L.F("Новая тема через {0}", Limits.ResetIn(Limits.NextMonday(app.Today))));

            // Цвета темы кружками: видно, какие картинки подойдут.
            var dot = 22f;
            var dotsW = theme.Colors.Length * (dot + 8f) - 8f;
            for (var i = 0; i < theme.Colors.Length; i++)
            {
                var circle = Ui.Image(Card, "Color", Art2.Ball(Palette.Hex(theme.Colors[i])), Color.white);
                Ui.PlaceCenter(circle.rectTransform, CardWidth / 2f - dotsW / 2f + dot / 2f + i * (dot + 8f), Cursor + dot / 2f, dot, dot);
            }

            Space(dot + 14f);

            // Три места.
            const float gap = 12f;
            var slotW = Mathf.Floor((Inner - 2f * gap) / 3f);
            var size = Mathf.Min(slotW, 110f);
            var top = Cursor;
            var total = 0;
            for (var i = 0; i < Exhibition.Pictures; i++)
            {
                var x = 24f + i * (slotW + gap);
                total += Slot(app, theme, i, x, top, slotW, size, done);
            }

            Space(size + (done ? 46f : 92f));

            // Сумма и медали: полоска до 360 с отметками бронзы, серебра и золота.
            var medal = Exhibition.MedalOf(total);
            Ui.Label(Card, L.F("Оценка жюри: {0}", total), 16, Palette.Ink, 24, Cursor, Inner - 120, 24, true, TextAnchor.MiddleLeft);
            Ui.Label(Card, Exhibition.MedalName(medal), 15, MedalColor(medal), 24 + Inner - 120, Cursor, 120, 24, true, TextAnchor.MiddleRight);
            Space(30f);
            const float max = 360f;
            Ui.ProgressBar(Card, 24, Cursor, Inner, 10, Mathf.Min(1f, total / max), MedalColor(Mathf.Max(1, medal)));
            foreach (var need in Exhibition.MedalScores)
            {
                var tick = Ui.Box(Card, "Tick", 1, Palette.Card).rectTransform;
                Ui.Place(tick, 24 + Inner * need / max - 1f, Cursor, 2f, 10f);
            }

            Space(16f);
            Ui.Fit(Ui.Label(Card, autoPicked ? L.T("Подобрали лучший набор: тапни картинку, чтобы заменить, или смени рамку")
                : L.T("Очки: цвета в тему, звёзды картинки и редкость рамки (рамка её коллекции ещё +6)"), 12, Palette.Ink3,
                24, Cursor, Inner, 34, false), 9).lineSpacing = 0.9f;
            Space(42f);

            if (done)
            {
                AddText(L.T("Картинки уже на выставке"), 14, 1);
                AddPrimary(L.T("Понятно"), 54).OnClick(Close);
            }
            else
            {
                var submit = AddPrimary(L.T("Выставить"));
                Ui.Sheen((RectTransform)submit.transform, Inner, 60f, 3.2f, 0.5f);
                submit.OnClick(() => Submit(app, changed));
            }

            Finish(8);
        }

        /// <summary>Место выставки: картинка в рамке (тап — выбрать другую), очки, кнопка смены рамки.</summary>
        private int Slot(App app, ExhibitionTheme theme, int index, float x, float y, float w, float size, bool done)
        {
            var level = app.Levels.Get(_levels[index]);
            var frame = AlbumFrames.Get(_frames[index]);
            var stars = app.Progress.StarsOf(level.id);
            var (colorPoints, starPoints, framePoints) = Exhibition.Breakdown(level, stars, frame, theme);
            var score = colorPoints + starPoints + framePoints;
            var root = Ui.Place(Ui.Node(Card, "Slot" + index), x + (w - size) / 2f, y, size, size);
            MiniPicture.Framed(root, frame.Id, size, level);
            Ui.Fit(Ui.Label(Card, L.F("{0} очк.", score), 13, Palette.Ink, x, y + size + 4f, w, 20), 9);

            // Из чего очки: цвета, звёзды, рамка — видно, что поменять, чтобы стало больше.
            Ui.Fit(Ui.Label(Card, L.F("цвета {0} · ★ {1} · рамка {2}", colorPoints, starPoints, framePoints), 10, Palette.Ink3, x - 4f, y + size + 22f, w + 8f, 16,
                false), 7);
            if (done)
            {
                return score;
            }

            // Есть рамка выгоднее для этой картинки — кнопка подсказывает, на сколько больше очков.
            var owned = AlbumFrames.All.Where(f => app.Progress.FrameOwned(f.Id)).ToList();
            var bestGain = owned.Max(f => Exhibition.Score(level, stars, f, theme)) - score;

            root.gameObject.AddComponent<Tap>().OnClick(() => app.ShowPopup(new PicturePickerPopup(app, theme, _levels, picked =>
            {
                var levels = (int[])_levels.Clone();
                levels[index] = picked;
                Reopen(app, levels, _frames);
            })));

            var better = bestGain > 0;
            Ui.Button(Card, better ? L.F("Рамка +{0}", bestGain) : L.T("Рамка"), x + 2f, y + size + 44f, w - 4f, 36f,
                better ? Palette.PrimarySoft : Palette.Surface, Palette.CardShadow, better ? Palette.Primary : Palette.Ink, 13).OnClick(() =>
            {
                // По кругу, но сначала — самая выгодная, если она ещё не стоит.
                var ids = owned.Select(f => f.Id).ToList();
                var best = owned.OrderByDescending(f => Exhibition.Score(level, stars, f, theme)).First().Id;
                var next = better ? best : ids[(ids.IndexOf(_frames[index]) + 1) % ids.Count];
                var frames = (int[])_frames.Clone();
                frames[index] = next;
                Reopen(app, _levels, frames);
            });
            return score;
        }

        private void Reopen(App app, int[] levels, int[] frames)
        {
            var changed = Closed;
            Closed = null;
            Close();
            app.ShowPopup(new ExhibitionPopup(app, changed ?? app.RefreshScreen, levels, frames));
        }

        private void Submit(App app, Action changed)
        {
            var pictures = _levels.Select(id => app.Levels.Get(id)).ToList();
            var result = app.Progress.SubmitExhibition(app.Today, pictures, _frames, app.Random);
            if (result == null)
            {
                app.Toast(L.T("Нужны три разные готовые картинки"));
                return;
            }

            var r = result.Value;
            Analytics.Event("exhibition_submit", ("score", r.Score), ("medal", r.Medal));
            app.Save();
            Closed = null;
            Close();
            if (r.Granted != null)
            {
                Sfx.Play(Sound.Win, 0.6f);
                app.ShowPopup(new RewardPopup(app, L.F("Выставка: {0}", Exhibition.MedalName(r.Medal)), Icon.Trophy, r.Granted.Value, changed,
                    Art2.Trophy(r.Medal >= 3)));
            }
            else
            {
                app.Toast(L.F("Оценка жюри: {0}. На медаль не хватило, но очки сезона твои", r.Score));
                changed();
            }
        }

        /// <summary>Лучший набор: три картинки ближе всего к теме, каждая в самой ценной своей рамке.</summary>
        private static (int[] levels, int[] frames) BestSet(App app, ExhibitionTheme theme)
        {
            var progress = app.Progress;
            var frames = AlbumFrames.All.Where(f => progress.FrameOwned(f.Id)).ToList();
            var picks = progress.Data.completed
                .Where(app.Levels.Has)
                .Select(id => app.Levels.Get(id))
                .Select(level =>
                {
                    var best = frames.OrderByDescending(f => Exhibition.Score(level, progress.StarsOf(level.id), f, theme)).First();
                    return (level.id, frame: best.Id, score: Exhibition.Score(level, progress.StarsOf(level.id), best, theme));
                })
                .OrderByDescending(p => p.score)
                .Take(Exhibition.Pictures)
                .ToList();
            while (picks.Count < Exhibition.Pictures)
            {
                picks.Add((1, 0, 0));
            }

            return (picks.Select(p => p.id).ToArray(), picks.Select(p => p.frame).ToArray());
        }

        public static Color MedalColor(int medal) => medal switch
        {
            3 => Palette.Hex("#8A5700"),
            2 => Palette.Hex("#5F6670"),
            1 => Palette.Hex("#9A5222"),
            _ => Palette.Ink3,
        };
    }

    /// <summary>Выбор картинки для выставки: готовые картинки сеткой, у каждой — доля цветов темы.</summary>
    public sealed class PicturePickerPopup : Popup
    {
        public PicturePickerPopup(App app, ExhibitionTheme theme, IReadOnlyCollection<int> taken, Action<int> picked)
            : base(app, bottomSheet: true)
        {
            AddTitle(L.T("Какую картинку выставить?"), 20);
            AddText(L.F("Тема «{0}»: процент — сколько цветов картинки в тему", theme.Name), 13, 2);
            const int columns = 4;
            const float gap = 10f;
            var cellW = Mathf.Floor((Inner - gap * (columns - 1)) / columns);
            var cellH = cellW + 22f;
            var levels = app.Progress.Data.completed.Where(app.Levels.Has).OrderBy(id => id).ToList();
            var rows = (levels.Count + columns - 1) / columns;
            var viewH = RoomFor(8f + 12f, rows * (cellH + gap));
            var content = Ui.Scroll(Card, 24, Cursor, Inner, viewH);
            content.sizeDelta = new Vector2(Inner, rows * (cellH + gap));
            for (var i = 0; i < levels.Count; i++)
            {
                var level = app.Levels.Get(levels[i]);
                var x = (i % columns) * (cellW + gap);
                var y = (i / columns) * (cellH + gap);
                Stagger(i, 16, () => PickTile(app, theme, taken, picked, content, level, x, y, cellW));
            }

            Space(viewH + 8f);
            Finish(12);
        }

        private void PickTile(App app, ExhibitionTheme theme, IReadOnlyCollection<int> taken, Action<int> picked, RectTransform content, LevelData level,
            float x, float y, float cellW)
        {
            {
                var used = taken.Contains(level.id);
                var tile = Ui.Box(content, "Pick", 12, used ? Palette.Track : Palette.Surface, true).rectTransform;
                Ui.Place(tile, x, y, cellW, cellW);
                MiniPicture.Create(tile, level, cellW / 2f, cellW / 2f, cellW - 12f, used);
                var share = Mathf.RoundToInt(100f * Exhibition.Share(level, theme));
                Ui.Label(content, share + "%", 12, share >= 50 ? Palette.Primary : Palette.Ink2, x, y + cellW + 2f, cellW, 18);
                if (!used)
                {
                    var id = level.id;
                    tile.gameObject.AddComponent<Tap>().OnClick(() =>
                    {
                        Close();
                        picked(id);
                    });
                }
            }
        }
    }
}
