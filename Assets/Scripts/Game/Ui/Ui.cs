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
        private static Font? _regular;
        private static bool _custom;

        /// <summary>
        /// Округлый Nunito (OFL, лицензия рядом в Resources/Fonts): Main.ttf — ExtraBold для заголовков и
        /// кнопок, Regular.ttf — SemiBold для подписей. Начертания настоящие, поэтому FontStyle.Bold
        /// поверх не включаем: синтетический жирный у Unity расплывается. Нет файлов — встроенный шрифт.
        /// </summary>
        public static Font Font
        {
            get
            {
                if (_font == null)
                {
                    _font = Resources.Load<Font>("Fonts/Main");
                    _custom = _font != null;
                    if (_font == null)
                    {
                        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    }
                }

                return _font;
            }
        }

        private static Font RegularFont
        {
            get
            {
                if (_regular == null)
                {
                    _regular = Resources.Load<Font>("Fonts/Regular");
                    if (_regular == null)
                    {
                        _regular = Font;
                    }
                }

                return _regular;
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

        /// <summary>
        /// Свой вложенный Canvas у слоя: uGUI пересобирает геометрию целиком того Canvas, где что-то сдвинулось
        /// или перекрасилось. Подвижное (картинка, поле, станок, эффекты, окна) — в своих слоях, и летящая
        /// нитка пересобирает только слой эффектов, а не весь экран. Порядок отрисовки прежний (без overrideSorting).
        /// input — слою нужны нажатия: у вложенного Canvas свой GraphicRaycaster.
        /// </summary>
        public static RectTransform Layer(RectTransform rt, bool input)
        {
            if (rt.GetComponent<Canvas>() == null)
            {
                rt.gameObject.AddComponent<Canvas>();
                if (input)
                {
                    rt.gameObject.AddComponent<GraphicRaycaster>();
                }
            }

            return rt;
        }

        /// <summary>
        /// Слой для того, что анимируется без конца (пульс кнопки, кольца, лучи): иначе его Canvas пересобирался бы
        /// каждый кадр, пока экран открыт. Внутри области с обрезкой (RectMask2D) не трогаем — обрезка
        /// считается в пространстве её Canvas. Нажатия сохраняются, если внутри есть кликабельное.
        /// </summary>
        public static void LoopLayer(Object owner)
        {
            var rt = owner switch
            {
                RectTransform r => r,
                Component c => c.transform as RectTransform,
                GameObject go => go.transform as RectTransform,
                _ => null,
            };
            if (rt == null || rt.GetComponent<Canvas>() != null || rt.GetComponentInParent<RectMask2D>() != null)
            {
                return;
            }

            var input = rt.GetComponentInChildren<Tap>(true) != null;
            if (!input)
            {
                foreach (var graphic in rt.GetComponentsInChildren<Graphic>(true))
                {
                    if (graphic.raycastTarget)
                    {
                        input = true;
                        break;
                    }
                }
            }

            Layer(rt, input);
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

        /// <summary>Объём плашки.</summary>
        public enum Relief
        {
            /// <summary>Плоская: тени, подложки, нитки, стрелки, вуали.</summary>
            Flat,

            /// <summary>Выпуклая «подушка»: кнопки, плашки, бирки, строки списков.</summary>
            Raised,

            /// <summary>Глянцевый «леденец»: заполнения полосок.</summary>
            Glossy,

            /// <summary>Вдавленная: дорожки полосок, слоты, лоток станка, переключатели, ячейки.</summary>
            Inset,
        }

        /// <summary>
        /// Объём по роли плашки — в одном месте на всю игру, а не в каждом экране: имя говорит, что это.
        /// Неизвестное имя — плоская (так новые подложки и тени не станут случайно выпуклыми).
        /// </summary>
        public static Relief ReliefOf(string name)
        {
            if (name.EndsWith("Track", System.StringComparison.Ordinal))
            {
                return Relief.Inset;
            }

            switch (name)
            {
                case "Slot": case "Loom": case "Tabs": case "Switch": case "Preview": case "Mat": case "Inside": case "Tile":
                case "Yarn": case "Segment":
                    return Relief.Inset;
                case "Fill": case "Heat":
                    return Relief.Glossy;
                case "Face": case "Back": case "Chip": case "Count": case "Claim": case "Reward": case "Rarity": case "Status":
                case "State": case "Mission": case "StarRow": case "Row": case "Section": case "Group": case "Shelf": case "ShelfLip":
                case "More": case "Active": case "Indicator": case "Part": case "Level": case "Dot": case "Visible": case "Glow":
                    return Relief.Raised;
                default:
                    return Relief.Flat;
            }
        }

        /// <summary>Скруглённая плашка; объём — по роли (<see cref="ReliefOf"/>).</summary>
        public static Image Box(RectTransform parent, string name, float radius, Color color, bool raycast = false)
        {
            var relief = ReliefOf(name);
            var sprite = relief switch
            {
                Relief.Raised => Art.SoftCard(radius),
                Relief.Inset => Art.InsetBox(radius),
                Relief.Glossy => Art.GlossBox(radius),
                _ => Art.RoundRect(radius),
            };
            var image = Image(parent, name, sprite, color, raycast);
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = Art.Density;
            if (relief == Relief.Glossy)
            {
                // Блик — дочерний слой: тянется вместе с заполнением, не перекрашивается его цветом.
                var gloss = Image(image.rectTransform, "Gloss", Art.GlossOverlay(radius), Color.white);
                gloss.type = UnityEngine.UI.Image.Type.Sliced;
                gloss.pixelsPerUnitMultiplier = Art.Density;
                Stretch(gloss.rectTransform);
            }

            return image;
        }

        /// <summary>Плашка с плотной тенью снизу, как у карточек в макетах.</summary>
        public static RectTransform Card(RectTransform parent, string name, float x, float y, float w, float h,
            float radius, Color color, Color shadow, float shadowOffset = 3f, bool raycast = false)
        {
            var root = Place(Node(parent, name), x, y, w, h);
            if (shadowOffset > 0 && shadow == Palette.CardShadow)
            {
                // Белые карточки — на мягкой размытой тени цвета фона, а не на плотной «подошве»:
                // легче и современнее. У цветных кнопок «подошва» остаётся: она показывает, что их нажимают.
                var blur = Mathf.Clamp(Mathf.Min(w, h) * 0.2f, 4f, 12f);
                var soft = Image(root, "Shadow", Art.SoftShadow(radius, blur), Palette.SoftShadow);
                soft.type = UnityEngine.UI.Image.Type.Sliced;
                soft.pixelsPerUnitMultiplier = Art.Density;
                Place(soft.rectTransform, -blur, shadowOffset + 1f - blur * 0.6f, w + 2f * blur, h + 2f * blur);
            }
            else if (shadowOffset > 0)
            {
                var back = Box(root, "Shadow", radius, shadow);
                Place(back.rectTransform, 0, shadowOffset, w, h);
            }

            var face = Image(root, "Face", Art.SoftCard(radius), color, raycast);
            face.type = UnityEngine.UI.Image.Type.Sliced;
            face.pixelsPerUnitMultiplier = Art.Density;
            Place(face.rectTransform, 0, 0, w, h);

            // Белым карточкам — тонкий контур: на белом окне и на светлом фоне они не теряются.
            if (color == Palette.Card || color == Palette.Surface)
            {
                Outline(root, radius, Palette.Line, 1.5f);
            }

            return root;
        }

        /// <summary>Блик сверху: свет падает сверху, и плоская кнопка становится выпуклой.</summary>
        public static Image Shine(RectTransform parent, float w, float h, float radius)
        {
            var shineH = Mathf.Round(h * 0.44f);
            var shine = Box(parent, "Shine", Mathf.Min(Mathf.Max(2f, radius - 3f), shineH / 2f), new Color(1f, 1f, 1f, 0.16f));
            Place(shine.rectTransform, 4f, 3f, w - 8f, shineH);
            return shine;
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

        /// <summary>
        /// Единая шкала текста: мелкие цифры 11, подписи 13, основной текст 16, заголовки 20.
        /// Крупные заголовки (от 23) остаются как есть. Раньше на экранах было больше десятка
        /// разных размеров — интерфейс выглядел неровным. Ui.Fit дальше может уменьшить строку, если не влезает.
        /// </summary>
        public static int TypeSize(float size) =>
            size < 12f ? 11 : size < 14.5f ? 13 : size < 17.5f ? 16 : size < 23f ? 20 : Mathf.RoundToInt(size);

        /// <summary>
        /// «Крупный текст» (настройки удобства): мелкий текст (меньше 20) на ~20% крупнее там, где помещается.
        /// Через автоподбор размера: не влезает — ужимается обратно до обычного, раскладка не ломается.
        /// </summary>
        public static bool LargeText { get; set; }

        public static Text Label(RectTransform parent, string text, float size, Color color, bool bold = true,
            TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var rt = Node(parent, "Text");
            var label = rt.gameObject.AddComponent<Text>();
            label.font = Font;
            if (!bold && _custom)
            {
                label.font = RegularFont;
            }

            label.text = text;
            label.fontSize = TypeSize(size);
            label.fontStyle = bold && !_custom ? FontStyle.Bold : FontStyle.Normal;
            label.color = color;
            label.alignment = anchor;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.supportRichText = false;
            if (LargeText && label.fontSize < 20)
            {
                var normal = label.fontSize;
                label.fontSize = Mathf.RoundToInt(normal * 1.2f);
                label.resizeTextForBestFit = true;
                label.resizeTextMaxSize = label.fontSize;
                label.resizeTextMinSize = normal;
            }

            Emboss(label);
            return label;
        }

        /// <summary>
        /// Тиснение крупного текста (заголовки, кнопки): тёмный — со светлой кромкой снизу, будто вдавлен;
        /// светлый — с мягкой тенью, будто выпуклый. Мелкий текст не трогаем: тень удваивает вершины.
        /// </summary>
        private static void Emboss(Text label)
        {
            if (label.fontSize < 20)
            {
                return;
            }

            var c = label.color;
            var light = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f > 0.6f;
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = light ? new Color(0.12f, 0.08f, 0.25f, 0.28f) : new Color(1f, 1f, 1f, 0.7f);
            shadow.effectDistance = new Vector2(0f, light ? -1.5f : -1f);
        }

        /// <summary>
        /// Не дать тексту вылезти за свою рамку: шрифт уменьшается (до minSize), пока строка не влезет.
        /// Для названий и подписей, длина которых заранее неизвестна.
        /// </summary>
        public static Text Fit(Text label, int minSize)
        {
            label.resizeTextForBestFit = true;
            label.resizeTextMaxSize = label.fontSize;
            label.resizeTextMinSize = Mathf.Min(minSize, label.fontSize);
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        /// <summary>Кнопка «i» в кружке: по тапу — короткая справка. Подробности не загромождают экран.</summary>
        public static Tap InfoButton(RectTransform parent, float cx, float cy, string title, string text, float size = 26f)
        {
            var root = PlaceCenter(Node(parent, "Info"), cx, cy, size + 14f, size + 14f);
            var hit = Image(root, "Hit", null, Color.clear, true);
            Stretch(hit.rectTransform);
            var circle = Image(root, "Back", Art.Circle, Palette.Surface);
            PlaceCenter(circle.rectTransform, (size + 14f) / 2f, (size + 14f) / 2f, size, size);
            Outline(circle.rectTransform, size / 2f, Palette.Line, 1.5f);
            IconImage(root, Icon.Info, Palette.Ink2, (size + 14f) / 2f, (size + 14f) / 2f, size * 0.7f);
            var tap = root.gameObject.AddComponent<Tap>();
            tap.OnClick(() =>
            {
                var app = App.Instance;
                if (app != null)
                {
                    app.ShowPopup(new InfoPopup(app, title, text));
                }
            });
            return tap;
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
            // Детальная монета: золото с ободом и выпуклым клубком.
            var root = PlaceCenter(Node(parent, "Coin"), cx, cy, size, size);
            Stretch(Image(root, "Face", Art2.Coin, Color.white).rectTransform);
            return root;
        }

        /// <summary>Большая кнопка с «клавишной» тенью.</summary>
        public static Tap Button(RectTransform parent, string text, float x, float y, float w, float h,
            Color color, Color shadow, Color textColor, float fontSize = 20f)
        {
            var radius = Mathf.Min(22f, h / 2f);
            var root = Card(parent, "Button", x, y, w, h, radius, color, shadow, 5f, true);
            // Блик сверху: кнопка выпуклая, как будто свет падает сверху.
            Shine(root, w, h, radius);
            var label = Label(root, text, fontSize, textColor);
            Place(label.rectTransform, 0, 0, w, h);
            var tap = root.gameObject.AddComponent<Tap>();
            tap.Sound = Sound.Needles;
            return tap;
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
        /// <param name="onCard">Кнопка лежит на белой карточке или окне: тогда она залита светло-сиреневым
        /// и без тени, иначе белый круг сливается с белым фоном.</param>
        public static Tap RoundButton(RectTransform parent, Icon icon, float x, float y, float size = 44f, bool onCard = false)
        {
            var root = Place(Node(parent, "Round" + icon), x, y, size, size);
            if (!onCard)
            {
                var shadow = Image(root, "Shadow", Art.Circle, Palette.CardShadow);
                Place(shadow.rectTransform, 0, 2, size, size);
            }

            var face = Image(root, "Face", Art.SoftDisc, onCard ? Palette.ButtonFill : Palette.Card, true);
            Place(face.rectTransform, 0, 0, size, size);
            IconImage(root, icon, Palette.Ink, size / 2f, size / 2f, size * 0.45f);
            return root.gameObject.AddComponent<Tap>();
        }

        /// <summary>Пилюля с монетами.</summary>
        public static Text CoinPill(RectTransform parent, int coins, float rightX, float y)
        {
            // Ширина — по числу цифр: монета и число стоят рядом, без пустоты справа.
            var digits = Mathf.Max(3, coins.ToString().Length);
            var w = 12f + 26f + 6f + digits * 10f + 12f;
            var root = Card(parent, "Coins", rightX - w, y, w, 44, 22, Palette.Card, Palette.CardShadow, 2f);
            Coin(root, 25, 22, 26);
            // Монеты, которые ещё летят в счётчик, прибавятся по прилёту (см. App.FlyCoins).
            var app = App.Instance;
            var shown = app != null ? coins - app.CoinsInFlight : coins;
            var label = Label(root, shown.ToString(), 17, Palette.Ink, 44, 0, w - 50, 44, true, TextAnchor.MiddleLeft);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (app != null)
            {
                app.CoinCounter = label;
            }

            return label;
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
            // Выпрыгивает, а потом «бьётся» двумя ударами с паузой: заметно, но не мельтешит, как ровный пульс.
            Tweens.Scale(root, 0f, 1f, 0.4f, Ease.OutBack, () =>
                Tweens.Run(root, 2.2f, t => root.localScale = Vector3.one * (1f + 0.16f * Ease.Heartbeat(t)), null, null, 0f, true), 0.25f);
            return root;
        }

        /// <summary>
        /// CanvasGroup элемента, а нет — новая. Не через «??»: в редакторе отсутствующий компонент приходит
        /// «поддельным null», и ?? вернул бы его вместо новой группы.
        /// </summary>
        public static CanvasGroup Group(Component target)
        {
            var group = target.GetComponent<CanvasGroup>();
            return group != null ? group : target.gameObject.AddComponent<CanvasGroup>();
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
