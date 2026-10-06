#nullable enable

using UnityEngine;
using UnityEngine.UI;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Строит интерфейс кодом. Координаты — от левого верхнего угла родителя, в точках макета (ширина экрана 390).
    /// </summary>
    public static class Ui
    {
        private static Font? _font;

        /// <summary>Свой шрифт кладётся в Assets/Resources/Fonts/Main.ttf (например, Nunito Black), иначе встроенный.</summary>
        public static Font Font
        {
            get
            {
                if (_font == null)
                {
                    _font = Resources.Load<Font>("Fonts/Main");
                    if (_font == null)
                    {
                        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    }
                }

                return _font;
            }
        }

        public static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Поставить элемент: x, y — левый верхний угол в родителе.</summary>
        public static RectTransform Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>Поставить элемент по центру: cx, cy — центр в координатах родителя.</summary>
        public static RectTransform PlaceCenter(RectTransform rt, float cx, float cy, float w, float h)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(cx, -cy);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>Растянуть на весь родитель.</summary>
        public static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Image(RectTransform parent, string name, Sprite? sprite, Color color, bool raycast = false)
        {
            var rt = Node(parent, name);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        /// <summary>Скруглённая плашка.</summary>
        public static Image Box(RectTransform parent, string name, float radius, Color color, bool raycast = false)
        {
            var image = Image(parent, name, Art.RoundRect(radius), color, raycast);
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = Art.Density;
            return image;
        }

        /// <summary>Плашка с плотной тенью снизу, как у карточек в макетах.</summary>
        public static RectTransform Card(RectTransform parent, string name, float x, float y, float w, float h,
            float radius, Color color, Color shadow, float shadowOffset = 3f, bool raycast = false)
        {
            var root = Place(Node(parent, name), x, y, w, h);
            if (shadowOffset > 0)
            {
                var back = Box(root, "Shadow", radius, shadow);
                Place(back.rectTransform, 0, shadowOffset, w, h);
            }

            var face = Box(root, "Face", radius, color, raycast);
            Place(face.rectTransform, 0, 0, w, h);

            // Белым карточкам — тонкий контур: на белом окне и на светлом фоне они не теряются.
            if (color == Palette.Card || color == Palette.Surface)
            {
                Outline(root, radius, Palette.Line, 1.5f);
            }

            return root;
        }

        /// <summary>Контур поверх элемента (растянут на весь родитель).</summary>
        public static Image Outline(RectTransform parent, float radius, Color color, float stroke)
        {
            var outline = Image(parent, "Outline", Art.RoundRectOutline(radius, stroke), color);
            outline.type = UnityEngine.UI.Image.Type.Sliced;
            outline.pixelsPerUnitMultiplier = Art.Density;
            Stretch(outline.rectTransform);
            return outline;
        }

        public static Text Label(RectTransform parent, string text, float size, Color color, bool bold = true,
            TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var rt = Node(parent, "Text");
            var label = rt.gameObject.AddComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = Mathf.RoundToInt(size);
            label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            label.color = color;
            label.alignment = anchor;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.supportRichText = false;
            return label;
        }

        public static Text Label(RectTransform parent, string text, float size, Color color, float x, float y, float w, float h,
            bool bold = true, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var label = Label(parent, text, size, color, bold, anchor);
            Place(label.rectTransform, x, y, w, h);
            return label;
        }

        public static Image IconImage(RectTransform parent, Icon icon, Color color, float cx, float cy, float size)
        {
            var image = Image(parent, "Icon" + icon, Art.Icon(icon), color);
            PlaceCenter(image.rectTransform, cx, cy, size, size);
            return image;
        }

        /// <summary>Монетка: жёлтый круг с ободком.</summary>
        public static RectTransform Coin(RectTransform parent, float cx, float cy, float size)
        {
            var root = PlaceCenter(Node(parent, "Coin"), cx, cy, size, size);
            Stretch(Image(root, "Edge", Art.Circle, Palette.CoinShadow).rectTransform);
            var face = Image(root, "Face", Art.Circle, Palette.Coin);
            PlaceCenter(face.rectTransform, size / 2f, size / 2f - size * 0.06f, size * 0.84f, size * 0.84f);
            return root;
        }

        /// <summary>Большая кнопка с «клавишной» тенью.</summary>
        public static Tap Button(RectTransform parent, string text, float x, float y, float w, float h,
            Color color, Color shadow, Color textColor, float fontSize = 20f)
        {
            var root = Card(parent, "Button", x, y, w, h, Mathf.Min(22f, h / 2f), color, shadow, 5f, true);
            var label = Label(root, text, fontSize, textColor);
            Place(label.rectTransform, 0, 0, w, h);
            return root.gameObject.AddComponent<Tap>();
        }

        /// <summary>Кнопка без фона: вторичное действие, не навязывается.</summary>
        public static Tap TextButton(RectTransform parent, string text, float x, float y, float w, float h,
            Color textColor, float fontSize = 15f, Icon? icon = null)
        {
            var root = Place(Node(parent, "TextButton"), x, y, w, h);
            var hit = Image(root, "Hit", null, Color.clear, true);
            Stretch(hit.rectTransform);
            var label = Label(root, text, fontSize, textColor);
            Place(label.rectTransform, icon != null ? 14 : 0, 0, w, h);
            if (icon != null)
            {
                var width = label.preferredWidth;
                IconImage(root, icon.Value, textColor, w / 2f - width / 2f - 4f, h / 2f, 18);
            }

            return root.gameObject.AddComponent<Tap>();
        }

        /// <summary>Круглая белая кнопка с иконкой (пауза, настройки, назад).</summary>
        public static Tap RoundButton(RectTransform parent, Icon icon, float x, float y, float size = 44f)
        {
            var root = Place(Node(parent, "Round" + icon), x, y, size, size);
            var shadow = Image(root, "Shadow", Art.Circle, Palette.CardShadow);
            Place(shadow.rectTransform, 0, 2, size, size);
            var face = Image(root, "Face", Art.Circle, Palette.Card, true);
            Place(face.rectTransform, 0, 0, size, size);
            IconImage(root, icon, Palette.Ink, size / 2f, size / 2f, size * 0.45f);
            return root.gameObject.AddComponent<Tap>();
        }

        /// <summary>Пилюля с монетами.</summary>
        public static Text CoinPill(RectTransform parent, int coins, float rightX, float y)
        {
            const float w = 104f;
            var root = Card(parent, "Coins", rightX - w, y, w, 44, 22, Palette.Card, Palette.CardShadow, 2f);
            Coin(root, 22, 22, 26);
            return Label(root, coins.ToString(), 17, Palette.Ink, 40, 0, w - 50, 44, true, TextAnchor.MiddleLeft);
        }

        /// <summary>Полоса прогресса: фон и заливка, возвращает заливку.</summary>
        public static RectTransform ProgressBar(RectTransform parent, float x, float y, float w, float h, float value, Color fill)
        {
            var track = Box(parent, "Track", h / 2f, Palette.Track);
            Place(track.rectTransform, x, y, w, h);
            var bar = Box(track.rectTransform, "Fill", h / 2f, fill);
            Place(bar.rectTransform, 0, 0, Mathf.Max(h, w * Mathf.Clamp01(value)), h);
            bar.enabled = value > 0.001f;
            return bar.rectTransform;
        }

        /// <summary>
        /// Вертикальная прокрутка: окно x, y, w, h и содержимое внутри него.
        /// Высоту содержимого задаёт вызывающий: content.sizeDelta = (w, высота).
        /// </summary>
        public static RectTransform Scroll(RectTransform parent, float x, float y, float w, float h)
        {
            var viewport = Place(Node(parent, "Scroll"), x, y, w, h);
            var hit = Image(viewport, "Hit", null, Color.clear, true);
            Stretch(hit.rectTransform);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Place(Node(viewport, "Content"), 0, 0, w, h);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 24f;
            return content;
        }

        /// <summary>Красная точка-счётчик в углу кнопки: «здесь что-то ждёт».</summary>
        public static RectTransform Badge(RectTransform parent, float cx, float cy, int count)
        {
            var size = count > 9 ? 26f : 22f;
            var root = PlaceCenter(Node(parent, "Badge"), cx, cy, size, 22f);
            Stretch(Box(root, "Back", 11, Palette.Danger).rectTransform);
            var label = Label(root, count > 0 ? count.ToString() : "!", 13, Color.white);
            Stretch(label.rectTransform);
            Tweens.Run(root, 1.2f, t => root.localScale = Vector3.one * (1f + 0.12f * Ease.Bump(t)), null, null, 0f, true);
            return root;
        }

        /// <summary>Мировая позиция центра элемента — для анимаций перелёта между панелями.</summary>
        public static Vector3 WorldCenter(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            return (corners[0] + corners[2]) / 2f;
        }

        public static void Clear(Transform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(parent.GetChild(i).gameObject);
            }
        }
    }
}
