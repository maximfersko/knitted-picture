#nullable enable

using System.Collections.Generic;
using System.Linq;
using KnittedPicture.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KnittedPicture.Game
{
    /// <summary>Обновление вида по модели: станок, поле, связки, бустеры, таймер.</summary>
    public sealed partial class GameScreen
    {
        // ---------- Обновление ----------

        private void RefreshAll()
        {
            CancelResponsiveInput();
            _mysteryPeekLeft = 0f;
            _picture.SetMysteryPeek(false);
            _picture.Refresh();
            RefreshGoals();
            if (_slotFrames.Length != _game.Slots.Count)
            {
                RebuildSlots();
            }

            _arriving.Clear();
            SyncLoom(true);
            RefreshField();
            RefreshBoosters();
            RefreshProgress();
            _coins.text = App.Progress.Coins.ToString();
        }

        private void RefreshProgress()
        {
            var fraction = (float)_picture.LandedCount / _game.TotalStitches;
            _progressFill.sizeDelta = new Vector2(Mathf.Max(8f, _progressWidth * fraction), 8f);
            _progressText.text = string.Empty;
        }

        /// <summary>Привести клубки на станке к модели: новые появляются, ушедшие исчезают, числа обновляются.</summary>
        private void SyncLoom(bool rebuild = false)
        {
            var used = 0;
            for (var i = 0; i < _slotFrames.Length; i++)
            {
                var spool = _game.Slots[i];
                var view = _slotViews[i];
                var tail = _tailViews[i];
                var isTail = _game.IsTail(i);
                if (tail != null && (rebuild || !isTail))
                {
                    DropTail(tail, rebuild);
                    _tailViews[i] = null;
                    tail = null;
                }

                if (isTail)
                {
                    if (view != null)
                    {
                        Object.Destroy(view.View.Root.gameObject);
                        _slotViews[i] = null;
                    }

                    if (tail == null)
                    {
                        _tailViews[i] = BuildTail(i, spool!);
                    }

                    used++;
                    _slotOutlines[i].enabled = false;
                    continue;
                }

                if (view != null && (rebuild || spool == null || view.SpoolId != spool.Id))
                {
                    Object.Destroy(view.View.Root.gameObject);
                    _slotViews[i] = null;
                    view = null;
                }

                if (spool != null)
                {
                    used++;
                }

                if (spool != null && view == null && !_arriving.Contains(spool.Id))
                {
                    var size = SpoolSizeAt(i);
                    var created = SpoolView.Create(_slotFrames[i], size);
                    Ui.PlaceCenter(created.Root, SpoolX(i), 29f, size, size);
                    created.Bind(spool, _colors[spool.Color], true, false, _game.IsForeign(spool));
                    created.SetFabric(_game.IsForeign(spool));
                    created.HideDouble();
                    _slotViews[i] = new SlotSpool(created, spool.Id, spool.Rainbow ? -1 : spool.Color);
                    if (_game.IsForeign(spool) && !_foreignShown)
                    {
                        _foreignShown = true;
                        ToastOnce("foreign-loom", L.T("Эта пряжа распустится сама за 6 шагов. Ножницы освободят место сразу"));
                    }
                    view = _slotViews[i];
                }

                if (view != null && spool != null)
                {
                    var synergy = _game.HasSlotSynergy(i);
                    view.View.SetLoomState(spool, _game.IsForeign(spool), synergy);
                    if (synergy)
                    {
                        ToastOnce("synergy", L.T("Клубки одного цвета рядом вяжут быстрее"));
                    }
                    view.View.SetWaiting(!_game.IsForeign(spool) && !_game.HasWork(spool));
                }

                _slotOutlines[i].enabled = spool == null;
            }

            // Плашка толстого клубка бледнеет вместе с ним: ждёт весь «широкий клубок».
            for (var i = 0; i < _tailViews.Length; i++)
            {
                var plate = _tailViews[i];
                var owner = i < _game.Slots.Count ? _game.Slots[i] : null;
                // Общая ячейка на два слота не бледнеет: она — сама лунка, а ждёт клубок (он бледнеет сам).
                if (plate == null || owner == null || plate.name == "Wide")
                {
                    continue;
                }

                var group = Ui.Group(plate);
                group.blocksRaycasts = false;
                group.alpha = !_game.IsForeign(owner) && !_game.HasWork(owner) ? 0.5f : 1f;
            }

            var free = _game.FreeSlots;
            _loomLabel.text = free == 1 && _slotFrames.Length > 1 ? L.T("Осталось 1 место!") : free == 0 ? L.T("Мест нет") : string.Empty;
            _loomLabel.color = free <= 1 ? Palette.Danger : Palette.Ink2;
        }

        /// <summary>Толстый клубок стоит в этом слоте и в соседнем справа: рисуем его одним широким клубком.</summary>
        private bool IsWide(int slot) => slot + 1 < _slotFrames.Length && _game.TailOf(slot) == slot + 1;

        /// <summary>Центр клубка по горизонтали внутри рамки слота: у широкого — над зазором между двумя слотами.</summary>
        private float SpoolX(int slot)
        {
            var w = _slotFrames[slot].sizeDelta.x;
            return IsWide(slot) ? w + SlotGap / 2f : w / 2f;
        }

        /// <summary>
        /// Толстый клубок в общей ячейке заметно крупнее обычного (почти во всю высоту лунки): сразу видно,
        /// что он толстый и занимает два места.
        /// </summary>
        private float SpoolSizeAt(int slot) => IsWide(slot) ? Mathf.Min(_slotSpool * 1.32f, 58f) : _slotSpool;

        /// <summary>Куда летит клубок на станок: в центр слота, у широкого — между двумя слотами.</summary>
        private Vector3 SlotTarget(int slot) =>
            IsWide(slot)
                ? Vector3.Lerp(Ui.WorldCenter(_slotFrames[slot]), Ui.WorldCenter(_slotFrames[slot + 1]), 0.5f)
                : Ui.WorldCenter(_slotFrames[slot]);

        /// <summary>
        /// Второй слот толстого клубка. Если слоты соседние — они сливаются в одну общую лунку на оба слота
        /// (без шва посередине), клубок стоит в её центре и крупнее обычного. Лунка «растекается» из своего слота
        /// на соседний, пока клубок летит. Если толстому достались слоты врозь — подкрашенная рамка и нить к клубку.
        /// </summary>
        private RectTransform BuildTail(int slot, Spool spool)
        {
            var color = spool.Rainbow ? Palette.Coin : _colors[spool.Color];
            var head = _game.Slots.ToList().IndexOf(spool);
            if (head >= 0 && head + 1 == slot)
            {
                // Лунка живёт в рамке головного слота и рисуется под клубком; рамка головного — поверх соседней,
                // а непрозрачная заливка целиком закрывает соседний слот: две клетки читаются как одна.
                var headFrame = _slotFrames[head];
                headFrame.SetAsLastSibling();
                var hw = headFrame.sizeDelta.x;
                var total = hw * 2f + SlotGap;
                var wide = Ui.Place(Ui.Node(headFrame, "Wide"), 0, 0, total, headFrame.sizeDelta.y);
                wide.name = "Wide";
                wide.SetAsFirstSibling();
                Ui.Stretch(Ui.Box(wide, "Fill", 14, Color.Lerp(Palette.Card, color, 0.18f)).rectTransform);
                var edge = Ui.Image(wide, "Edge", Art.RoundRectOutline(14, 2), Palette.WithAlpha(color, 0.6f));
                edge.type = Image.Type.Sliced;
                edge.pixelsPerUnitMultiplier = Art.Density;
                Ui.Stretch(edge.rectTransform);

                // Слияние: ячейка растягивается вправо от своего слота на соседний, с лёгким пружинящим доводом.
                var from = hw / total;
                wide.localScale = new Vector3(from, 1f, 1f);
                Tweens.Run(wide, 0.3f, t => wide.localScale = new Vector3(Mathf.LerpUnclamped(from, 1f, Ease.OutBack(t)), 1f, 1f), null,
                    () => wide.localScale = Vector3.one);
                return wide;
            }

            var frame = _slotFrames[slot];
            var root = Ui.Stretch(Ui.Node(frame, "Tail"));
            Ui.Stretch(Ui.Box(root, "Fill", 14, Palette.WithAlpha(color, 0.25f)).rectTransform);
            var outline = Ui.Image(root, "Edge", Art.RoundRectOutline(14, 2), Palette.WithAlpha(color, 0.7f));
            outline.type = Image.Type.Sliced;
            outline.pixelsPerUnitMultiplier = Art.Density;
            Ui.Stretch(outline.rectTransform);
            var toLeft = head >= 0 && head < slot;
            var w = frame.sizeDelta.x;
            var thread = Ui.Box(root, "Thread", 3, color);
            Ui.Place(thread.rectTransform, toLeft ? -8f : w / 2f, 26, w / 2f + 8f, 6);
            Tweens.Scale(root, 0.5f, 1f, 0.2f);
            return root;
        }

        /// <summary>
        /// Толстый клубок ушёл: общая лунка «разъезжается» обратно в свой слот и тает, рамка соседнего снова видна.
        /// При перестройке станка — сразу.
        /// </summary>
        private static void DropTail(RectTransform tail, bool now)
        {
            if (now || tail.name != "Wide" || Tweens.Calm)
            {
                Object.Destroy(tail.gameObject);
                return;
            }

            var group = Ui.Group(tail);
            group.blocksRaycasts = false;
            var width = tail.sizeDelta.x;
            var to = (width - SlotGap) / 2f / width;
            Tweens.Run(tail, 0.22f, t =>
            {
                var e = Ease.OutCubic(t);
                tail.localScale = new Vector3(Mathf.Lerp(1f, to, e), 1f, 1f);
                group.alpha = 1f - e;
            }, null, () => Object.Destroy(tail.gameObject));
        }

        private void RefreshField()
        {
            ClearHintExtras();
            Ui.Clear(_fieldContent);
            _field.Clear();
            _hint = null;
            _hintHand = null;
            for (var c = 0; c < _game.ColumnCount; c++)
            {
                var column = _game.Column(c);
                var shown = Mathf.Min(VisibleRows, column.Count);
                for (var d = 0; d < shown; d++)
                {
                    var spool = column[d];
                    var view = SpoolView.Create(_fieldContent, _spoolSize);
                    var (x, y) = FieldPosition(c, d);
                    Ui.PlaceCenter(view.Root, x, y, _spoolSize, _spoolSize);
                    view.Bind(spool, _colors[spool.Color], d == 0 || _scissors, _game.IsSurplus(spool));
                    view.SetFabric(spool.IsColorVisible && _game.IsForeign(spool));
                    if (d > 0 && !_scissors)
                    {
                        // Задние ряды тише и чуть меньше: главное на поле — передние клубки.
                        view.Quiet();
                        view.Root.localScale = Vector3.one * 0.86f;
                    }

                    var tap = view.Tap;
                    tap.ClickSound = false;

                    // Передний клубок: пресс сразу, короткий тап на отпускании, удержание показывает цели.
                    tap.FireOnPress = _scissors || d > 0;
                    var columnIndex = c;
                    var depth = d;
                    var spoolId = spool.Id;
                    var gestureEpoch = -1;
                    if (!tap.FireOnPress)
                    {
                        tap.Pressed += () =>
                        {
                            BeginPreview(columnIndex, spoolId);
                            gestureEpoch = _previewSpool == spoolId ? _inputEpoch : -1;
                        };
                        tap.Released += () => EndPreview(spoolId);
                    }
                    tap.OnClick(() =>
                    {
                        if (!tap.FireOnPress && gestureEpoch != _inputEpoch)
                        {
                            return;
                        }
                        // Пока палец удерживал клубок, поле могло измениться от вязки или второго касания.
                        var current = _game.Column(columnIndex);
                        if (depth < current.Count && current[depth].Id == spoolId)
                        {
                            OnSpoolTap(columnIndex, depth);
                        }
                    });
                    _field.Add(new FieldSpool(view, spool.Id, c, d));

                    // Передний клубок, которому сразу есть работа, слегка «дышит».
                    if (d == 0 && spool.IsColorVisible && _game.HasWork(spool) && !spool.Locked && !spool.Frozen)
                    {
                        var root = view.Root;
                        Tweens.Run(root, 1.4f, t => root.localScale = Vector3.one * (1f + 0.04f * Ease.Bump(t)), null, null, c * 0.12f, true);
                    }
                }

                // Сколько ещё клубков за видимыми — тихой плашкой под колонкой, не поверх клубков.
                if (column.Count > VisibleRows)
                {
                    var (x, _) = FieldPosition(c, 0);
                    var rowY = 7f + (_spoolSize + 10f) * VisibleRows;
                    var more = Ui.Box(_fieldContent, "More", 7, Palette.Surface).rectTransform;
                    Ui.PlaceCenter(more, x, rowY + DepthRow / 2f - 2f, 34, 15);
                    Ui.Label(more, "+" + (column.Count - VisibleRows), 11, Palette.Ink2, 0, 0, 34, 15);
                }
            }

            DrawLinkThreads();
        }

        /// <summary>
        /// Связанные клубки на поле соединены тонкой нитью (под клубками): видно, кто с кем в паре,
        /// даже когда пар несколько.
        /// </summary>
        private void DrawLinkThreads()
        {
            var pairs = new Dictionary<int, List<FieldSpool>>();
            foreach (var f in _field)
            {
                var column = _game.Column(f.Column);
                if (f.Depth >= column.Count || column[f.Depth].Id != f.SpoolId || column[f.Depth].Link == 0)
                {
                    continue;
                }

                var link = column[f.Depth].Link;
                if (!pairs.TryGetValue(link, out var list))
                {
                    list = new List<FieldSpool>();
                    pairs[link] = list;
                }

                list.Add(f);
            }

            foreach (var list in pairs.Values)
            {
                if (list.Count != 2)
                {
                    continue;
                }

                var (ax, ay) = FieldPosition(list[0].Column, list[0].Depth);
                var (bx, by) = FieldPosition(list[1].Column, list[1].Depth);
                var dx = bx - ax;
                var dy = by - ay;
                var length = Mathf.Sqrt(dx * dx + dy * dy);
                var link = _game.Column(list[0].Column)[list[0].Depth].Link;
                var thread = Ui.Box(_fieldContent, "LinkThread", 1.5f, Palette.WithAlpha(Palette.LinkPair(link), 0.6f)).rectTransform;
                Ui.PlaceCenter(thread, (ax + bx) / 2f, (ay + by) / 2f, length, 3f);

                // Координаты поля идут сверху вниз, поэтому угол со знаком минус.
                thread.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
                thread.SetAsFirstSibling();
            }
        }

        /// <summary>
        /// Где пара связанного клубка и что мешает: связанные едут на станок только вдвоём,
        /// когда оба впереди своих колонок. Без этого отказ выглядел как баг.
        /// </summary>
        private string LinkPartnerText(int column, int depth)
        {
            var (partnerColumn, partnerDepth) = FindLinkPartner(column, depth);
            if (partnerColumn < 0)
            {
                return L.T("Связанные клубки едут на станок только вдвоём");
            }

            var where = L.F("в {0}-й колонке", partnerColumn + 1);
            if (partnerDepth > 0)
            {
                return L.F("Пара {0}, перед ней {1} {2}", where, partnerDepth, L.Plural(partnerDepth, "клубок", "клубка", "клубков"));
            }

            var partner = _game.Column(partnerColumn)[0];
            var block = partner.Locked ? L.T("под замком") : partner.Knots > 0 ? L.T("с узелком") : partner.Frozen ? L.T("во льду") : L.T("не готова");
            return L.F("Пара с таким же кольцом {0} пока {1}", where, block);
        }

        private (int Column, int Depth) FindLinkPartner(int column, int depth)
        {
            var col = _game.Column(column);
            if (depth >= col.Count || col[depth].Link == 0)
            {
                return (-1, -1);
            }

            var spool = col[depth];
            for (var c = 0; c < _game.ColumnCount; c++)
            {
                var other = _game.Column(c);
                for (var d = 0; d < other.Count; d++)
                {
                    if (other[d].Id != spool.Id && other[d].Link == spool.Link)
                    {
                        return (c, d);
                    }
                }
            }

            return (-1, -1);
        }

        /// <summary>Отказ связанному клубку: подсветить его пару, которую надо освободить первой.</summary>
        private void PunchLinkPartner(int column, int depth)
        {
            var col = _game.Column(column);
            if (depth >= col.Count || col[depth].Link == 0)
            {
                return;
            }

            var spool = col[depth];
            foreach (var f in _field)
            {
                var other = _game.Column(f.Column);
                if (f.Depth < other.Count && other[f.Depth].Id == f.SpoolId && other[f.Depth].Id != spool.Id && other[f.Depth].Link == spool.Link)
                {
                    Tweens.Punch(f.View.Root, 0.25f, 0.35f);
                    Highlight(f.View.Root, Palette.LinkPair(spool.Link));
                    var source = FindFieldView(spool.Id);
                    if (source != null)
                    {
                        Highlight(source.Root, Palette.LinkPair(spool.Link));
                    }
                    Fx.Sparkle(_fx, Ui.WorldCenter(f.View.Root), Palette.LinkPair(spool.Link), 8, 30f);
                    return;
                }
            }
        }

        private (float X, float Y) FieldPosition(int column, int depth)
        {
            var rowH = _spoolSize + 10f;
            return (8f + _colW * (column + 0.5f), 7f + rowH * depth + rowH / 2f);
        }

        private void RefreshBoosters()
        {
            foreach (var pair in _boosters)
            {
                var type = pair.Key;
                var unlocked = App.Progress.IsBoosterOpen(type, BoosterLevel);
                var available = type == BoosterType.Undo ? _game.CanUndo
                    : type == BoosterType.Slot ? _game.Slots.Count < LoomGame.MaxSlots
                    : true;
                var free = type == BoosterType.Scissors ? _run.FreeScissors : type == BoosterType.Undo ? _run.FreeUndo : 0;
                pair.Value.Refresh(unlocked, App.Progress.Stock(type), Economy.Price(type), Economy.UnlockLevel(type),
                    available, type == BoosterType.Scissors && _scissors, free);
            }
        }

        private void RefreshTimer()
        {
            if (_timerFill == null || _timerText == null || _timerFillImage == null)
            {
                return;
            }

            var left = Mathf.Max(0f, _game.TimeLeft);
            var fraction = _game.TimeLimit > 0 ? Mathf.Clamp01(left / _game.TimeLimit) : 0f;
            var seconds = Mathf.CeilToInt(left);
            if (seconds != _timerShown)
            {
                _timerShown = seconds;
                _timerText.text = $"{seconds / 60}:{seconds % 60:00}";
            }

            _timerFill.sizeDelta = new Vector2(Mathf.Max(10f, _timerW * fraction), 10f);
            var fill = _timerFillImage;
            if (_finishing)
            {
                // Финиш: время больше не важно — полоска и цифры спокойные, край экрана гаснет.
                _timerText.color = Palette.Primary;
                _timerText.rectTransform.localScale = Vector3.one;
                fill.color = Palette.Primary;
                if (_mothEdge != null)
                {
                    _mothEdge.color = Color.clear;
                }

                return;
            }

            // Последняя четверть: моль близко. Один раз — надпись у самой моли и отклик, дальше она беспокойнее.
            var near = fraction < 0.25f && _game.TimerStarted && _game.Status == GameStatus.Playing;
            if (near && !_mothNear && _built)
            {
                Fx.FloatText(_fx, MothWorld() ?? Ui.WorldCenter(_timerText.rectTransform), L.T("Моль близко!"), Palette.Danger, 18f);
                _moth?.Startle();
                Sfx.Play(Sound.Deny, 0.5f);
                Haptics.Play(Haptic.Medium);
            }

            _mothNear = near;
            UpdateMoth(fraction);

            // Сердцебиение: на каждой новой секунде короткий толчок, к концу сильнее.
            var countdown = near && left <= CountdownFrom;
            var sinceTick = 1f - (left - Mathf.Floor(left));
            var pulse = near && !Tweens.Calm ? Mathf.Exp(-sinceTick * 6f) : 0f;
            _timerText.rectTransform.localScale = Vector3.one * (1f + (countdown ? 0.35f : 0.18f) * pulse);
            _timerText.color = near ? Palette.Danger : Palette.Ink;
            fill.color = near ? Color.Lerp(Palette.Danger, Palette.WithAlpha(Palette.Danger, 0.6f), pulse)
                : fraction < 0.5f ? Color.Lerp(TimerWarm, Palette.Primary, (fraction - 0.25f) / 0.25f)
                : Palette.Primary;
            if (_timerIcon != null)
            {
                _timerIcon.color = near ? Palette.WithAlpha(Palette.Danger, 0.7f + 0.3f * pulse) : Palette.WithAlpha(Palette.Ink, 0.5f);
            }

            if (_mothEdge != null)
            {
                var edge = countdown ? 0.3f + 0.4f * pulse : near ? (Tweens.Calm ? 0.14f : 0.14f * pulse) : 0f;
                _mothEdge.color = Palette.WithAlpha(Palette.Danger, edge);
            }

            // Последние 10 секунд: тик каждую секунду — вибрация (последние 5 — заметнее), щелчок
            // и укус моли по краю рамки: видно и слышно, что она уже грызёт.
            if (countdown && seconds != _lastTickSecond)
            {
                _lastTickSecond = seconds;
                Haptics.Play(seconds <= 5 ? Haptic.Medium : Haptic.Soft);
                Sfx.Play(Sound.Click, seconds <= 5 ? 0.9f : 0.6f);
                if (_moth != null && _moth.Shown && !_moth.Flying)
                {
                    _moth.Chew(1, _ =>
                    {
                        var at = MothWorld();
                        if (at != null)
                        {
                            Fx.Sparkle(_fx, at.Value, Palette.Hex("#F4E8D6"), 2, 8f);
                        }
                    }, null);
                }
            }
        }
    }
}
