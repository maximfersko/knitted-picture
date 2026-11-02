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

        /// <summary>
        /// Подсказка ищется в фоне (перебор на сложном поле на слабом телефоне — секунды), не дольше 1,5 с.
        /// Пока ищет, кадры идут как обычно; ответ показываем, только если игрок за это время не сходил.
        /// </summary>
        private void ShowHint()
        {
            if (_hintSearching)
            {
                return;
            }

            _hintSearching = true;
            var snapshot = _game.Clone();
            var taps = _game.Taps;
            var app = App;
            System.Threading.Tasks.Task.Run(() => LevelSolver.Hint(snapshot, 20_000, HintBudgetMs)).ContinueWith(task =>
            {
                var column = task.Status == System.Threading.Tasks.TaskStatus.RanToCompletion ? task.Result : -1;
                app.RunOnMain(() =>
                {
                    _hintSearching = false;
                    if (Root == null || _finishing || _game.Taps != taps || _hint != null || _game.Status != GameStatus.Playing)
                    {
                        return;
                    }

                    PlaceHint(column);
                });
            });
        }

        private const int HintBudgetMs = 1500;

        private bool _hintSearching;

        private void PlaceHint(int column)
        {
            if (column < 0)
            {
                // Пути нет (или перебор упёрся в предел): не перебирать заново каждый кадр — только после нового хода.
                _hintFailedAt = _game.Taps;
                _idle = 0;
                return;
            }

            var (x, y) = FieldPosition(column, 0);
            var size = _spoolSize + 16f;
            _hint = BuildHintGlow(column, x, y);
            PlaceSlotHint();

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

        /// <summary>
        /// Подсказка хода — золотая середина между «не видно» и «кричит». Первые четыре секунды спокойная фаза:
        /// под клубком медленно дышит тёплое золотое сияние (период 2 с), вокруг золотое кольцо с белой каймой
        /// и лёгким пульсом масштаба (до +6%). Если хода всё ещё нет, добавляются одна волна-кольцо раз в 2 с
        /// (до 50% прозрачности, масштаб 1 → 1,6) и тихий «кивок» клубка: низкий прыжок и покачивание до 3°.
        /// Золото контрастно на белом поле и не путается с цветами пряжи. Всё лежит под клубком;
        /// «Меньше движения» — неподвижные сияние и кольцо, без волны и без смещения клубка.
        /// </summary>
        private RectTransform BuildHintGlow(int column, float x, float y)
        {
            var gold = Palette.Hex("#FFC531");
            var s = _spoolSize;
            var root = Ui.PlaceCenter(Ui.Node(_fieldContent, "Hint"), x, y, s, s);
            var target = _field.FirstOrDefault(f => f.Column == column && f.Depth == 0);
            if (target != null)
            {
                // Под клубок: сияние и волна видны вокруг него, а не поверх.
                root.SetSiblingIndex(target.View.Root.GetSiblingIndex());
            }

            var glow = Ui.Image(root, "Glow", Art.Glow, Palette.WithAlpha(gold, 0.8f));
            Ui.PlaceCenter(glow.rectTransform, s / 2f, s / 2f, s * 2.3f, s * 2.3f);
            var wave = Ui.Image(root, "Ripple", Art.Ring, Color.clear);
            Ui.PlaceCenter(wave.rectTransform, s / 2f, s / 2f, s + 14f, s + 14f);
            var ring = Ui.Image(root, "Ring", Art.Ring, gold);
            Ui.PlaceCenter(ring.rectTransform, s / 2f, s / 2f, s + 14f, s + 14f);
            var inner = Ui.Image(root, "Inner", Art.Ring, Palette.WithAlpha(Color.white, 0.9f));
            Ui.PlaceCenter(inner.rectTransform, s / 2f, s / 2f, s + 6f, s + 6f);

            var spool = target?.View.Root;
            var basePos = spool != null ? spool.anchoredPosition : Vector2.zero;
            const float period = 2.0f;
            const float calmPhase = 4f;

            // Время считается от момента, когда подсказка появилась; при «Меньше движения» стоит на нуле.
            var started = Time.unscaledTime;
            Tweens.Run(root, period * 100f, t =>
            {
                // Пока открыто окно (пауза и т. п.), время подсказки стоит: после закрытия она продолжит с того же места.
                if (App.HasPopups)
                {
                    started += Time.unscaledDeltaTime;
                }

                var time = Tweens.Calm ? 0f : Mathf.Max(0f, Time.unscaledTime - started);
                var beat = (time % period) / period;
                var pulse = 0.5f + 0.5f * Mathf.Cos(beat * Mathf.PI * 2f);
                glow.color = Palette.WithAlpha(gold, 0.3f + 0.2f * pulse);
                glow.rectTransform.localScale = Vector3.one * (0.92f + 0.12f * pulse);
                ring.rectTransform.localScale = Vector3.one * (1f + 0.06f * pulse);

                // Спокойная фаза закончилась, а хода нет: одна волна и тихий кивок клубка раз в 2 с.
                var active = time >= calmPhase;
                var cycle = active ? ((time - calmPhase) % period) / period : 0f;
                wave.rectTransform.localScale = Vector3.one * (1f + 0.6f * Ease.OutCubic(cycle));
                wave.color = active ? Palette.WithAlpha(gold, 0.5f * (1f - cycle)) : Color.clear;
                if (spool != null)
                {
                    var hop = active && cycle < 0.3f ? Mathf.Sin(cycle / 0.3f * Mathf.PI) : 0f;
                    spool.anchoredPosition = basePos + new Vector2(0f, hop * s * 0.08f);
                    var wobble = active && cycle >= 0.3f && cycle < 0.7f ? Mathf.Sin((cycle - 0.3f) / 0.4f * Mathf.PI * 3f) * (0.7f - cycle) / 0.4f : 0f;
                    spool.localRotation = Quaternion.Euler(0f, 0f, wobble * 3f);
                }
            }, null, null, 0f, true);

            _hintSpool = spool;
            _hintSpoolPos = basePos;
            return root;
        }

        /// <summary>Куда полетит клубок: первое свободное место на станке мягко светится золотом и дышит.</summary>
        private void PlaceSlotHint()
        {
            var free = -1;
            for (var i = 0; i < _slotFrames.Length && i < _game.Slots.Count; i++)
            {
                if (_game.Slots[i] == null)
                {
                    free = i;
                    break;
                }
            }

            if (free < 0)
            {
                return;
            }

            var gold = Palette.Hex("#FFC531");
            var frame = _slotFrames[free];
            var node = Ui.Stretch(Ui.Node(frame, "SlotHint"));
            var fill = Ui.Box(node, "Fill", 14, Palette.WithAlpha(gold, 0.16f));
            Ui.Stretch(fill.rectTransform);
            var edge = Ui.Image(node, "Edge", Art.RoundRectOutline(14, 3), gold);
            edge.type = Image.Type.Sliced;
            edge.pixelsPerUnitMultiplier = Art.Density;
            Ui.Stretch(edge.rectTransform);
            Tweens.Run(node, 1.4f, t =>
            {
                var pulse = 0.5f + 0.5f * Mathf.Cos(t * Mathf.PI * 2f);
                fill.color = Palette.WithAlpha(gold, 0.08f + 0.08f * pulse);
                edge.color = Palette.WithAlpha(gold, 0.35f + 0.35f * pulse);
            }, null, null, 0f, true);
            _hintSlot = node;
        }

        private RectTransform? _hintSlot;
        private RectTransform? _hintSpool;
        private Vector2 _hintSpoolPos;

        /// <summary>Убрать подсветку места на станке и вернуть клубок на место (если он ещё на поле).</summary>
        private void ClearHintExtras()
        {
            if (_hintSlot != null)
            {
                Object.Destroy(_hintSlot.gameObject);
                _hintSlot = null;
            }

            if (_hintSpool != null)
            {
                _hintSpool.anchoredPosition = _hintSpoolPos;
                _hintSpool.localRotation = Quaternion.identity;
                _hintSpool = null;
            }
        }

        private void HideHint()
        {
            ClearHintExtras();
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
        /// Натяжение впервые остановило столбик: объяснить правило у линии натяжения. Один раз за игру;
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
            ShowTip(L.T("Столбики выше линии натяжения ждут, пока довяжется ряд внизу"), line, false);
            _tipKeep = true;
        }

        /// <summary>
        /// Станок впервые заполнился на одном из первых уровней: коротко объяснить затор прямо над станком, пока
        /// игрок не встретил его на деле. Один раз за игру. Не мешает выноске уровня (ждёт, пока она уйдёт) и
        /// разбору затора (тот прячет эту выноску и показывает свою).
        /// </summary>
        private void MaybeLoomFullTip()
        {
            if (_loomTipDone || _level.id > LoomFullTipMaxLevel || _run.Pattern || _ended || _finishing || _failShown || _tip != null ||
                App.HasPopups || _game.Status != GameStatus.Playing || _game.FreeSlots != 0)
            {
                return;
            }

            // Совет уже показывали (раньше или только что): больше не спрашиваем сохранение, метод зовётся каждый кадр.
            _loomTipDone = true;
            if (!App.Progress.TakeLoomFullIntro())
            {
                return;
            }

            App.Save();
            ShowTip(L.T("Места кончились, а работы нет? Это затор. Бери клубки, которым сразу есть дело"), _loomBg, false);
            _tipKeep = true;
        }

        /// <summary>Совет о заторе показан (сейчас или раньше): проверка по сохранению больше не нужна.</summary>
        private bool _loomTipDone;

        /// <summary>До какого уровня включительно показывается совет о заторе.</summary>
        private const int LoomFullTipMaxLevel = 4;

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
