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

        /// <summary>Ряд закрыт целиком — когда долетят нитки, по нему слева направо пробегает светлая волна.</summary>
        private void ShimmerClosedRows()
        {
            var lowest = _game.LowestFront;
            if (_lowestRow >= 0 && lowest < _lowestRow)
            {
                for (var y = _lowestRow; y > lowest && y >= 0; y--)
                {
                    var row = y;
                    Tweens.Delay(_fx, BeadFlight, () => _picture.Shimmer(row));
                }
            }

            _lowestRow = lowest < 0 ? 0 : lowest;
        }

        /// <summary>Столбцы, изменённые шагом вязки: один набор на все шаги (их ~15 в секунду), без мусора.</summary>
        private readonly HashSet<int> _changed = new HashSet<int>();

        private void Present(StepResult step)
        {
            var changed = _changed;
            changed.Clear();
            var rush = _tempo.Rush;
            // В «Разгоне» петли идут потоком: лёгкий тик вибрации в такт (Haptics сам не чаще раза в 50 мс).
            if (rush && step.Stitches.Count > 0)
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
                var beadColor = rush ? Color.Lerp(color, Color.white, 0.3f) : color;
                var beadSize = Mathf.Max(10f, _slotSpool * 0.28f) * (rush ? 1.25f : 1f);
                Fx.Bead(_fx, from, to, beadColor, beadSize, BeadFlight, () =>
                {
                    if (_picture.Land(x, y))
                    {
                        Fx.Sparkle(_fx, to, rush ? RushColor : color, rush ? 5 : 3, rush ? 24f : 16f);
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
                    Fx.FloatText(_fx, from, "+1", Palette.CoinShadow, 16f);
                    Sfx.Play(Sound.Coin, 0.25f);
                }

                if (slot != null)
                {
                    Wobble(slot.View.Root);
                }
            }

            foreach (var done in step.Completed)
            {
                var slot = _slotViews[done.Slot];
                if (slot != null)
                {
                    var at = Ui.WorldCenter(slot.View.Root);
                    if (done.Leftover > 0)
                    {
                        // Лишняя пряжа: цвет довязан, клубок уходит недовязанным.
                        Fx.Poof(_fx, at, Palette.Ink3, _slotSpool);
                        Fx.FloatText(_fx, at, L.T("лишняя пряжа"), Palette.Ink2, 14f);
                    }
                    else
                    {
                        Fx.Poof(_fx, at, slot.Color >= 0 ? _colors[slot.Color] : Palette.Coin, _slotSpool * 1.3f);
                    }
                }

                Sfx.Play(Sound.SpoolDone, done.Leftover > 0 ? 0.35f : 0.7f);
                Haptics.Play(Haptic.Tap);
            }

            if (step.Stitches.Count > 0 || step.Completed.Count > 0)
            {
                SyncLoom();
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
            foreach (var stitch in step.Stitches)
            {
                if (_game.LeftOf(stitch.Color) == 0 && _finishedColors.Add(stitch.Color))
                {
                    finished = true;
                    CelebrateColor(stitch.Color);
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
                    Fx.Sparkle(_fx, Ui.WorldCenter(view.Root), IceBlue, 8, 30f);
                }

                Sfx.Play(Sound.Unlock, 0.7f);
            }
        }

        /// <summary>
        /// Цвет довязан: когда долетят последние нитки, его клетки пробегает волна, над ними — «Цвет готов!»,
        /// звук и ощутимый отклик. Последний цвет не празднуем отдельно — там сразу победная волна.
        /// </summary>
        private void CelebrateColor(int color)
        {
            if (_game.Status == GameStatus.Won)
            {
                return;
            }

            Tweens.Delay(Root, BeadFlight + 0.05f, () =>
            {
                if (_ended)
                {
                    return;
                }

                var at = _picture.CelebrateColor(color);
                if (at == null)
                {
                    return;
                }

                Fx.FloatText(_fx, at.Value, L.T("Цвет готов!"), Color.Lerp(_colors[color], Palette.Ink, 0.45f), 22f);
                Fx.Sparkle(_fx, at.Value, _colors[color], 10, 50f);
                Fx.Poof(_fx, at.Value, Color.Lerp(_colors[color], Color.white, 0.3f), 110f);
                Sfx.Play(Sound.Star, 0.55f);
                Haptics.Play(Haptic.Medium);
            });
        }

        /// <summary>Числа на льду тают вместе с петлями — без перестройки поля.</summary>
        private void UpdateFieldIce()
        {
            foreach (var f in _field)
            {
                var column = _game.Column(f.Column);
                if (f.Depth < column.Count && column[f.Depth].Id == f.SpoolId)
                {
                    f.View.SetIce(column[f.Depth].Ice);
                }
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
            Fx.FloatText(_fx, at, "+" + _goldPerLoop, Palette.CoinShadow, 20f);
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
                Fx.FloatText(_fx, at, L.T("Разгон!"), RushColor, 30f);
                Sfx.Play(Sound.Combo, 0.9f);
                Haptics.Play(Haptic.Medium);
                Tweens.Punch(_loomBg, 0.06f, 0.3f);
                Fx.Sparkle(_fx, Ui.WorldCenter(_loomBg), RushColor, 12, 140f);
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
                var pulse = 0.55f + 0.45f * Ease.Bump(Mathf.Repeat(_rushClock / 0.6f, 1f));
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

        private void Wobble(RectTransform root)
        {
            Tweens.Run(root, 0.12f, t =>
            {
                root.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * Mathf.PI * 2f) * 9f);
                root.localScale = Vector3.one * (1f + 0.06f * Ease.Bump(t));
            }, null, () =>
            {
                root.localRotation = Quaternion.identity;
                root.localScale = Vector3.one;
            });
        }
    }
}
