#nullable enable

using System.Collections.Generic;
using System.Linq;
using KnittedPicture.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KnittedPicture.Game
{
    /// <summary>Вязка: показ шагов станка, праздник цвета, лёд, золото и комбо за темп.</summary>
    public sealed partial class GameScreen
    {
        // ---------- Вязка ----------

        /// <summary>Самый нижний недовязанный ряд на прошлом шаге (−1 — ещё не считали).</summary>
        private int _lowestRow = -1;
        /// <summary>Отмена хода делает отложенные праздники прежнего полотна неактуальными.</summary>
        private int _celebrationEpoch;
        private float _nextPerfectBurstAt;

        /// <summary>Ряд закрыт целиком — когда долетят нитки, по нему слева направо пробегает светлая волна.</summary>
        private void ShimmerClosedRows()
        {
            var lowest = _game.LowestFront;
            if (_lowestRow >= 0 && lowest < _lowestRow)
            {
                for (var y = _lowestRow; y > lowest && y >= 0; y--)
                {
                    var row = y;
                    var epoch = _celebrationEpoch;
                    Tweens.Delay(_fx, BeadFlight, () =>
                    {
                        if (epoch == _celebrationEpoch && !_ended && !_failShown)
                        {
                            _picture.Shimmer(row);
                        }
                    });
                }
            }

            _lowestRow = lowest < 0 ? 0 : lowest;
        }

        /// <summary>Столбцы, изменённые шагом вязки: один набор на все шаги (их ~15 в секунду), без мусора.</summary>
        private readonly HashSet<int> _changed = new HashSet<int>();
        /// <summary>Один толчок клубка за шаг, даже если толщина и соседство дали несколько петель.</summary>
        private readonly HashSet<int> _wobbled = new HashSet<int>();

        private void Present(StepResult step)
        {
            var changed = _changed;
            changed.Clear();
            _wobbled.Clear();
            var rush = _tempo.Rush;
            // Один короткий импульс на рабочий шаг, не на каждую петлю; Haptics ограничивает частоту.
            if (step.Stitches.Count > 0 || step.Completed.Count > 0)
            {
                Haptics.Play(Haptic.Tap);
            }

            foreach (var stitch in step.Stitches)
            {
                changed.Add(stitch.X);
                _picture.MarkPending(stitch.X, stitch.Y);
                var slot = _slotViews[stitch.Slot];
                var from = slot != null ? Ui.WorldCenter(slot.View.Root) : Ui.WorldCenter(_slotFrames[stitch.Slot]);
                var to = _picture.CellWorld(stitch.X, stitch.Y);
                var color = _colors[stitch.Color];
                var x = stitch.X;
                var y = stitch.Y;

                // Золото засчитывается, если петля связана во время комбо.
                var gold = _game.IsGold(x, y);
                var caught = gold && _tempo.Active;

                // Нитка-клубочек летит от клубка к петле по дуге с хвостом; в «Разгоне» светится и крупнее.
                var beadColor = rush ? Color.Lerp(color, Color.white, 0.12f) : color;
                var beadSize = Mathf.Max(10f, _slotSpool * 0.28f) * (rush ? 1.25f : 1f);
                Fx.Bead(_fx, from, to, beadColor, beadSize, BeadFlight, () =>
                {
                    if (_picture.Land(x, y))
                    {
                        Fx.Sparkle(_fx, to, rush ? RushColor : color, rush ? 2 : 1, rush ? 18f : 12f);
                        Sfx.Play(Sound.Stitch, 0.5f);
                    }

                    if (gold)
                    {
                        OnGold(x, y, caught);
                    }

                    RefreshProgress();
                });

                if (_tempo.OnStitch())
                {
                    _rushCoinsPending++;
                }

                if (slot != null && _wobbled.Add(stitch.SpoolId))
                {
                    Wobble(slot.View.Root);
                }
            }

            ShowRushCoins(step.Status != GameStatus.Playing);

            foreach (var done in step.Completed)
            {
                var slot = _slotViews[done.Slot];
                if (slot != null)
                {
                    var at = Ui.WorldCenter(slot.View.Root);
                    if (done.Leftover > 0)
                    {
                        // Лишняя пряжа: цвет довязан, клубок уходит недовязанным.
                        if (done.Unraveled)
                        {
                            Fx.LooseThreads(_fx, at, slot.Color >= 0 ? _colors[slot.Color] : Palette.Ink3, _slotSpool, true);
                        }
                        else
                        {
                            Fx.Poof(_fx, at, Palette.Ink3, _slotSpool);
                        }
                        Fx.FloatText(_fx, at, done.Unraveled ? L.T("Пряжа распустилась") : L.T("лишняя пряжа"), Palette.Ink2, 14f);
                    }
                    else
                    {
                        var spoolColor = slot.Color >= 0 ? _colors[slot.Color] : Palette.Coin;
                        if (done.Perfect && Time.unscaledTime >= _nextPerfectBurstAt)
                        {
                            _nextPerfectBurstAt = Time.unscaledTime + 0.28f;
                            Fx.YarnBurst(_fx, at, Palette.Coin, spoolColor, _slotSpool * 1.15f);
                        }
                        else
                        {
                            Fx.Poof(_fx, at, spoolColor, _slotSpool * 1.3f);
                        }
                    }
                }

                Sfx.Play(Sound.SpoolDone, done.Leftover > 0 ? 0.35f : 0.7f);
                if (done.Perfect)
                {
                    Highlight(_slotFrames[done.Slot], Palette.CoinShadow);
                    if (_game.Mystery && _game.Status == GameStatus.Playing)
                    {
                        _mysteryPeekLeft = 2f;
                        _picture.SetMysteryPeek(true);
                        if (_told.Add("perfect-peek"))
                        {
                            Fx.FloatText(_fx, Ui.WorldCenter(_picture.Root), L.T("Идеально: виден следующий ряд"),
                                Palette.Primary, 17f);
                        }
                    }
                }
            }

            if (step.Completed.Any(done => done.Perfect))
            {
                Sfx.Play(Sound.Star, 0.35f);
            }

            if (step.Progress)
            {
                SyncLoom();
            }

            if (step.TimeAdded > 0f)
            {
                RefreshTimer();
                _moth?.Startle();
                if (_timerText != null)
                {
                    Fx.FloatText(_fx, Ui.WorldCenter(_timerText.rectTransform), L.F("+{0:0.#} с", step.TimeAdded), Palette.Primary, 16f);
                }
            }

            if (_game.Band > 0 && changed.Count > 0)
            {
                // Натяжение: сдвинулся нижний фронт — у других столбцов могли включиться фронты.
                _picture.RefreshColumns();
                MaybeTensionTip();
            }
            else
            {
                foreach (var x in changed)
                {
                    _picture.RefreshColumn(x);
                }
            }

            if (step.Stitches.Count > 0)
            {
                _idle = 0;
                UpdateFieldIce();
                ShimmerClosedRows();
            }

            // Цвет довязан — клубки этого цвета на поле стали лишними: показать это.
            var finished = false;
            var celebrated = false;
            var majorFeedback = step.Completed.Any(done => done.Perfect) ||
                step.Stitches.Any(stitch => _game.IsGold(stitch.X, stitch.Y));
            foreach (var stitch in step.Stitches)
            {
                if (_game.LeftOf(stitch.Color) == 0 && _finishedColors.Add(stitch.Color))
                {
                    finished = true;
                    if (!celebrated)
                    {
                        celebrated = true;
                        CelebrateColor(stitch.Color, majorFeedback);
                    }
                }
            }

            if (step.Thawed.Count > 0 || finished)
            {
                RefreshField();
            }

            foreach (var id in step.Thawed)
            {
                var view = FindFieldView(id);
                if (view != null)
                {
                    Tweens.Punch(view.Root, 0.25f, 0.3f);
                    Fx.IceShatter(_fx, Ui.WorldCenter(view.Root), IceBlue, view.Size);
                }

                Sfx.Play(Sound.Unlock, 0.7f);
            }
        }

        /// <summary>
        /// Цвет довязан: когда долетят последние нитки, его клетки пробегает волна, над ними — «Цвет готов!»,
        /// звук и ощутимый отклик. Последний цвет не празднуем отдельно — там сразу победная волна.
        /// </summary>
        private float _nextColorCelebrationAt;

        private void CelebrateColor(int color, bool afterGoldOrPerfect)
        {
            if (_game.Status == GameStatus.Won)
            {
                return;
            }

            var delay = Mathf.Max(0f, _nextColorCelebrationAt - Time.unscaledTime);
            if (afterGoldOrPerfect)
            {
                delay = Mathf.Max(delay, 0.9f);
            }
            _nextColorCelebrationAt = Time.unscaledTime + delay + 1.1f;
            var epoch = _celebrationEpoch;
            Tweens.Delay(Root, BeadFlight + 0.05f + delay, () =>
            {
                if (_ended || _failShown || epoch != _celebrationEpoch || _game.LeftOf(color) > 0)
                {
                    return;
                }

                var at = _picture.CelebrateColor(color);
                if (at == null)
                {
                    return;
                }

                Fx.FloatText(_fx, at.Value, L.T("Цвет готов!"), Color.Lerp(_colors[color], Palette.Ink, 0.45f), 22f);
                Fx.LooseThreads(_fx, at.Value, _colors[color], 55f);
                Sfx.Play(Sound.Star, 0.55f);
                Haptics.Play(Haptic.Medium);
            });
        }

        /// <summary>Числа на льду тают вместе с петлями — без перестройки поля.</summary>
        private void UpdateFieldIce()
        {
            var cracked = false;
            var pitch = 1f;
            foreach (var f in _field)
            {
                var column = _game.Column(f.Column);
                if (f.Depth < column.Count && column[f.Depth].Id == f.SpoolId)
                {
                    var ice = column[f.Depth].Ice;
                    if (ice > 0 && f.View.ShownIce > ice)
                    {
                        cracked = true;
                        pitch = Mathf.Max(pitch, 1f + 0.3f / ice);
                    }
                    f.View.SetIce(column[f.Depth].Ice);
                }
            }

            if (cracked)
            {
                Sfx.Play(Sound.Knot, 0.18f, pitch);
            }
        }

        private void OnGold(int x, int y, bool caught)
        {
            // Нитка могла долететь уже после отмены хода, а петля — засчитаться раньше: считаем каждую один раз.
            var key = y * _game.Width + x;
            if (!_game.IsKnitted(x, y) || _goldDone.ContainsKey(key))
            {
                return;
            }

            _goldDone[key] = caught;
            _picture.ResolveGold(x, y, caught);
            var at = _picture.CellWorld(x, y);
            if (!caught)
            {
                Fx.FloatText(_fx, at, L.T("мимо"), Palette.Ink3, 14f);
                return;
            }

            _goldCaught++;
            _goldCoins += _goldPerLoop;
            Fx.Sparkle(_fx, at, Palette.Coin, 10, 34f);

            // База петли вылетает из петли, надбавка мотка на спицах: из его значка в шапке.
            var bonus = _yarnBadge != null && _yarnPerk == YarnPerk.Gold ? _yarnBonus : 0;
            Fx.FloatText(_fx, at, "+" + (_goldPerLoop - bonus), Palette.CoinShadow, 20f);
            if (bonus > 0 && _yarnBadge != null)
            {
                Tweens.Punch(_yarnBadge, 0.2f, 0.3f);
                Fx.FloatText(_fx, Ui.WorldCenter(_yarnBadge) - new Vector3(0, 26f * _fx.lossyScale.x, 0), "+" + bonus, Palette.CoinShadow, 18f);
            }

            Sfx.Play(Sound.Coin, 0.7f);
            Haptics.Play(Haptic.Soft);
            RefreshGoals();
            if (_goldGoal != null)
            {
                Tweens.Punch(_goldGoal.transform.parent, 0.15f, 0.25f);
            }
        }

        // ---------- Комбо за темп ----------

        private void OnComboGrow(int combo)
        {
            if (combo < TempoCombo.ActiveFrom)
            {
                return;
            }

            var at = Ui.WorldCenter(_loomBg) + new Vector3(0, 46f * _fx.lossyScale.y, 0);
            if (combo == TempoCombo.RushFrom)
            {
                Tweens.HitStop(0.05f);
                Fx.FloatText(_fx, at, L.T("Разгон!"), RushColor, 30f);
                Sfx.Play(Sound.Combo, 0.9f);
                Haptics.Play(Haptic.Medium);
                Tweens.Punch(_loomBg, 0.06f, 0.3f);
                Fx.LooseThreads(_fx, Ui.WorldCenter(_loomBg), RushColor, 100f);
            }
            else
            {
                // Без всплывающих «Комбо ×N» на каждый тап: серию показывает плашка над станком.
                Sfx.Play(Sound.Combo, 0.45f);
            }

            Tweens.Punch(_comboChip, 0.18f, 0.22f);
        }

        private void OnComboBurned(bool wasRush)
        {
            if (wasRush)
            {
                var at = Ui.WorldCenter(_loomBg) + new Vector3(0, 46f * _fx.lossyScale.y, 0);
                Fx.FloatText(_fx, at, L.T("Разгон погас"), Palette.Ink3, 16f);
            }
        }

        /// <summary>Плашка комбо, жар «Разгона» и подсветка станка — каждый кадр.</summary>
        private void UpdateCombo(float dt)
        {
            ShowRushCoins(false);
            var active = _tempo.Active;
            var targetAlpha = active ? 1f : 0f;

            // Каждое изменение помечает слой на пересборку — трогаем только то, что правда изменилось.
            if (_comboGroup.alpha != targetAlpha)
            {
                _comboGroup.alpha = Mathf.MoveTowards(_comboGroup.alpha, targetAlpha, dt * 6f);
            }

            _loomTitle.enabled = _comboGroup.alpha < 0.5f;
            if (active)
            {
                var rush = _tempo.Rush;
                var shown = _tempo.Combo * 2 + (rush ? 1 : 0);
                if (shown != _comboShown)
                {
                    _comboShown = shown;
                    _comboText.text = rush ? L.F("Разгон ×{0}", _tempo.Combo) : L.F("Комбо ×{0}", _tempo.Combo);
                    _comboBack.color = rush ? RushColor : Palette.Link;
                    _comboBolt.enabled = rush;
                    _comboText.rectTransform.anchoredPosition = new Vector2(rush ? 24f : 10f, 0f);
                }

                var heat = new Vector2((ComboW - 20f) * _tempo.Heat, 3f);
                if (_comboHeat.sizeDelta != heat)
                {
                    _comboHeat.sizeDelta = heat;
                }
            }

            if (_tempo.Rush)
            {
                _rushClock += dt;
                // «Меньше движения»: свечение горит ровно, без пульса.
                var pulse = Tweens.Calm ? 0.9f : 0.55f + 0.45f * Ease.Bump(Mathf.Repeat(_rushClock / 0.6f, 1f));
                _rushGlow.color = Palette.WithAlpha(RushColor, pulse);
                _loomBgImage.color = Color.Lerp(Palette.LoomBg, Palette.CoinSoft, 0.8f);
            }
            else
            {
                _rushClock = 0;
                _rushGlow.color = Color.clear;
                _loomBgImage.color = Palette.LoomBg;
            }
        }

        private int _rushCoinsPending;
        private float _nextRushCoinsAt;

        /// <summary>Монеты серии собираются в одну подпись примерно за секунду; при исходе партии показываем остаток сразу.</summary>
        private void ShowRushCoins(bool flush)
        {
            if (_rushCoinsPending == 0 || (!flush && Time.unscaledTime < _nextRushCoinsAt))
            {
                return;
            }

            Fx.FloatText(_fx, Ui.WorldCenter(_comboChip) + new Vector3(0f, -28f * _fx.lossyScale.y, 0f),
                "+" + _rushCoinsPending, Palette.CoinShadow, 16f);
            Sfx.Play(Sound.Coin, 0.25f);
            _rushCoinsPending = 0;
            _nextRushCoinsAt = Time.unscaledTime + 0.9f;
        }

        private void Wobble(RectTransform root)
        {
            if (Tweens.Calm)
            {
                return;
            }

            Tweens.Kill(root, 31);
            var rotation = root.localRotation;
            var scale = root.localScale;
            Tweens.Run(root, 0.12f, t =>
            {
                root.localRotation = Quaternion.Lerp(rotation, Quaternion.identity, t) * Quaternion.Euler(0, 0, Mathf.Sin(t * Mathf.PI * 2f) * 9f);
                root.localScale = Vector3.Lerp(scale, Vector3.one, t) * (1f + 0.06f * Ease.Bump(t));
            }, null, () =>
            {
                root.localRotation = Quaternion.identity;
                root.localScale = Vector3.one;
            }, channel: 31);
        }
    }
}
