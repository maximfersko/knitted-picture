#nullable enable

using UnityEngine;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Заставка при запуске: нитка по спирали сматывается в клубок, клубок с поворотом вырастает и пружинит,
    /// под ним проявляется название; затем заставка растворяется, открывая главную. Около 1,8 с —
    /// за это время в фоне дорисовываются клубки. Держится поверх окон (подарок дня откроется под ней).
    /// «Меньше движения» — просто клубок и название с мягким исчезновением.
    /// </summary>
    public static class Splash
    {
        public static void Show(RectTransform canvas, Color ground)
        {
            var root = Ui.Stretch(Ui.Node(canvas, "Splash"));
            var group = root.gameObject.AddComponent<CanvasGroup>();
            Ui.Stretch(Ui.Image(root, "Back", null, ground, true).rectTransform);
            var center = Ui.Node(root, "Center");
            center.anchorMin = center.anchorMax = new Vector2(0.5f, 0.55f);
            center.pivot = new Vector2(0.5f, 0.5f);
            center.sizeDelta = Vector2.zero;

            var yarn = Palette.Hex("#3C78D8");
            var ball = Ui.Image(center, "Ball", Art2.YarnBall(yarn), Color.white).rectTransform;
            ball.anchorMin = ball.anchorMax = new Vector2(0.5f, 0.5f);
            ball.sizeDelta = new Vector2(112f, 112f);
            ball.anchoredPosition = Vector2.zero;

            var title = Ui.Label(center, "Вязаная картинка", 30, Palette.Ink);
            var titleRt = title.rectTransform;
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 0.5f);
            titleRt.sizeDelta = new Vector2(340f, 44f);
            titleRt.anchoredPosition = new Vector2(0f, -96f);
            var titleGroup = title.gameObject.AddComponent<CanvasGroup>();

            const int dots = 22;
            var thread = new RectTransform[dots];
            for (var i = 0; i < dots; i++)
            {
                var dot = Ui.Image(center, "Thread", Art.Circle, Color.Lerp(yarn, Color.white, 0.15f)).rectTransform;
                dot.anchorMin = dot.anchorMax = new Vector2(0.5f, 0.5f);
                dot.sizeDelta = Vector2.one * 9f;
                thread[i] = dot;
            }

            var calm = Tweens.Calm;
            const float wind = 0.9f;
            const float total = 1.75f;
            Tweens.Run(root, total, t =>
            {
                root.SetAsLastSibling();
                var time = t * total;

                // Нитка: точки по спирали, каждая со своим отставанием, тянутся к центру и исчезают в клубке.
                for (var i = 0; i < dots; i++)
                {
                    var k = Mathf.Clamp01(time / wind - i * 0.025f);
                    var radius = Mathf.Lerp(150f, 0f, Ease.InOutCubic(k));
                    var angle = (i * 0.42f + k * 4.2f) * Mathf.PI;
                    thread[i].anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    thread[i].gameObject.SetActive(!calm && k < 0.97f);
                }

                // Клубок растёт по мере намотки, с поворотом, в конце пружинит.
                var grow = calm ? 1f : Mathf.Clamp01(time / wind);
                var spring = calm || time < wind ? 0f : Ease.Jolt(Mathf.Clamp01((time - wind) / 0.4f));
                ball.localScale = Vector3.one * (Mathf.Lerp(0.15f, 1f, Ease.OutCubic(grow)) * (1f + 0.12f * spring));
                ball.localRotation = Quaternion.Euler(0f, 0f, calm ? 0f : -200f * (1f - Ease.OutCubic(grow)));

                // Название проявляется и чуть поднимается после намотки.
                var show = Mathf.Clamp01((time - (calm ? 0.1f : 0.75f)) / 0.4f);
                titleGroup.alpha = show;
                titleRt.anchoredPosition = new Vector2(0f, -96f + 10f * (1f - Ease.OutCubic(show)) * (calm ? 0f : 1f));
            }, null, () =>
            {
                group.blocksRaycasts = false;
                Tweens.Run(group, 0.35f, t =>
                {
                    root.SetAsLastSibling();
                    group.alpha = 1f - t;
                    center.localScale = Vector3.one * (1f + (calm ? 0f : 0.06f * t));
                }, Ease.InOutCubic, () => Object.Destroy(root.gameObject));
            });
        }
    }
}
