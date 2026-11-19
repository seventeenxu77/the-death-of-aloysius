using UnityEngine;
using UnityEngine.UI;

namespace Aloysius.Level1
{
    /// <summary>Paper and ink are geometry, so the comic border stays sharp at any resolution.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class Level1ComicFrame : MaskableGraphic
    {
        public Color Paper = new Color(.965f, .96f, .90f, 1);
        public Color Ink = new Color(.015f, .008f, .005f, 1);
        public float Border = 30;
        public float InkWidth = 5;
        public float InnerInkWidth = 2.8f;
        public float Roughness = 1.1f;
        public bool CornerMarks = true;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            Rect image = new Rect(r.xMin + Border, r.yMin + Border, r.width - Border * 2, r.height - Border * 2);
            Ring(vh, r, Inset(r, InkWidth), Ink, true, true);
            Ring(vh, Inset(r, InkWidth), Expand(image, InnerInkWidth), Paper, true, false);
            Ring(vh, Expand(image, InnerInkWidth), image, Ink, false, false);
            if (CornerMarks)
            {
                foreach (int sx in new[] { -1, 1 })
                foreach (int sy in new[] { -1, 1 })
                {
                    Vector2 c = new Vector2(sx < 0 ? r.xMin : r.xMax, sy < 0 ? r.yMin : r.yMax);
                    Stroke(vh, c + new Vector2(-sx * 7, -sy * 8), c + new Vector2(-sx * 14, -sy * 21), 1.7f);
                    Stroke(vh, c + new Vector2(-sx * 16, -sy * 8), c + new Vector2(-sx * 27, -sy * 11), 1.4f);
                }
            }
        }

        static Rect Inset(Rect r, float d) => new Rect(r.x + d, r.y + d, r.width - d * 2, r.height - d * 2);
        static Rect Expand(Rect r, float d) => Inset(r, -d);
        Vector2 Edge(Rect r, int side, float t, bool rough)
        {
            Vector2[] corners = { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax) };
            Vector2 p = Vector2.Lerp(corners[side], corners[(side + 1) % 4], t);
            if (rough)
            {
                float n = (Mathf.Sin(t * 79 + side * 9) * .55f + Mathf.Sin(t * 33 + side * 4) * .45f) * Roughness * Mathf.Sin(t * Mathf.PI);
                p += (side % 2 == 0 ? Vector2.up : Vector2.right) * n;
            }
            return p;
        }
        void Ring(VertexHelper vh, Rect outer, Rect inner, Color tint, bool roughOuter, bool roughInner)
        {
            const int steps = 24;
            for (int s = 0; s < 4; s++)
            for (int i = 0; i < steps; i++)
                Quad(vh, Edge(outer, s, i / (float)steps, roughOuter), Edge(outer, s, (i + 1f) / steps, roughOuter),
                    Edge(inner, s, (i + 1f) / steps, roughInner), Edge(inner, s, i / (float)steps, roughInner), tint);
        }
        void Stroke(VertexHelper vh, Vector2 a, Vector2 b, float width)
        {
            Vector2 d = (b - a).normalized, n = new Vector2(-d.y, d.x) * width * .5f;
            Quad(vh, a - n, b - n * .35f, b + n * .35f, a + n, Ink);
        }
        static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
        {
            int index = vh.currentVertCount;
            vh.AddVert(a, tint, Vector2.zero); vh.AddVert(b, tint, Vector2.zero);
            vh.AddVert(c, tint, Vector2.zero); vh.AddVert(d, tint, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2); vh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
