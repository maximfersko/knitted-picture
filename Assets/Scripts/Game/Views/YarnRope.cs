#nullable enable

using UnityEngine;
using UnityEngine.UI;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Рабочая нить: кручёная верёвка через опорные точки (сплайн Катмулла — Рома: проходит через
    /// каждую точку, изгибы плавные). Меш — полоса по кривой; текстура нити повторяется по длине
    /// (витки не растягиваются) и бежит вдоль верёвки, когда нить тянется в петлю. Цвет пряжи — цвет вершин.
    /// Точки — в локальных координатах родителя (слой растянут на экран).
    /// </summary>
    public sealed class YarnRope : MaskableGraphic
    {
        private const int PerSegment = 10;

        /// <summary>Опорные точки от клубка к спице.</summary>
        public Vector2[] Points = new Vector2[0];

        /// <summary>Толщина в точках.</summary>
        public float Thickness = 4f;

        /// <summary>Сдвиг текстуры вдоль нити: растёт, когда нить уходит в петли.</summary>
        public float Flow;

        /// <summary>Какая доля верёвки видна от клубка (0 — смотана, 1 — до спицы).</summary>
        public float Reach = 1f;

        public override Texture mainTexture => Art2.Rope;

        public void Rebuild() => SetVerticesDirty();

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Reach <= 0.01f || Points.Length < 2)
            {
                return;
            }

            var tint = (Color32)color;
            var half = Thickness / 2f;
            var segments = (Points.Length - 1) * PerSegment;
            var length = 0f;
            var previous = Point(0f);
            for (var i = 0; i <= segments; i++)
            {
                var t = Reach * i / segments;
                var p = Point(t);
                var ahead = Point(Mathf.Min(1f, t + 0.004f));
                var behind = Point(Mathf.Max(0f, t - 0.004f));
                var tangent = (ahead - behind).normalized;
                if (tangent.sqrMagnitude < 0.0001f)
                {
                    tangent = Vector2.up;
                }

                var normal = new Vector2(-tangent.y, tangent.x) * half;
                length += Vector2.Distance(previous, p);
                previous = p;
                var u = length / (Thickness * 4f) - Flow;
                vh.AddVert(p + normal, tint, new Vector4(u, 1f, 0f, 0f));
                vh.AddVert(p - normal, tint, new Vector4(u, 0f, 0f, 0f));
                if (i > 0)
                {
                    var k = i * 2;
                    vh.AddTriangle(k - 2, k - 1, k);
                    vh.AddTriangle(k - 1, k + 1, k);
                }
            }
        }

        /// <summary>Точка сплайна при t ∈ [0, 1] по всей длине.</summary>
        private Vector2 Point(float t)
        {
            var n = Points.Length - 1;
            var f = Mathf.Clamp01(t) * n;
            var i = Mathf.Min((int)f, n - 1);
            var u = f - i;
            var p0 = Points[Mathf.Max(0, i - 1)];
            var p1 = Points[i];
            var p2 = Points[i + 1];
            var p3 = Points[Mathf.Min(n, i + 2)];
            return 0.5f * (2f * p1 + (p2 - p0) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * (u * u) + (3f * p1 - p0 - 3f * p2 + p3) * (u * u * u));
        }
    }
}
