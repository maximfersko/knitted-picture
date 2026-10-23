#nullable enable

using System;
using KnittedPicture.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Таймлапс «как вязалось»: бледный эскиз, по которому «змейкой» проходят цветные петли: первый ряд снизу
    /// слева направо, второй справа налево и так далее. Слои: эскиз; маска «готовые ряды» (всё ниже текущего ряда);
    /// маска «текущий ряд» (растёт от левого или правого края). Обе маски показывают одну готовую текстуру
    /// миниатюры (<see cref="MiniPicture.TextureOf"/>), поэтому рядов не сдвигает даже у неквадратных картинок.
    /// «Меньше движения»: линейно за секунду, без звука, толчка и искр.
    /// </summary>
    public sealed class KnitReplay
    {
        /// <summary>Канал твина прогона: новый прогон и тап останавливают прежний.</summary>
        private const int Channel = 41;

        private readonly RectTransform _fxLayer;
        private readonly RectTransform _doneMask;
        private readonly RectTransform _rowMask;
        private readonly RectTransform _rowImage;
        private readonly float _width;
        private readonly float _height;
        private readonly int _rows;
        private readonly float _size;

        /// <summary>Какой ряд сейчас вяжется (-1 — ещё не начат или сброшен).</summary>
        private int _row = -1;

        /// <summary>Доля текущего ряда, что показана; NaN — ещё не выставлена.</summary>
        private float _fraction = float.NaN;

        private bool _celebrate = true;

        private KnitReplay(RectTransform parent, LevelData level, float cx, float cy, float size)
        {
            _fxLayer = parent;
            _size = size;
            _rows = Mathf.Max(1, level.height);

            // Пропорции как у MiniPicture.Create: клетка миниатюры шире, чем выше.
            var aspect = (float)level.width * MetaRaster.MiniCellW / (level.height * MetaRaster.MiniCellH);
            _width = aspect >= 1 ? size : size * aspect;
            _height = aspect >= 1 ? size / aspect : size;

            Root = Ui.PlaceCenter(Ui.Node(parent, "KnitReplay"), cx, cy, _width, _height);

            // 1. Бледный эскиз на весь размер: он же ловит тапы.
            var sketch = Root.gameObject.AddComponent<RawImage>();
            sketch.texture = MiniPicture.TextureOf(level, faded: true);
            sketch.raycastTarget = true;

            // 2. Готовые ряды: маска от низа картинки, внутри готовая текстура во весь размер.
            _doneMask = BottomLeft(Ui.Node(Root, "Done"));
            _doneMask.gameObject.AddComponent<RectMask2D>();
            var doneImage = BottomLeft(Ui.Node(_doneMask, "Picture"));
            AddPicture(doneImage, level);
            doneImage.sizeDelta = new Vector2(_width, _height);

            // 3. Текущий ряд: узкая маска на высоте ряда, внутри та же текстура, сдвинутая на обратное смещение маски.
            _rowMask = BottomLeft(Ui.Node(Root, "Row"));
            _rowMask.gameObject.AddComponent<RectMask2D>();
            _rowImage = BottomLeft(Ui.Node(_rowMask, "Picture"));
            AddPicture(_rowImage, level);
            _rowImage.sizeDelta = new Vector2(_width, _height);

            var tap = Root.gameObject.AddComponent<Tap>();
            tap.ClickSound = false;
            tap.PressFeedback = false;
            tap.OnClick(() =>
            {
                if (Playing)
                {
                    ShowDone();
                }
                else
                {
                    IdleTap?.Invoke();
                }
            });

            Clear();
        }

        /// <summary>Картинка с таймлапсом, центр (cx, cy) в координатах parent; size — большая сторона, как у <see cref="MiniPicture.Create"/>.</summary>
        public static KnitReplay Create(RectTransform parent, LevelData level, float cx, float cy, float size) =>
            new KnitReplay(parent, level, cx, cy, size);

        /// <summary>Корень вида: его размер равен размеру картинки, центр там, где его просили.</summary>
        public RectTransform Root { get; }

        /// <summary>Прогон идёт (в том числе пока ждёт задержку).</summary>
        public bool Playing { get; private set; }

        /// <summary>Что делать, если тапнули по виду, когда прогона нет (например, запустить заново).</summary>
        public Action? IdleTap { get; set; }

        /// <summary>
        /// Искры и толчок в конце ближайшего прогона. Сбрасывается в true после каждого прогона: так экран победы,
        /// у которого свой праздник на входе, гасит дубль один раз, а повторы потом празднуют как обычно.
        /// </summary>
        public bool Celebrate
        {
            get => _celebrate;
            set => _celebrate = value;
        }

        /// <summary>Высота низа ряда k в точках вида: ряд 0 снизу, у k == высота картинки это вся картинка.</summary>
        private float RowTop(int k) => k >= _rows ? _height : _height * k * MetaRaster.MiniCellH / (_rows * MetaRaster.MiniCellH + 6f);

        /// <summary>Запустить с пустого полотна: seconds на всю картинку, через delay секунд; done — когда довязано.</summary>
        public void Play(float seconds, float delay = 0f, Action? done = null)
        {
            Tweens.Kill(Root, Channel);
            Clear();
            Playing = true;
            var celebrate = _celebrate;
            _celebrate = true;
            var calm = Tweens.Calm;
            Tweens.Run(Root, calm ? 1f : Mathf.Max(0.1f, seconds), t => Apply(t * _rows), calm ? null : (Func<float, float>)Ease.InOutCubic, () =>
            {
                Complete();
                if (celebrate && !Tweens.Calm)
                {
                    Tweens.Punch(Root, 0.04f, 0.3f);
                    Fx.Sparkle(_fxLayer, Ui.WorldCenter(Root), Palette.Coin, 8, _size * 0.5f);
                }

                done?.Invoke();
            }, delay, false, Channel);
        }

        /// <summary>Сразу показать готовую картинку и остановить прогон.</summary>
        public void ShowDone()
        {
            Tweens.Kill(Root, Channel);
            _celebrate = true;
            Complete();
        }

        private void Complete()
        {
            Playing = false;
            _row = _rows;
            _fraction = float.NaN;
            _doneMask.sizeDelta = new Vector2(_width, _height);
            _rowMask.sizeDelta = Vector2.zero;
        }

        /// <summary>Пустое полотно: видно только эскиз.</summary>
        private void Clear()
        {
            _row = -1;
            _fraction = float.NaN;
            _doneMask.sizeDelta = new Vector2(_width, 0f);
            _rowMask.sizeDelta = Vector2.zero;
        }

        /// <summary>cells — сколько рядов связано (дробное); меняет геометрию только когда что-то сдвинулось.</summary>
        private void Apply(float cells)
        {
            var row = Mathf.Clamp(Mathf.FloorToInt(cells), 0, _rows - 1);
            var fraction = cells >= _rows ? 1f : Mathf.Clamp01(cells - row);
            var rowChanged = row != _row;
            if (!rowChanged && Mathf.Approximately(fraction, _fraction))
            {
                return;
            }

            if (rowChanged)
            {
                _row = row;
                _doneMask.sizeDelta = new Vector2(_width, RowTop(row));
                if ((row & 1) == 1 && !Tweens.Calm)
                {
                    // Тихий стук спиц через ряд.
                    Sfx.Play(Sound.Stitch, 0.25f);
                }
            }

            _fraction = fraction;
            var top = RowTop(row);
            var rowHeight = RowTop(row + 1) - top;
            var shown = _width * fraction;

            // Змейка: чётные ряды (первый снизу) идут слева направо, нечётные справа налево.
            var x = (row & 1) == 0 ? 0f : _width - shown;
            _rowMask.anchoredPosition = new Vector2(x, top);
            _rowMask.sizeDelta = new Vector2(shown, rowHeight);
            _rowImage.anchoredPosition = new Vector2(-x, -top);
        }

        private static RectTransform BottomLeft(RectTransform rt)
        {
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        private static void AddPicture(RectTransform rt, LevelData level)
        {
            var raw = rt.gameObject.AddComponent<RawImage>();
            raw.texture = MiniPicture.TextureOf(level, faded: false);
            raw.raycastTarget = false;
        }
    }
}
