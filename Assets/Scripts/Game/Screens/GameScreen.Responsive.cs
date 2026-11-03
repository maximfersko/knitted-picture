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
        private readonly Dictionary<RectTransform, RectTransform> _previewMarks = new Dictionary<RectTransform, RectTransform>();
        private readonly HashSet<RectTransform> _previewTargets = new HashSet<RectTransform>();
        private readonly List<RectTransform> _oldPreviewTargets = new List<RectTransform>();
        private int _previewColumn = -1;
        private int _previewSpool = -1;
        private float _previewAt;
        private float _previewRefresh;
        private float _mysteryPeekLeft;
        private int _knotTone;
        private int _inputEpoch;
        private RectTransform? _bufferFeedback;
        private float _bufferFeedbackUntil;

        /// <summary>Пауза и другие действия отменяют намерение, чтобы оно не исполнилось после возврата.</summary>
        public void CancelResponsiveInput()
        {
            _inputEpoch++;
            _responsive.Clear();
            ClearBufferFeedback();
            EndPreview(_previewSpool);
        }

        /// <summary>Короткая рамка подтверждает, что касание принято в очередь.</summary>
        private void ShowBufferFeedback(int column)
        {
            ClearBufferFeedback();
            var field = _field.FirstOrDefault(f => f.Column == column && f.Depth == 0);
            if (field != null)
            {
                _bufferFeedback = Outline(field.View.Root, Palette.Primary);
                _bufferFeedbackUntil = Time.unscaledTime + ResponsiveInput.BufferWindow;
            }
        }

        private void ClearBufferFeedback()
        {
            if (_bufferFeedback != null)
            {
                _bufferFeedback.gameObject.SetActive(false);
                Object.Destroy(_bufferFeedback.gameObject);
                _bufferFeedback = null;
            }
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
            foreach (var mark in _previewMarks.Values)
            {
                if (mark != null)
                {
                    Object.Destroy(mark.gameObject);
                }
            }

            _previewMarks.Clear();
            _previewTargets.Clear();
            _oldPreviewTargets.Clear();
        }

        private void UpdateResponsiveInput(float dt)
        {
            if (!_responsive.Pending || Time.unscaledTime >= _bufferFeedbackUntil)
            {
                ClearBufferFeedback();
            }
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
            _previewTargets.Clear();
            var preview = ResponsiveInput.Preview(_game, _previewColumn);
            foreach (var stitch in preview.Stitches)
            {
                _previewTargets.Add(_picture.CellRect(stitch.X, stitch.Y));
            }

            foreach (var slot in preview.Slots)
            {
                _previewTargets.Add(_slotFrames[slot]);
            }

            RefreshPreviewMarks();
        }

        /// <summary>Сохраняет рамки неизменных целей: удержание не создаёт заново UI каждые 100 мс.</summary>
        private void RefreshPreviewMarks()
        {
            _oldPreviewTargets.Clear();
            foreach (var pair in _previewMarks)
            {
                if (pair.Key == null || !_previewTargets.Contains(pair.Key))
                {
                    if (pair.Value != null)
                    {
                        pair.Value.gameObject.SetActive(false);
                        Object.Destroy(pair.Value.gameObject);
                    }
                    _oldPreviewTargets.Add(pair.Key);
                }
            }
            foreach (var target in _oldPreviewTargets)
            {
                _previewMarks.Remove(target);
            }
            foreach (var target in _previewTargets)
            {
                if (!_previewMarks.TryGetValue(target, out var mark) || mark == null)
                {
                    var edge = Outline(target, Palette.Primary);
                    edge.name = "PreviewFeedback";
                    _previewMarks[target] = edge;
                }
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

            // Ключ может лежать за тремя видимыми рядами: указываем вход в нужную колонку.
            var deepKey = DeepKeyColumn();
            if (deepKey >= 0)
            {
                var front = _field.FirstOrDefault(f => f.Column == deepKey && f.Depth == 0);
                if (front != null)
                {
                    Highlight(front.View.Root, Palette.CoinShadow);
                }
            }
        }

        private int DeepKeyColumn()
        {
            for (var c = 0; c < _game.ColumnCount; c++)
            {
                if (_game.Column(c).Skip(VisibleRows).Any(s => s.IsKey))
                {
                    return c;
                }
            }

            return -1;
        }

        /// <summary>Пара за пределами экрана: подсвечиваем колонку, которую нужно разобрать.</summary>
        private void HighlightHiddenPartner(int column, int depth)
        {
            var partner = FindLinkPartner(column, depth);
            if (partner.Column < 0 || partner.Depth < VisibleRows)
            {
                return;
            }

            var front = _field.FirstOrDefault(f => f.Column == partner.Column && f.Depth == 0);
            if (front != null)
            {
                Highlight(front.View.Root, Palette.Link);
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
