#nullable enable

using System.Collections.Generic;
using System.Linq;
using KnittedPicture.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KnittedPicture.Game
{
    /// <summary>Подсказки: рука при паузе, выноски уровня и натяжения, к чему они относятся.</summary>
    public sealed partial class GameScreen
    {
        // ---------- Подсказки ----------

        private void ShowHint()
        {
            var column = LevelSolver.Hint(_game);
            if (column < 0)
            {
                // Пути нет (или перебор упёрся в предел): не перебирать заново каждый кадр — только после нового хода.
                _hintFailedAt = _game.Taps;
                _idle = 0;
                return;
            }

            var (x, y) = FieldPosition(column, 0);
            var ring = Ui.Image(_fieldContent, "Hint", Art.Ring, Palette.Primary);
            var size = _spoolSize + 16f;
            Ui.PlaceCenter(ring.rectTransform, x, y, size, size);
            _hint = ring.rectTransform;
            var hint = _hint;
            Tweens.Run(hint, 0.9f, t => hint.localScale = Vector3.one * (1f + 0.12f * Ease.Bump(t)), null, null, 0f, true);

            // На первом уровне — рука, как во вступлении: тапни сюда.
            if (_level.id == 1)
            {
                var hand = Ui.Node(_fieldContent, "HintHand");
                Ui.PlaceCenter(hand, x + size * 0.35f, y + size * 0.45f, 52, 52);
                var halo = Ui.Image(hand, "Halo", Art.Icon(Icon.Hand), Color.white);
                Ui.PlaceCenter(halo.rectTransform, 26, 26, 60, 60);
                var icon = Ui.Image(hand, "Hand", Art.Icon(Icon.Hand), Palette.Ink);
                Ui.PlaceCenter(icon.rectTransform, 26, 26, 52, 52);
                var start = hand.anchoredPosition;
                Tweens.Run(hand, 0.9f, t =>
                {
                    hand.anchoredPosition = start + new Vector2(6f, -6f) * Ease.Bump(t);
                    hand.localScale = Vector3.one * (1f - 0.1f * Ease.Bump(t));
                }, null, null, 0f, true);
                _hintHand = hand;
            }
        }

        private void HideHint()
        {
            if (_hint != null)
            {
                Object.Destroy(_hint.gameObject);
                _hint = null;
            }

            if (_hintHand != null)
            {
                Object.Destroy(_hintHand.gameObject);
                _hintHand = null;
            }

            _idle = 0;
        }

        /// <summary>
        /// Подсказка уровня — лёгкая выноска рядом с тем, о чём она: над особым клубком на поле
        /// (с «?», узелком, замком, льдом…), под целью по золоту, над картинкой.
        /// Белая, небольшая, со стрелкой; не перехватывает тапы, исчезает сама или с первым ходом.
        /// Вернуть её можно тапом по плашке уровня.
        /// </summary>
        /// <summary>Подсказка уровня — в начале, пока не сделан первый тап.</summary>
        private void ShowTip()
        {
            if (_game.Taps > 0)
            {
                return;
            }

            ShowTip(_level.tip, null, false);
        }

        /// <summary>
        /// Натяжение впервые остановило столбик: объяснить правило прямо на пунктире. Один раз за игру;
        /// подсказка не гаснет от следующего тапа, чтобы её успели прочитать.
        /// </summary>
        private void MaybeTensionTip()
        {
            var line = _picture.TensionLine;
            if (line == null || _ended || _tip != null || App.HasPopups || !App.Progress.TakeTensionIntro())
            {
                return;
            }

            App.Save();
            ShowTip(L.T("Столбики выше пунктира ждут, пока довяжется ряд внизу"), line, false);
            _tipKeep = true;
        }

        private void ShowTip(string tipText, RectTransform? target, bool below)
        {
            if (_ended || _finishing || _tip != null || string.IsNullOrEmpty(tipText))
            {
                return;
            }

            _tipKeep = false;
            if (target == null)
            {
                (target, below) = TipTarget(tipText.ToLowerInvariant());
            }

            // Станок — главное место в игре, его выноска не закрывает: про клубок поля она ложится под ним
            // (поле прямо под станком), а общий совет — сверху, над картинкой.
            if (target != null && _fieldContent != null && target.IsChildOf(_fieldContent))
            {
                below = true;
            }

            var maxW = Mathf.Min(W - 2 * Pad, 320f);
            var root = Ui.Node(Root, "Tip");
            _tip = root;

            var label = Ui.Label(root, L.T(tipText), 14, Palette.Ink, 40, 0, maxW - 52, 10, true, TextAnchor.MiddleLeft);
            label.lineSpacing = 0.9f;
            var textW = Mathf.Min(maxW - 52, Mathf.Max(label.preferredWidth, 60f));
            label.rectTransform.sizeDelta = new Vector2(textW, 10);
            var bubbleW = textW + 52f;
            var bubbleH = Mathf.Max(40f, label.preferredHeight + 18f);

            // Где цель на экране — в точках макета от левого верхнего угла экрана.
            float cx, top, bottom;
            if (target != null)
            {
                var corners = new Vector3[4];
                target.GetWorldCorners(corners);
                var a = Root.InverseTransformPoint(corners[0]);
                var b = Root.InverseTransformPoint(corners[2]);
                cx = (a.x + b.x) / 2f + W / 2f;
                top = H / 2f - b.y;
                bottom = H / 2f - a.y;
            }
            else
            {
                // Общий совет — у верхнего края картинки: верхние ряды вяжутся последними, их можно закрыть.
                cx = W / 2f;
                top = bottom = _top + 4f;
            }

            const float arrow = 9f;
            var x = Mathf.Clamp(cx - bubbleW / 2f, Pad, W - Pad - bubbleW);
            if (target != null && below && bottom + arrow + 2f + bubbleH > H - 4f)
            {
                // Под целью не помещается (нижний край экрана) — над ней.
                below = false;
            }

            var y = target == null ? top : below ? bottom + arrow + 2f : top - bubbleH - arrow - 2f;
            Ui.Place(root, x, y, bubbleW, bubbleH);
            var arrowX = Mathf.Clamp(cx - x, 22f, bubbleW - 22f);

            var shadow = Ui.Box(root, "Shadow", 14, Palette.WithAlpha(Palette.Ink, 0.12f));
            Ui.Place(shadow.rectTransform, 0, 3, bubbleW, bubbleH);

            // Стрелка — повёрнутый квадратик с контуром; лицо выноски закрывает его половину.
            if (target != null)
            {
                var tail = Ui.Box(root, "Arrow", 3, Palette.Card).rectTransform;
                Ui.PlaceCenter(tail, arrowX, below ? 0f : bubbleH, 14, 14);
                tail.localRotation = Quaternion.Euler(0, 0, 45f);
                Ui.Outline(tail, 3, Palette.Line, 1.5f);
            }

            var face = Ui.Box(root, "Face", 14, Palette.Card);
            Ui.Place(face.rectTransform, 0, 0, bubbleW, bubbleH);
            Ui.Outline(face.rectTransform, 14, Palette.Line, 1.5f);
            if (target != null)
            {
                // Без контура в месте стыка: выноска и стрелка читаются одной фигурой.
                var patch = Ui.Box(root, "Patch", 2, Palette.Card).rectTransform;
                Ui.PlaceCenter(patch, arrowX, below ? 1.5f : bubbleH - 1.5f, 16, 4);
            }

            var bulb = Ui.Image(root, "Bulb", Art.Circle, Palette.CoinSoft);
            Ui.PlaceCenter(bulb.rectTransform, 22, bubbleH / 2f, 26, 26);
            Ui.IconImage(root, Icon.Info, Palette.CoinShadow, 22, bubbleH / 2f, 16);
            label.transform.SetAsLastSibling();
            Ui.Place(label.rectTransform, 40, 0, textW, bubbleH);

            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            Tweens.Fade(group, 0f, 1f, 0.25f);
            Tweens.Scale(root, 0.85f, 1f, 0.3f, Ease.OutBack);
            var rest = root.anchoredPosition;
            var lift = below ? -1f : 1f;
            Tweens.Run(root, 1.8f, t => root.anchoredPosition = rest + new Vector2(0, lift * 3f * Ease.Bump(t)), null, null, 0.3f, true);
            _tipShownAt = Time.unscaledTime;
            Tweens.Delay(root, Mathf.Clamp(1.5f + tipText.Length * 0.04f, 3f, 4.5f), HideTip);
        }

        /// <summary>
        /// К чему относится подсказка: по словам в ней ищем особый клубок на видимой части поля,
        /// цель по золоту, картинку. below — выноска под целью (цели наверху), иначе над ней.
        /// null — общий совет, выноска ложится на пустой станок.
        /// </summary>
        private (RectTransform? target, bool below) TipTarget(string tipText)
        {
            // Без учёта регистра: подсказка часто начинается с заглавной («Тапни клубок…»).
            var tip = tipText.ToLowerInvariant();
            System.Func<Spool, bool>? match = null;
            if (tip.Contains("?") || tip.Contains("скрыт"))
            {
                match = s => !s.IsColorVisible;
            }
            else if (tip.Contains("узел"))
            {
                match = s => s.Knots > 0;
            }
            else if (tip.Contains("замок") || tip.Contains("ключ"))
            {
                match = s => s.Locked || s.IsKey;
            }
            else if (tip.Contains("ледян") || tip.Contains("лёд"))
            {
                match = s => s.Frozen;
            }
            else if (tip.Contains("связан"))
            {
                match = s => s.Link != 0;
            }
            else if (tip.Contains("толст"))
            {
                match = s => s.Thick;
            }
            else if (tip.Contains("радуж"))
            {
                match = s => s.Rainbow;
            }
            else if (tip.Contains("цвета ткани") || tip.Contains("цвета фона") || tip.Contains("чуж") || tip.Contains("сеточк"))
            {
                match = s => _game.IsForeign(s);
            }
            else if (tip.Contains("тапни клубок"))
            {
                match = s => _game.HasWork(s);
            }

            if (match != null)
            {
                foreach (var f in _field)
                {
                    var column = _game.Column(f.Column);
                    if (f.Depth < column.Count && column[f.Depth].Id == f.SpoolId && match(column[f.Depth]))
                    {
                        return (f.View.Root, false);
                    }
                }
            }

            if (tip.Contains("золот"))
            {
                return _goldGoal != null ? ((RectTransform)_goldGoal.transform.parent, true) : (_pictureCard, true);
            }

            if (tip.Contains("фронт") || tip.Contains("в рамке") || tip.Contains("снизу вверх"))
            {
                return (_pictureCard, true);
            }

            return (null, false);
        }

        private void HideTip()
        {
            if (_tip == null)
            {
                return;
            }

            var tip = _tip;
            _tip = null;
            _tipKeep = false;
            var group = tip.GetComponent<CanvasGroup>();
            Tweens.Fade(group, group.alpha, 0f, 0.2f, () => Object.Destroy(tip.gameObject));
        }

        private void PunchField(int spoolId)
        {
            var view = FindFieldView(spoolId);
            if (view != null)
            {
                Tweens.Scale(view.Root, 0.6f, 1f, 0.3f);
            }
        }

        private SpoolView? FindFieldView(int spoolId) => _field.FirstOrDefault(f => f.SpoolId == spoolId)?.View;
    }
}
