#nullable enable

using System.Collections.Generic;
using System.Linq;
using KnittedPicture.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KnittedPicture.Game
{
    /// <summary>Моль: где сидит, прилёт и уход, что съест; рамка станка, когда мест мало.</summary>
    public sealed partial class GameScreen
    {
        // ---------- Моль ----------

        /// <summary>
        /// Где сидит моль при такой доле оставшегося времени. Пока времени больше 60% — в правом верхнем
        /// углу рамки, спокойно. Дальше идёт по верхнему краю к середине короткими перебежками
        /// (семь остановок), к 15% — над серединой картинки, головой к полотну.
        /// </summary>
        private (Vector2 At, float Facing) MothPerch(float fraction)
        {
            var corners = new Vector3[4];
            _pictureCard.GetWorldCorners(corners);
            var bottomLeft = (Vector2)_mothLayer!.InverseTransformPoint(corners[0]);
            var topRight = (Vector2)_mothLayer.InverseTransformPoint(corners[2]);
            var span = _moth!.Span;
            var startX = topRight.x - span * 0.55f;
            var endX = (bottomLeft.x + topRight.x) / 2f;
            var walk = Mathf.InverseLerp(0.6f, 0.15f, fraction);
            walk = Mathf.Floor(walk * 7f + 0.001f) / 7f;
            var at = new Vector2(Mathf.Lerp(startX, endX, walk), topRight.y - 5f);

            // Голова к полотну: почти вниз, с наклоном к середине картинки.
            var facing = MothView.Facing(new Vector2((endX - at.x) * 0.35f, -(topRight.y - bottomLeft.y) * 0.5f));
            return (at, facing);
        }

        /// <summary>Моль следит за временем: перебирается на своё место и волнуется к концу.</summary>
        private void UpdateMoth(float fraction)
        {
            if (_moth == null || !_moth.Shown || _mothEating || _finishing || _moth.Flying)
            {
                return;
            }

            _moth.Agitation = Mathf.Clamp01(Mathf.InverseLerp(0.45f, 0.05f, fraction));
            var (at, facing) = MothPerch(fraction);
            if (Vector2.Distance(at, _mothSpot) > 0.5f)
            {
                _mothSpot = at;
                _moth.MoveTo(at, facing);
            }
        }

        private void MothArrive(bool instant)
        {
            if (_moth == null || _mothLayer == null || _ended || _finishing)
            {
                return;
            }

            var fraction = _game.TimeLimit > 0 ? Mathf.Clamp01(_game.TimeLeft / _game.TimeLimit) : 1f;
            var (at, facing) = MothPerch(fraction);
            _mothSpot = at;
            _moth.Agitation = Mathf.Clamp01(Mathf.InverseLerp(0.45f, 0.05f, fraction));
            if (instant)
            {
                _moth.Place(at, facing);
                return;
            }

            var rect = _mothLayer.rect;
            _moth.FlyIn(new Vector2(rect.xMax + 40f, rect.yMax - 40f), at, facing);
        }

        /// <summary>Улететь за правый верхний край: победа, финиш.</summary>
        private void MothLeave()
        {
            if (_moth == null || _mothLayer == null || !_moth.Shown || _mothGone)
            {
                return;
            }

            _mothGone = true;
            var rect = _mothLayer.rect;
            _moth.FlyAway(new Vector2(rect.xMax + 50f, rect.yMax + 30f));
        }

        private Vector3? MothWorld() =>
            _moth != null && _mothLayer != null && _moth.Shown ? _mothLayer.TransformPoint(_moth.Position) : (Vector3?)null;

        /// <summary>
        /// Какие петли съест моль: связанная клетка рисунка ближе всего к ней и соседняя связанная.
        /// Нет связанного рисунка — клетка ткани. Только картинка: модель партии не меняется.
        /// </summary>
        private List<(int X, int Y)> MothBites()
        {
            var bites = new List<(int X, int Y)>();
            if (_moth == null || _mothLayer == null)
            {
                return bites;
            }

            var from = _moth.Position;
            (int X, int Y)? Nearest(System.Func<int, int, bool> ok)
            {
                (int X, int Y)? best = null;
                var bestDistance = float.MaxValue;
                for (var y = 0; y < _game.Height; y++)
                {
                    for (var x = 0; x < _game.Width; x++)
                    {
                        if (!ok(x, y))
                        {
                            continue;
                        }

                        var d = Vector2.Distance(from, _mothLayer!.InverseTransformPoint(_picture.CellWorld(x, y)));
                        if (d < bestDistance)
                        {
                            bestDistance = d;
                            best = (x, y);
                        }
                    }
                }

                return best;
            }

            var first = Nearest((x, y) => _game.IsKnitted(x, y) && !_game.IsFabric(x, y)) ?? Nearest((x, y) => _game.IsFabric(x, y));
            if (first == null)
            {
                return bites;
            }

            bites.Add(first.Value);
            var (fx, fy) = first.Value;
            var fabric = _game.IsFabric(fx, fy);
            var second = Nearest((x, y) => Mathf.Abs(x - fx) + Mathf.Abs(y - fy) == 1
                && (fabric ? _game.IsFabric(x, y) : _game.IsKnitted(x, y) && !_game.IsFabric(x, y)));
            if (second != null)
            {
                bites.Add(second.Value);
            }

            return bites;
        }

        /// <summary>Рамка станка: оранжевая, когда осталось одно место, красная — когда мест нет.</summary>
        private void UpdateLoomAlert(float dt)
        {
            _alertClock += dt;
            var free = _game.FreeSlots;
            if (free >= 2 || _slotFrames.Length <= 1)
            {
                _loomAlert.color = Color.clear;
                return;
            }

            var color = free == 1 ? TimerWarm : Palette.Danger;

            // «Меньше движения»: без мигания, ровная яркость; «нет мест» заметно ярче, чем «одно место».
            var pulse = Tweens.Calm ? (free == 1 ? 0.8f : 1f) : 0.45f + 0.55f * Ease.Bump(Mathf.Repeat(_alertClock / 0.9f, 1f));
            _loomAlert.color = Palette.WithAlpha(color, pulse);
        }
    }
}
