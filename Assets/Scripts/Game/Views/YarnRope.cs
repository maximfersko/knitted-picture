#nullable enable

using UnityEngine;
using UnityEngine.UI;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Рабочая нить: кручёная верёвка от клубка на станке к спице — две кубические кривые подряд
    /// (до точки Mid у края доски и от неё к спице), с общей касательной в Mid: изгиб плавный.
    /// Меш — полоса из 40 сегментов; текстура нити повторяется по длине (витки не растягиваются)
    /// и бежит вдоль верёвки, когда нить тянется в петлю. Цвет пряжи — цвет вершин.
    /// Точки — в локальных координатах родителя (слой растянут на экран).
    /// </summary>
    public sealed class YarnRope : MaskableGraphic
    {
        private const int Segments = 40;

        public Vector2 Start;
        public Vector2 Control1;
        public Vector2 MidIn;
        public Vector2 Mid;
        public Vector2 MidOut;
        public Vector2 Control2;
        public Vector2 End;

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
            if (Reach <= 0.01f)
            {
                return;
            }

            var tint = (Color32)color;
            var half = Thickness / 2f;

            // Длина для повтора текстуры: плитка 64×16 — виток на каждые Thickness·4/4 точек.
            var length = 0f;
            var previous = Point(0f);
            var u = 0f;
            for (var i = 0; i <= Segments; i++)
            {
                var t = Reach * i / Segments;
                var p = Point(t);
                var ahead = Point(Mathf.Min(1f, t + 0.01f));
                var behind = Point(Mathf.Max(0f, t - 0.01f));
                var tangent = (ahead - behind).normalized;
                if (tangent.sqrMagnitude < 0.0001f)
                {
                    tangent = Vector2.up;
                }

                var normal = new Vector2(-tangent.y, tangent.x) * half;
                length += Vector2.Distance(previous, p);
                previous = p;
                u = length / (Thickness * 4f) - Flow;
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

        private Vector2 Point(float t)
        {
            return t < 0.5f
                ? Cubic(Start, Control1, MidIn, Mid, t * 2f)
                : Cubic(Mid, MidOut, Control2, End, t * 2f - 1f);
        }

        private static Vector2 Cubic(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
        {
            var s = 1f - t;
            return s * s * s * a + 3f * s * s * t * b + 3f * s * t * t * c + t * t * t * d;
        }
    }
}
