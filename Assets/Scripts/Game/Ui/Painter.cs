#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Рисует простые фигуры в текстуру через поле расстояний — со сглаживанием.
    /// Координаты фигур в сетке 24×24 (как у иконок Lucide), y — вниз.
    /// </summary>
    public sealed class Painter
    {
        private const float Grid = 24f;
        private readonly List<Func<float, float, float>> _shapes = new List<Func<float, float, float>>();
        // Линии потолще (было 2.2): значки читаются рядом с объёмными клубками и не выглядят «системными».
        private float _stroke = 2.6f;

        public Painter Stroke(float width)
        {
            _stroke = width;
            return this;
        }

        public Painter Line(float ax, float ay, float bx, float by)
        {
            var r = _stroke / 2f;
            _shapes.Add((x, y) => SegmentDistance(x, y, ax, ay, bx, by) - r);
            return this;
        }

        public Painter Circle(float cx, float cy, float radius)
        {
            var r = _stroke / 2f;
            _shapes.Add((x, y) => Mathf.Abs(Length(x - cx, y - cy) - radius) - r);
            return this;
        }

        public Painter Disc(float cx, float cy, float radius)
        {
            _shapes.Add((x, y) => Length(x - cx, y - cy) - radius);
            return this;
        }

        /// <summary>Дуга от угла from до угла to по часовой стрелке (0° — вправо, 90° — вниз).</summary>
        public Painter Arc(float cx, float cy, float radius, float from, float to)
        {
            var r = _stroke / 2f;
            var ax = cx + radius * Mathf.Cos(from * Mathf.Deg2Rad);
            var ay = cy + radius * Mathf.Sin(from * Mathf.Deg2Rad);
            var bx = cx + radius * Mathf.Cos(to * Mathf.Deg2Rad);
            var by = cy + radius * Mathf.Sin(to * Mathf.Deg2Rad);
            var span = Mathf.Repeat(to - from, 360f);
            _shapes.Add((x, y) =>
            {
                var angle = Mathf.Repeat(Mathf.Atan2(y - cy, x - cx) * Mathf.Rad2Deg - from, 360f);
                if (angle <= span)
                {
                    return Mathf.Abs(Length(x - cx, y - cy) - radius) - r;
                }

                return Mathf.Min(Length(x - ax, y - ay), Length(x - bx, y - by)) - r;
            });
            return this;
        }

        public Painter Box(float cx, float cy, float halfW, float halfH, float radius, bool filled)
        {
            var r = _stroke / 2f;
            _shapes.Add((x, y) =>
            {
                var qx = Mathf.Abs(x - cx) - halfW + radius;
                var qy = Mathf.Abs(y - cy) - halfH + radius;
                var outside = Length(Mathf.Max(qx, 0), Mathf.Max(qy, 0)) + Mathf.Min(Mathf.Max(qx, qy), 0) - radius;
                return filled ? outside : Mathf.Abs(outside) - r;
            });
            return this;
        }

        public Painter Triangle(float ax, float ay, float bx, float by, float cx, float cy)
        {
            _shapes.Add((x, y) =>
            {
                var d = Mathf.Min(
                    SegmentDistance(x, y, ax, ay, bx, by),
                    Mathf.Min(SegmentDistance(x, y, bx, by, cx, cy), SegmentDistance(x, y, cx, cy, ax, ay)));
                var s1 = Cross(bx - ax, by - ay, x - ax, y - ay);
                var s2 = Cross(cx - bx, cy - by, x - bx, y - by);
                var s3 = Cross(ax - cx, ay - cy, x - cx, y - cy);
                var inside = (s1 >= 0 && s2 >= 0 && s3 >= 0) || (s1 <= 0 && s2 <= 0 && s3 <= 0);
                return inside ? -d : d;
            });
            return this;
        }

        /// <summary>Белая фигура с прозрачным фоном: цвет задаёт Image.color.</summary>
        public Sprite ToSprite(int size = 96)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var scale = size / Grid;
            var pixels = new Color32[size * size];
            for (var py = 0; py < size; py++)
            {
                for (var px = 0; px < size; px++)
                {
                    var x = (px + 0.5f) / scale;
                    var y = (size - py - 0.5f) / scale;
                    var d = float.MaxValue;
                    foreach (var shape in _shapes)
                    {
                        d = Mathf.Min(d, shape(x, y));
                    }

                    var alpha = Mathf.Clamp01(0.5f - d * scale);
                    pixels[py * size + px] = new Color32(255, 255, 255, (byte)(alpha * 255));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static float Length(float x, float y) => Mathf.Sqrt(x * x + y * y);

        private static float Cross(float ax, float ay, float bx, float by) => ax * by - ay * bx;

        private static float SegmentDistance(float x, float y, float ax, float ay, float bx, float by)
        {
            var dx = bx - ax;
            var dy = by - ay;
            var lengthSq = dx * dx + dy * dy;
            var t = lengthSq > 0 ? Mathf.Clamp01(((x - ax) * dx + (y - ay) * dy) / lengthSq) : 0;
            return Length(x - (ax + t * dx), y - (ay + t * dy));
        }
    }
}
