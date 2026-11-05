#nullable enable
using UnityEngine;
using UnityEngine.UI;

namespace KnittedPicture.Game
{
    /// <summary>Непрерывная лента с общими вершинами; буфер геометрии переиспользуется.</summary>
    public sealed class LinkedCordMesh : MaskableGraphic
    {
        private readonly LinkCordRaster.Sample[] _samples = new LinkCordRaster.Sample[LinkCordRaster.MaxSegments + 1];
        private int _count;
        private Texture? _texture;
        public override Texture mainTexture => _texture != null ? _texture : Texture2D.whiteTexture;

        public void Set(Vector2 a, Vector2 b, float sag, Color tint)
        {
            if (_texture == null)
            {
                _texture = Art2.Tile("linkedCord", LinkCordRaster.Tile);
                SetMaterialDirty();
            }
            var pixelScale = canvas != null ? canvas.scaleFactor : 1f;
            _count = LinkCordRaster.Fill(_samples, a.x, a.y, b.x, b.y, sag, pixelScale);
            color = tint;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = rectTransform.rect;
            for (var i = 0; i <= _count; i++)
            {
                var p = _samples[i];
                var x = rect.xMin + p.X;
                var y = rect.yMax - p.Y;
                var nx = p.Nx * 4f;
                var ny = -p.Ny * 4f;
                vh.AddVert(new Vector3(x - nx, y - ny), color, new Vector2(p.U, 0f));
                vh.AddVert(new Vector3(x + nx, y + ny), color, new Vector2(p.U, 1f));
                if (i > 0)
                {
                    var n = i * 2;
                    vh.AddTriangle(n - 2, n - 1, n);
                    vh.AddTriangle(n, n - 1, n + 1);
                }
            }
        }
    }

    /// <summary>Общий кручёный шнур с двумя узлами и необязательным бликом.</summary>
    public sealed class LinkedCordGraphic
    {
        private readonly RectTransform _parent;
        private readonly LinkedCordMesh _mesh;
        private readonly Image[] _knots = new Image[2];
        private readonly Color _color;
        private readonly Image _glint;

        public LinkedCordGraphic(RectTransform parent, Color color)
        {
            _parent = parent;
            _color = color;
            Root = Ui.Stretch(Ui.Node(parent, "LinkedCord"));
            _mesh = Root.gameObject.AddComponent<LinkedCordMesh>();
            _mesh.raycastTarget = false;
            for (var i = 0; i < 2; i++)
            {
                _knots[i] = Ui.Image(Root, "Knot", Art2.Get("linkedKnot", LinkCordRaster.Knot), color);
            }
            _glint = Ui.Image(Root, "Glint", Art.Circle, Color.clear);
        }

        public RectTransform Root { get; }

        /// <summary>Позиции заданы от левого верхнего угла родителя в точках интерфейса.</summary>
        public void Set(Vector2 a, Vector2 b, float sag, float opacity, float glint = -1f, float inset = 0f)
        {
            _mesh.Set(a, b, sag, Palette.WithAlpha(_color, opacity));
            var sparkle = LinkCordRaster.Point(a.x, a.y, b.x, b.y, sag, Mathf.Clamp01(glint));
            Ui.PlaceCenter(_glint.rectTransform, sparkle.X, sparkle.Y, 4f, 4f);
            _glint.color = Palette.WithAlpha(Color.Lerp(_color, Color.white, 0.7f), glint >= 0f ? opacity * 0.8f : 0f);
            var distance = (b - a).magnitude;
            var offset = Mathf.Min(inset, distance * 0.45f);
            var knotT = distance > 0.0001f ? offset / distance : 0f;
            for (var i = 0; i < 2; i++)
            {
                var point = LinkCordRaster.Point(a.x, a.y, b.x, b.y, sag, i == 0 ? knotT : 1f - knotT);
                Ui.PlaceCenter(_knots[i].rectTransform, point.X, point.Y, 11f, 11f);
                _knots[i].color = Palette.WithAlpha(_color, opacity);
            }
        }

        /// <summary>Перевод мировой позиции центра в координаты родительского слоя.</summary>
        public Vector2 FromWorld(Vector3 world)
        {
            var local = _parent.InverseTransformPoint(world);
            return new Vector2(local.x - _parent.rect.xMin, _parent.rect.yMax - local.y);
        }
    }
}
