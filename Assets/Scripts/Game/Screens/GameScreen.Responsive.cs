#nullable enable

using System.Collections.Generic;
using System.Linq;
using KnittedPicture.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KnittedPicture.Game
{
    /// <summary>Предпросмотр, короткая очередь касания и адресные спокойные подсказки.</summary>
    public sealed partial class GameScreen
    {
        private readonly ResponsiveInput _responsive = new ResponsiveInput();
        private readonly List<RectTransform> _previewMarks = new List<RectTransform>();
        private int _previewColumn = -1;
        private int _previewSpool = -1;
        private float _previewAt;
        private float _previewRefresh;
        private float _mysteryPeekLeft;
        private int _knotTone;
        private int _inputEpoch;

        /// <summary>Пауза и другие действия отменяют намерение, чтобы оно не исполнилось после возврата.</summary>
        public void CancelResponsiveInput()
        {
            _inputEpoch++;
            _responsive.Clear();
            EndPreview(_previewSpool);
        }

        public override void Dispose()
        {
            CancelResponsiveInput();
            base.Dispose();
        }

        private void BeginPreview(int column, int spoolId)
        {
            CancelResponsiveInput();
            if (_ended || _failShown || _finishing || _scissors || App.HasPopups)
            {
                return;
            }

            _previewColumn = column;
            _previewSpool = spoolId;
            _previewAt = Time.unscaledTime + 0.22f;
            _previewRefresh = 0f;
        }

        private void EndPreview(int spoolId)
        {
            if (spoolId != _previewSpool)
            {
                return;
            }

            _previewColumn = -1;
            _previewSpool = -1;
            ClearPreviewMarks();
        }

        private void ClearPreviewMarks()
        {
            foreach (var mark in _previewMarks)
            {
                if (mark != null)
                {
                    Object.Destroy(mark.gameObject);
                }
            }

            _previewMarks.Clear();
        }

        private void UpdateResponsiveInput(float dt)
        {
            if (_ended || _failShown || _finishing)
            {
                CancelResponsiveInput();
                return;
            }

            if (_mysteryPeekLeft > 0f)
            {
                _mysteryPeekLeft = Mathf.Max(0f, _mysteryPeekLeft - dt);
                if (_mysteryPeekLeft == 0f)
                {
                    _picture.SetMysteryPeek(false);
                }
            }

            if (_previewColumn < 0 || Time.unscaledTime < _previewAt)
            {
                return;
            }

            var column = _game.Column(_previewColumn);
            if (column.Count == 0 || column[0].Id != _previewSpool)
            {
                EndPreview(_previewSpool);
                return;
            }

            _previewRefresh -= dt;
            if (_previewRefresh > 0f)
            {
                return;
            }

            _previewRefresh = 0.1f;
            ClearPreviewMarks();
            var preview = ResponsiveInput.Preview(_game, _previewColumn);
            foreach (var stitch in preview.Stitches)
            {
                _previewMarks.Add(Outline(_picture.CellRect(stitch.X, stitch.Y), Palette.Primary));
            }

            foreach (var slot in preview.Slots)
            {
                _previewMarks.Add(Outline(_slotFrames[slot], Palette.Primary));
            }
        }

        /// <summary>Контрастная рамка не зависит от анимаций и не перехватывает касания.</summary>
        private static RectTransform Outline(RectTransform parent, Color color)
        {
            var edge = Ui.Image(parent, "Feedback", Art.RoundRectOutline(8f, 2.5f), color);
            edge.type = Image.Type.Sliced;
            edge.pixelsPerUnitMultiplier = Art.Density;
            edge.raycastTarget = false;
            return Ui.Stretch(edge.rectTransform);
        }

        private void Highlight(RectTransform target, Color color)
        {
            var previous = target.Find("Feedback");
            if (previous != null)
            {
                Object.Destroy(previous.gameObject);
            }
            var ring = Outline(target, color);
            Tweens.Delay(ring, 1.1f, () => Object.Destroy(ring.gameObject));
        }

        private void HighlightKey(int column, int depth)
        {
            foreach (var field in _field)
            {
                var spool = _game.Column(field.Column)[field.Depth];
                if (spool.IsKey)
                {
                    Highlight(field.View.Root, Palette.CoinShadow);
                    var source = _field.FirstOrDefault(f => f.Column == column && f.Depth == depth);
                    if (source != null && !Tweens.Calm)
                    {
                        Fx.Bead(_fx, Ui.WorldCenter(source.View.Root), Ui.WorldCenter(field.View.Root),
                            Palette.Coin, 9f, 0.3f, () => { });
                    }
                }
            }
        }

        private void HighlightIce(int column)
        {
            var spool = _game.Column(column)[0];
            var view = FindFieldView(spool.Id);
            if (view != null)
            {
                Highlight(view.Root, IceBlue);
            }

            for (var x = 0; x < _game.Width; x++)
            {
                var y = _game.ActiveFrontY(x);
                if (y >= 0 && _game.ColorAt(x, y) == spool.Color)
                {
                    Highlight(_picture.CellRect(x, y), IceBlue);
                }
            }
        }
    }
}
