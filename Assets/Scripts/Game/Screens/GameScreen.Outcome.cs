#nullable enable

using System.Collections.Generic;
using System.Linq;
using KnittedPicture.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KnittedPicture.Game
{
    /// <summary>Финал уровня: довязка, победа, затор и его объяснение на поле, моль, окна проигрыша и спасение.</summary>
    public sealed partial class GameScreen
    {
        // ---------- Победа и поражение ----------

        /// <summary>
        /// Финиш: всё нужное уже на станке, победа неизбежна. Игроку незачем ждать и нечего решать:
        /// поле и бустеры затихают и не нажимаются, таймер останавливается, моль улетает, станок
        /// плавно разгоняется до трёх с лишним раз. Если вязать почти нечего, тихо: без надписи.
        /// </summary>
        private void StartFinish()
        {
            _finishing = true;
            _idle = 0;
            HideHint();
            HideTip();
            SetScissors(false);
            RefreshTimer();
            MothLeave();

            var left = _game.TotalStitches - _game.KnittedCount;
            if (left >= 8)
            {
                Fx.FloatText(_fx, Ui.WorldCenter(_loomBg) + new Vector3(0f, 30f * _fx.lossyScale.y, 0f), L.T("Финиш!"), Palette.Primary, 24f);
                Sfx.Play(Sound.Combo, 0.6f);
                Haptics.Play(Haptic.Medium);
            }

            var field = _fieldContent.parent.GetComponent<CanvasGroup>();
            if (field == null)
            {
                field = _fieldContent.parent.gameObject.AddComponent<CanvasGroup>();
            }

            field.blocksRaycasts = false;
            Tweens.Fade(field, field.alpha, 0.45f, 0.35f);
            foreach (var button in _boosters.Values)
            {
                button.Dimmed = true;
            }

            RefreshBoosters();
            Tweens.Run(Root, 1.1f, t => _finishSpeed = Mathf.Lerp(1f, 3.4f, t), Ease.InOutCubic);
        }

        private void AfterStatus(GameStatus status)
        {
            switch (status)
            {
                case GameStatus.Won:
                    OnWon();
                    break;
                case GameStatus.Jammed:
                    OnJam();
                    break;
                case GameStatus.TimeUp:
                    OnTimeUp();
                    break;
            }
        }

        private void OnWon()
        {
            _ended = true;
            HideHint();
            HideTip();
            SetScissors(false);
            MothLeave();
            Tweens.Delay(Root, BeadFlight + 0.1f, () =>
            {
                // Последняя петля села: короткая заморозка кадра перед «открытием» картинки.
                Tweens.HitStop(0.08f);
                _picture.Refresh();
                RefreshProgress();
                Sfx.Play(Sound.Unlock);
                Haptics.Play(Haptic.Success);
                _picture.Wave(() =>
                {
                    Tweens.Punch(_pictureCard, 0.06f, 0.4f);
                    Fx.Poof(_fx, Ui.WorldCenter(_pictureCard), Palette.Coin, _pictureCard.sizeDelta.x * 0.9f);
                    var stars = Economy.Stars(_boostersUsed, _rescues);
                    Analytics.Event(_run.Pattern ? "pattern_win" : "level_win", LevelParams(
                        ("stars", stars), ("boosters", _boostersUsed), ("rescues", _rescues),
                        ("best_combo", _tempo.Best), ("rushes", _tempo.RushCount), ("gold", _goldCaught), ("auto_finish", _finishing)));
                    var stats = new LevelStats(_tempo.RushCoins, _goldCoins, _goldCaught, _level.gold.Length, _game.Taps,
                        _tempo.Best, _boostersUsed > 0 || _rescues > 0, _game.TotalStitches, _tempo.RushCount);
                    Tweens.Delay(Root, 0.45f, () =>
                    {
                        if (_run.Pattern)
                        {
                            App.OnPatternWon(_level, stars, stats);
                        }
                        else
                        {
                            App.OnLevelWon(_level, stars, stats);
                        }
                    });
                });
            });
        }

        private void OnJam()
        {
            _failShown = true;
            SetScissors(false);
            HideHint();
            HideTip();
            _tempo.Break();

            // Затор: кадр замирает на миг, и только потом начинается встряска (её твины ждут конца заморозки).
            Tweens.HitStop(0.12f);
            Sfx.Play(Sound.Jam);
            Haptics.Play(Haptic.Warning);
            Tweens.Shake(_loomBg, 10f, 0.4f);
            Tweens.Shake(Root, 4f, 0.25f);
            foreach (var outline in _slotOutlines)
            {
                outline.enabled = true;
                var image = outline;
                Tweens.Run(image, 0.5f, t => image.color = Color.Lerp(Palette.Danger, Palette.SlotEmpty, t));
            }

            // Сначала причина прямо на поле (кольца и короткая выноска), потом окно. Тап пропускает.
            Tweens.Delay(Root, 0.45f, () =>
            {
                ExplainJam();
                Interlude(1.9f, () =>
                {
                    HideTip();
                    ShowFail(FailKind.Jam);
                });
            });
        }

        /// <summary>
        /// Затор: показать на поле, что случилось. Красное кольцо — где проблема, зелёное — что поможет,
        /// выноска в несколько слов. Один фокус, без длинных текстов: подробный совет — в окне.
        /// Нет конкретной цели: красные кольца на клубках станка (на пустом станке на рамках мест) и общая фраза
        /// (станок полон или передние клубки не взять); сам станок остаётся только якорем выноски. Выноска и кольца
        /// показываются всегда, так что окно проигрыша идёт после паузы <c>Interlude</c>.
        /// </summary>
        private void ExplainJam()
        {
            ClearJamMarks();
            RectTransform? target = null;
            var text = "";
            var below = false;
            switch (_game.Advice())
            {
                case JamAdvice.Blocked:
                    for (var c = 0; c < _game.ColumnCount && target == null; c++)
                    {
                        var column = _game.Column(c);
                        var view = FieldView(c, 0);
                        if (column.Count == 0 || view == null)
                        {
                            continue;
                        }

                        var spool = column[0];
                        if (spool.Locked)
                        {
                            var problem = JamMark(view, Palette.Danger);
                            var key = _field.FirstOrDefault(f => f.Depth < _game.Column(f.Column).Count && _game.Column(f.Column)[f.Depth].IsKey);
                            target = key != null ? JamMark(key.View.Root, Palette.Primary) : problem;
                            text = key != null ? L.T("Ключ от замка лежит глубже") : L.T("Замок откроет клубок с ключом");
                        }
                        else if (spool.Frozen)
                        {
                            target = JamMark(view, Palette.Danger);
                            text = L.T("Лёд тает от петель этого цвета");
                        }
                        else if (spool.Link != 0)
                        {
                            target = JamMark(view, Palette.Danger);
                            var (pc, pd) = FindLinkPartner(c, 0);
                            var partner = pc >= 0 ? FieldView(pc, pd) : null;
                            if (partner != null)
                            {
                                JamMark(partner, Palette.Primary);
                            }

                            text = L.T("Пара этого клубка ещё не впереди");
                        }
                    }

                    break;

                case JamAdvice.ForeignYarn:
                    for (var i = 0; i < _slotViews.Length && target == null; i++)
                    {
                        var slot = _slotViews[i];
                        var spool = i < _game.Slots.Count ? _game.Slots[i] : null;
                        if (slot != null && spool != null && _game.IsForeign(spool))
                        {
                            target = JamMark(slot.View.Root, Palette.Danger);
                            text = L.T("Эта пряжа картинке не нужна");
                        }
                    }

                    break;

                case JamAdvice.Tension:
                    var lowest = _game.LowestFront;
                    for (var x = 0; x < _game.Width && _jamMarks.Count < 3; x++)
                    {
                        if (lowest >= 0 && _game.FrontY(x) == lowest && !_game.IsHeld(x))
                        {
                            var mark = JamMark(_picture.CellRect(x, lowest), Palette.Primary);
                            target ??= mark;
                        }
                    }

                    text = L.T("Сначала довяжи этот ряд");
                    below = true;
                    break;

                case JamAdvice.Knots:
                    for (var c = 0; c < _game.ColumnCount && target == null; c++)
                    {
                        var column = _game.Column(c);
                        var view = FieldView(c, 0);
                        if (column.Count > 0 && column[0].Knots > 0 && view != null)
                        {
                            target = JamMark(view, Palette.Danger);
                            text = L.T("Узелки лучше развязывать заранее");
                        }
                    }

                    break;

                default:
                    // Станок забит не теми цветами: клубки на станке — красным, нужные картинке петли — зелёным.
                    foreach (var slot in _slotViews)
                    {
                        if (slot != null)
                        {
                            JamMark(slot.View.Root, Palette.Danger);
                        }
                    }

                    var shown = new HashSet<int>();
                    for (var x = 0; x < _game.Width && shown.Count < 3; x++)
                    {
                        var y = _game.ActiveFrontY(x);
                        if (y >= 0 && shown.Add(_game.ColorAt(x, y)))
                        {
                            JamMark(_picture.CellRect(x, y), Palette.Primary);
                        }
                    }

                    if (_jamMarks.Count > 0)
                    {
                        target = _loomBg;
                        text = L.T("Станок занят, а картинке нужны другие цвета");
                    }

                    break;
            }

            if (target == null)
            {
                // Конкретной причины на поле нет: показываем общее состояние станка, чтобы игрок не гадал, что случилось.
                ClearJamMarks();
                var marked = false;
                foreach (var slot in _slotViews)
                {
                    if (slot != null)
                    {
                        JamMark(slot.View.Root, Palette.Danger);
                        marked = true;
                    }
                }

                if (!marked)
                {
                    foreach (var frame in _slotFrames)
                    {
                        JamMark(frame, Palette.Danger);
                    }
                }

                target = _loomBg;
                text = _game.FreeSlots == 0 ? L.T("Станок полон, хода нет") : L.T("Передние клубки пока не взять");
                below = false;
            }

            ShowTip(text, target, below);
            _tipKeep = true;
        }

        private RectTransform? FieldView(int column, int depth) =>
            _field.FirstOrDefault(f => f.Column == column && f.Depth == depth)?.View.Root;

        /// <summary>
        /// Пульсирующее кольцо поверх цели и мягкое свечение под ним. Появляется с лёгким «прыжком»,
        /// дальше дышит; живёт до спасения или до перезапуска. Возвращает саму цель — к ней крепится выноска.
        /// </summary>
        private RectTransform JamMark(RectTransform target, Color color)
        {
            var size = Mathf.Max(target.rect.width, target.rect.height) + 12f;
            var root = Ui.Node(_fx, "JamMark");
            root.sizeDelta = new Vector2(size, size);
            root.position = Ui.WorldCenter(target);
            var glow = Ui.Image(root, "Glow", Art.Glow, Palette.WithAlpha(color, 0.3f));
            Ui.PlaceCenter(glow.rectTransform, size / 2f, size / 2f, size * 1.7f, size * 1.7f);
            glow.rectTransform.anchorMin = glow.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            glow.rectTransform.anchoredPosition = Vector2.zero;
            var ring = Ui.Image(root, "Ring", Art.Ring, color);
            Ui.Stretch(ring.rectTransform);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            var delay = 0.06f * _jamMarks.Count;
            root.localScale = Vector3.zero;
            Tweens.Scale(root, 0.4f, 1f, 0.32f, Ease.OutBack, null, delay);
            Tweens.Run(root, 0.9f, t =>
            {
                var bump = Ease.Bump(t);
                root.localScale = Vector3.one * (1f + 0.08f * bump);
                ring.color = Palette.WithAlpha(color, 0.75f + 0.25f * bump);
                glow.color = Palette.WithAlpha(color, 0.18f + 0.17f * bump);
            }, null, null, delay + 0.32f, true);
            _jamMarks.Add(root);
            return target;
        }

        private void ClearJamMarks()
        {
            foreach (var mark in _jamMarks)
            {
                if (mark == null)
                {
                    continue;
                }

                var group = mark.GetComponent<CanvasGroup>();
                var node = mark;
                Tweens.Fade(group, group.alpha, 0f, 0.2f, () => Object.Destroy(node.gameObject));
            }

            _jamMarks.Clear();
        }

        /// <summary>
        /// Короткая пауза перед окном (объяснение затора, моль ест полотно). Тап в любом месте её
        /// пропускает: кто понял сразу, не ждёт. then вызывается ровно один раз.
        /// </summary>
        private void Interlude(float seconds, System.Action then)
        {
            var overlay = Ui.Stretch(Ui.Node(Root, "Interlude"));
            var hit = overlay.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;
            var done = false;
            void Finish()
            {
                if (done)
                {
                    return;
                }

                done = true;
                Object.Destroy(overlay.gameObject);
                then();
            }

            var tap = overlay.gameObject.AddComponent<Tap>();
            tap.ClickSound = false;
            tap.PressFeedback = false;
            tap.OnClick(Finish);
            Tweens.Delay(overlay, seconds, Finish);
        }

        /// <summary>
        /// Время вышло: моль слетает с рамки на ближнюю связанную петлю, трижды кусает — дырка;
        /// перебегает на соседнюю, кусает дважды — вторая дырка. Потом окно. Тап пропускает сцену.
        /// </summary>
        private void OnTimeUp()
        {
            _failShown = true;
            SetScissors(false);
            HideHint();
            HideTip();
            _tempo.Break();
            if (_mothEdge != null)
            {
                _mothEdge.color = Palette.WithAlpha(Palette.Danger, 0.35f);
            }

            var bites = MothBites();
            if (_moth == null || _mothLayer == null || bites.Count == 0)
            {
                Sfx.Play(Sound.Jam);
                Haptics.Play(Haptic.Warning);
                Tweens.Delay(Root, 0.45f, () => ShowFail(FailKind.TimeUp));
                return;
            }

            _mothEating = true;
            _moth.Show();
            _moth.Agitation = 1f;
            Haptics.Play(Haptic.Medium);
            Sfx.Play(Sound.Deny, 0.7f);
            Interlude(2.6f, () =>
            {
                _mothEating = false;
                Sfx.Play(Sound.Jam);
                Haptics.Play(Haptic.Warning);
                ShowFail(FailKind.TimeUp);
            });
            MothEat(bites, 0);
        }

        /// <summary>Моль садится головой на петлю index и кусает; на последнем укусе — дырка.</summary>
        private void MothEat(List<(int X, int Y)> bites, int index)
        {
            if (!_mothEating || index >= bites.Count || _moth == null || _mothLayer == null)
            {
                return;
            }

            var (x, y) = bites[index];
            var cell = (Vector2)_mothLayer.InverseTransformPoint(_picture.CellWorld(x, y));
            var approach = cell - _moth.Position;
            var facing = approach.sqrMagnitude > 1f ? MothView.Facing(approach) : 180f;
            var forward = new Vector2(-Mathf.Sin(facing * Mathf.Deg2Rad), Mathf.Cos(facing * Mathf.Deg2Rad));
            var seat = cell - forward * _moth.Span * 0.3f;
            var chews = index == 0 ? 3 : 2;
            var moth = _moth;
            moth.MoveTo(seat, facing, () => moth.Chew(chews, bite =>
            {
                if (!_mothEating)
                {
                    return;
                }

                var world = _picture.CellWorld(x, y);
                var color = _colors[_game.ColorAt(x, y)];
                Fx.Sparkle(_fx, world, color, 2, 10f);
                Sfx.Play(Sound.Knot, 0.45f);
                Haptics.Play(Haptic.Soft);
                if (bite == chews)
                {
                    _picture.ShowHole(x, y);
                    Fx.Poof(_fx, world, color, moth.Span);
                    Haptics.Play(Haptic.Medium);
                }
            }, () => Tweens.Delay(Root, 0.12f, () => MothEat(bites, index + 1))));
        }

        /// <summary>
        /// Окно затора или моли. Спасение предлагается, только если оно правда поможет:
        /// лишний слот — когда после него есть ход, иначе ножницы (достать клубок изо льда или
        /// из-под замка, срезать чужую пряжу со станка). Если не поможет ничего — честно «заново».
        /// </summary>
        private void ShowFail(FailKind kind)
        {
            BoosterType? rescue = BoosterType.Time;
            var reason = L.T("Время вышло. Отгони моль и довяжи картинку");
            if (kind == FailKind.Jam)
            {
                // Затор при свободных местах: станок пуст, но все передние клубки заперты.
                var blocked = _game.FreeSlots > 0;
                if (_game.SlotWouldHelp)
                {
                    rescue = BoosterType.Slot;
                    reason = L.T("Места заняты, а нужного цвета на станке нет");
                }
                else if (_game.ScissorsWouldHelp && App.Progress.IsBoosterOpen(BoosterType.Scissors, BoosterLevel))
                {
                    rescue = BoosterType.Scissors;
                    reason = blocked
                        ? L.T("Передние клубки под замком, во льду или без пары. Ножницы достанут любой клубок с поля")
                        : _picture.TensionHolding
                        ? L.T("Столбики выше линии натяжения ждут нижний ряд, а его цвета на станке нет. Ножницы срежут лишний клубок со станка или достанут нужный с поля")
                        : L.T("Станок занят, а картинке нужен другой цвет. Ножницы срежут лишний клубок или достанут нужный");
                }
                else
                {
                    rescue = null;
                    reason = blocked
                        ? L.T("Передние клубки под замком, во льду или без пары. Попробуй ещё раз")
                        : _game.Slots.Count >= LoomGame.MaxSlots
                        ? L.T("Станок заполнен до предела. Попробуй ещё раз")
                        : L.T("Нужной пряжи больше нет. Попробуй ещё раз");
                }
            }

            var percent = ProgressPercent;

            // «Почти получилось»: сколько петель не хватило. Это и в событии, и в окне проигрыша.
            var left = _game.TotalStitches - _game.KnittedCount;

            // Каких цветов ждут столбцы, которые можно вязать прямо сейчас: игрок видит, чего не хватило.
            var needed = kind == FailKind.Jam && rescue != null
                ? _game.FrontColors().OrderBy(c => c).Select(c => _colors[c]).ToArray()
                : null;
            // Совет на следующую попытку: приём, которым этот затор можно было обойти.
            var advice = kind == FailKind.Jam ? AdviceText(_game.Advice()) : null;
            Analytics.Event("level_fail", LevelParams(
                ("reason", kind == FailKind.Jam ? "jam" : "time"),
                ("rescue_offered", rescue?.ToString() ?? "none"),
                ("jam_advice", kind == FailKind.Jam ? _game.Advice().ToString() : ""),
                ("left", left)));
            App.Funnel(FunnelStep.FirstFail);
            App.ShowPopup(new FailPopup(App, kind, rescue, reason, percent, left, Rescue, Restart, LeaveHome,
                rescue == BoosterType.Scissors && _run.FreeScissors > 0, StreakWarning(), needed, advice,
                _rescues, _run.VideoRescues < Economy.VideoRescuesPerLevel, () => _run.VideoRescues++));
        }

        private static string AdviceText(JamAdvice advice)
        {
            switch (advice)
            {
                case JamAdvice.Blocked:
                    return L.T("Совет: не закапывай ключи, пары связанных и цвет, от которого тает лёд");
                case JamAdvice.ForeignYarn:
                    return L.T("Совет: не бери клубки в сеточку, они только занимают место");
                case JamAdvice.Tension:
                    return L.T("Совет: сначала довязывай нижний ряд, иначе столбики выше линии натяжения ждут");
                case JamAdvice.Knots:
                    return L.T("Совет: развязывай узелки заранее, это не занимает место на станке");
                default:
                    return L.T("Совет: сначала бери короткие клубки, они быстрее освобождают место");
            }
        }

        private void Rescue(BoosterType type, string via)
        {
            _failShown = false;
            _mothEating = false;
            ClearJamMarks();
            Analytics.Event("rescue", LevelParams(("type", type.ToString()), ("via", via)));
            if (type == BoosterType.Scissors)
            {
                // Ножницы оплачиваются, когда ими режут; затор снимается этим ходом.
                _scissorsRescue = true;
                SetScissors(true);
                return;
            }

            _rescues++;
            if (type == BoosterType.Slot)
            {
                _game.AddSlot();
                RebuildSlots();
                RefreshAll();
                Sfx.Play(Sound.Unlock);
                Tweens.Scale(_slotFrames[_slotFrames.Length - 1], 0.4f, 1f, 0.3f);
            }
            else
            {
                _game.AddTime(Economy.TimeBonusSeconds);
                _lastTickSecond = -1;
                if (_mothEdge != null)
                {
                    _mothEdge.color = Color.clear;
                }

                // Моль спугнули: вздрагивает и улетает на своё место, дырки затягиваются.
                if (_picture.HasHoles)
                {
                    _picture.MendHoles(_fx);
                    Sfx.Play(Sound.Unlock);
                }

                if (_moth != null && _moth.Shown && _mothLayer != null)
                {
                    _moth.Startle();
                    var fraction = Mathf.Clamp01(_game.TimeLeft / _game.TimeLimit);
                    var (at, facing) = MothPerch(fraction);
                    _mothSpot = at;
                    _moth.Agitation = Mathf.Clamp01(Mathf.InverseLerp(0.45f, 0.05f, fraction));
                    _moth.FlyTo(at, facing);
                }

                RefreshTimer();
                RefreshAll();
            }

            if (_game.Status != GameStatus.Playing)
            {
                AfterStatus(_game.Status);
            }
        }

        /// <summary>Сколько процентов картинки связано (0..100): для аналитики и окна проигрыша.</summary>
        public int ProgressPercent => Mathf.FloorToInt(100f * _game.KnittedCount / Mathf.Max(1, _game.TotalStitches));

        /// <summary>Номер уровня для аналитики: 0 у «Узора дня».</summary>
        public int AnalyticsLevel => _run.Pattern ? 0 : _level.id;

        /// <summary>Общие параметры событий уровня: какой уровень, какая попытка, сколько длится, сколько связано.</summary>
        private (string, object)[] LevelParams(params (string, object)[] extra)
        {
            var list = new List<(string, object)>
            {
                ("level", _run.Pattern ? 0 : _level.id),
                ("pattern", _run.Pattern),
                ("attempt", _run.Attempt),
                ("seconds", Mathf.RoundToInt(Time.realtimeSinceStartup - _run.StartedAt)),
                ("progress", ProgressPercent),
                ("taps", _game.Taps),
            };
            list.AddRange(extra);
            return list.ToArray();
        }

        private void TrackBooster(BoosterType type, bool free) =>
            Analytics.Event("booster", LevelParams(("type", type.ToString()), ("free", free)));

        private void Restart()
        {
            // Заново посреди уровня (не из окна проигрыша и не после победы) — это уход с уровня.
            if (!_ended && !_failShown)
            {
                Analytics.Event("level_quit", LevelParams(("via", "restart")));
            }

            if (_run.Pattern)
            {
                App.StartPattern();
            }
            else
            {
                App.StartLevel(_level.id, restart: true);
            }
        }

        /// <summary>Уйти домой посреди уровня — как сдаться: серия побед обрывается (повтор её не трогает).</summary>
        private void LeaveHome()
        {
            if (!_ended)
            {
                Analytics.Event("level_quit", LevelParams(("via", "home"), ("after_fail", _failShown)));
            }

            if (!_run.Pattern && !_ended && _game.Taps > 0 && !App.Progress.IsCompleted(_level.id))
            {
                App.Progress.BreakStreak();
                App.Save();
            }

            App.ShowHome();
        }
    }
}
