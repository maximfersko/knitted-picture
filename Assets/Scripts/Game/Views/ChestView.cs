#nullable enable

using System;
using UnityEngine;
using UnityEngine.UI;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Вязаная шкатулка: корпус, крышка на «петлях» сзади, золотая застёжка и мягкая тень.
    /// Позади — тихое свечение и лучи, они появляются, только когда сундук готов или открыт.
    /// Все движения медленные и мягкие: сундук дышит, покачивается и плавно приоткрывается.
    /// </summary>
    public sealed class ChestView
    {
        private readonly RectTransform _pose;
        private readonly RectTransform _lid;
        private readonly RectTransform _clasp;
        private readonly Image _glow;
        private readonly Image _rays;
        private readonly float _scale;
        private Vector2 _lidRest;
        private bool _breathing;
        private bool _shining;

        private ChestView(RectTransform root, RectTransform pose, RectTransform lid, RectTransform clasp, Image glow, Image rays, float scale)
        {
            Root = root;
            _pose = pose;
            _lid = lid;
            _clasp = clasp;
            _glow = glow;
            _rays = rays;
            _scale = scale;
            _lidRest = lid.anchoredPosition;
        }

        public RectTransform Root { get; }

        /// <summary>Мировая точка «изнутри сундука»: отсюда вылетают награды.</summary>
        public Vector3 Mouth => _pose.TransformPoint(new Vector3(0, Art.ChestBodySize.y * _scale * 0.9f, 0));

        /// <summary>Шкатулка шириной width; cx, cy — центр корпуса вместе с крышкой.</summary>
        public static ChestView Create(RectTransform parent, float cx, float cy, float width, bool big)
        {
            var scale = width / Art.ChestBodySize.x;
            var bodySize = Art.ChestBodySize * scale;
            var lidSize = Art.ChestLidSize * scale;
            var overlap = 6f * scale;
            var height = bodySize.y + lidSize.y - overlap;
            var root = Ui.PlaceCenter(Ui.Node(parent, "Chest"), cx, cy, width * 1.1f, height);

            // Свечение и лучи — позади, по центру крышки.
            var rays = Ui.Image(root, "Rays", Art.Rays, Palette.WithAlpha(Palette.CoinSoft, 0f));
            Ui.PlaceCenter(rays.rectTransform, width * 0.55f, height * 0.42f, width * 2.4f, width * 2.4f);
            var glow = Ui.Image(root, "Glow", Art.Glow, Palette.WithAlpha(Palette.Coin, 0f));
            Ui.PlaceCenter(glow.rectTransform, width * 0.55f, height * 0.42f, width * 1.9f, width * 1.9f);

            var shadow = Ui.Image(root, "Shadow", Art.Circle, Palette.WithAlpha(Palette.Ink, 0.16f));
            Ui.PlaceCenter(shadow.rectTransform, width * 0.55f, height - 2f * scale, width * 0.92f, 16f * scale);

            // Поза: всё, что качается и дышит. Опорная точка — середина дна, как у настоящей коробки на столе.
            var pose = Ui.Node(root, "Pose");
            pose.anchorMin = pose.anchorMax = new Vector2(0.5f, 0f);
            pose.pivot = new Vector2(0.5f, 0f);
            pose.anchoredPosition = Vector2.zero;
            pose.sizeDelta = new Vector2(width, height);

            var body = Ui.Image(pose, "Body", Art.ChestBody(big), Color.white);
            var brt = body.rectTransform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.anchoredPosition = Vector2.zero;
            brt.sizeDelta = bodySize;

            // Крышка вращается вокруг заднего края: опорная точка — середина её низа.
            var lid = Ui.Image(pose, "Lid", Art.ChestLid(big), Color.white).rectTransform;
            lid.anchorMin = lid.anchorMax = new Vector2(0.5f, 0f);
            lid.pivot = new Vector2(0.5f, 0f);
            lid.anchoredPosition = new Vector2(0, bodySize.y - overlap);
            lid.sizeDelta = lidSize;

            // Застёжка: золотая плашка с замочной скважиной на стыке.
            var clasp = Ui.Node(pose, "Clasp");
            clasp.anchorMin = clasp.anchorMax = new Vector2(0.5f, 0f);
            clasp.pivot = new Vector2(0.5f, 0.5f);
            clasp.anchoredPosition = new Vector2(0, bodySize.y - overlap / 2f);
            clasp.sizeDelta = new Vector2(30f * scale, 36f * scale);
            var claspShadow = Ui.Box(clasp, "Shadow", 8f * scale, Palette.CoinShadow);
            Ui.Place(claspShadow.rectTransform, 0, 2f * scale, 30f * scale, 36f * scale);
            var claspFace = Ui.Box(clasp, "Face", 8f * scale, Palette.Coin);
            Ui.Place(claspFace.rectTransform, 0, 0, 30f * scale, 34f * scale);
            var hole = Ui.Image(clasp, "Hole", Art.Circle, Palette.Hex("#9A6544"));
            Ui.PlaceCenter(hole.rectTransform, 15f * scale, 14f * scale, 9f * scale, 9f * scale);
            var slit = Ui.Box(clasp, "Slit", 2f * scale, Palette.Hex("#9A6544"));
            Ui.PlaceCenter(slit.rectTransform, 15f * scale, 21f * scale, 4f * scale, 10f * scale);

            if (big)
            {
                // Большой сундук: три звёздочки на крышке.
                for (var i = -1; i <= 1; i++)
                {
                    var star = Ui.Image(lid, "Star", Art.Icon(Icon.Star), Palette.Hex("#FFF4D6"));
                    var srt = star.rectTransform;
                    srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0f);
                    srt.pivot = new Vector2(0.5f, 0.5f);
                    srt.anchoredPosition = new Vector2(i * 46f * scale, lidSize.y * 0.56f + (i == 0 ? 6f : 0f) * scale);
                    srt.sizeDelta = Vector2.one * (i == 0 ? 20f : 14f) * scale;
                }
            }

            return new ChestView(root, pose, lid, clasp, glow, rays, scale);
        }

        /// <summary>Тихое «дыхание» и лёгкое покачивание: сундук ждёт.</summary>
        public void Breathe()
        {
            _breathing = true;
            var pose = _pose;
            Tweens.Run(pose, 2.6f, t =>
            {
                if (!_breathing)
                {
                    return;
                }

                var wave = Mathf.Sin(t * Mathf.PI * 2f);
                pose.localScale = new Vector3(1f - 0.012f * wave, 1f + 0.02f * wave, 1f);
            }, null, null, 0f, true);
        }

        /// <summary>Сундук готов: под ним мягко светится, лучи медленно вращаются.</summary>
        public void Shine(float glow = 0.35f, float rays = 0.0f)
        {
            var glowImage = _glow;
            var raysImage = _rays;
            var startGlow = glowImage.color.a;
            var startRays = raysImage.color.a;
            Tweens.Run(glowImage, 0.8f, t =>
            {
                glowImage.color = Palette.WithAlpha(glowImage.color, Mathf.Lerp(startGlow, glow, t));
                raysImage.color = Palette.WithAlpha(raysImage.color, Mathf.Lerp(startRays, rays, t));
            }, Ease.OutCubic);
            if (_shining)
            {
                return;
            }

            _shining = true;
            var rt = raysImage.rectTransform;
            Tweens.Run(rt, 60f, t => rt.localRotation = Quaternion.Euler(0, 0, -360f * t), null, null, 0f, true);
            var grt = glowImage.rectTransform;
            Tweens.Run(grt, 3.2f, t => grt.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(t * Mathf.PI * 2f)), null, null, 0f, true);
        }

        /// <summary>
        /// Открытие: сундук приседает и дважды тихо покачивается, застёжка отщёлкивается,
        /// крышка плавно откидывается назад, изнутри поднимается тёплый свет.
        /// </summary>
        public void Open(Action opened)
        {
            _breathing = false;
            var pose = _pose;
            var lid = _lid;
            var clasp = _clasp;
            var scale = _scale;

            // 1. Замах: сундук приседает и дважды коротко дёргается — внутри что-то есть.
            Tweens.Run(pose, 0.42f, t =>
            {
                var squash = 0.1f * Ease.Bump(Mathf.Clamp01(t * 2.4f));
                var shake = t > 0.35f ? Mathf.Sin((t - 0.35f) / 0.65f * Mathf.PI * 4f) * 6f * (1f - t) : 0f;
                pose.localRotation = Quaternion.Euler(0, 0, shake);
                pose.localScale = new Vector3(1f + squash * 0.7f, 1f - squash, 1f);
            }, null, () =>
            {
                pose.localRotation = Quaternion.identity;
                Sfx.Play(Sound.Unlock, 0.8f);

                // 2. Хлопок: сундук подпрыгивает и вытягивается, застёжка отлетает, крышка откидывается с перелётом.
                Tweens.Run(pose, 0.5f, t =>
                {
                    var jump = t < 0.4f ? Ease.OutCubic(t / 0.4f) : 1f - Ease.InOutCubic((t - 0.4f) / 0.6f);
                    pose.anchoredPosition = new Vector2(0, 22f * scale * jump);
                    var stretch = t < 0.4f ? 0.08f * (1f - t / 0.4f) : -0.06f * Ease.Bump((t - 0.4f) / 0.6f);
                    pose.localScale = new Vector3(1f - stretch * 0.6f, 1f + stretch, 1f);
                }, null, () =>
                {
                    pose.anchoredPosition = Vector2.zero;
                    pose.localScale = Vector3.one;
                });

                var claspRest = clasp.anchoredPosition;
                Tweens.Run(clasp, 0.3f, t =>
                {
                    clasp.anchoredPosition = claspRest + new Vector2(4f * scale * t, -10f * scale * t);
                    clasp.localRotation = Quaternion.Euler(0, 0, 32f * t);
                }, Ease.OutBack);

                var rest = _lidRest;
                Tweens.Run(lid, 0.34f, t =>
                {
                    lid.anchoredPosition = rest + new Vector2(0, 26f * scale * t);
                    lid.localScale = new Vector3(1f - 0.05f * t, 1f - 0.55f * t, 1f);
                    lid.localRotation = Quaternion.Euler(0, 0, -10f * t);
                }, Ease.OutBack);

                // Вспышка света изнутри и короткий всплеск лучей, потом ровное свечение.
                // Shine плавно ведёт яркость от текущей: вспышка = начать с почти полной.
                _glow.color = Palette.WithAlpha(_glow.color, 0.95f);
                _rays.color = Palette.WithAlpha(_rays.color, 0.45f);
                Shine(0.6f, 0.22f);
                Tweens.Delay(Root, 0.22f, opened);
            });
        }

        /// <summary>Пыль и пушинки пряжи медленно поднимаются из сундука и тают.</summary>
        public void Fluff(RectTransform layer, Color[] colors, int count)
        {
            var from = Mouth;
            var unit = layer.lossyScale.x;
            for (var i = 0; i < count; i++)
            {
                var dot = Ui.Image(layer, "Fluff", Art.Circle, colors[i % colors.Length]);
                var rt = dot.rectTransform;
                var size = UnityEngine.Random.Range(5f, 11f);
                rt.sizeDelta = new Vector2(size, size);
                var start = from + new Vector3(UnityEngine.Random.Range(-60f, 60f) * unit * _scale, 0, 0);
                var drift = UnityEngine.Random.Range(-40f, 40f) * unit;
                var rise = UnityEngine.Random.Range(90f, 170f) * unit;
                var delay = i * 0.06f;
                rt.position = start;
                dot.color = Palette.WithAlpha(dot.color, 0f);
                var color = colors[i % colors.Length];
                Tweens.Run(rt, UnityEngine.Random.Range(1.4f, 2.2f), t =>
                {
                    rt.position = start + new Vector3(drift * t + Mathf.Sin(t * 6f + i) * 6f * unit, rise * t, 0);
                    dot.color = Palette.WithAlpha(color, 0.85f * Ease.Bump(t));
                }, Ease.OutCubic, () => UnityEngine.Object.Destroy(dot.gameObject), delay);
            }
        }
    }
}
