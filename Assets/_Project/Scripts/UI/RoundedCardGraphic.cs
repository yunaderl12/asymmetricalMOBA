using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// 角の丸い四角。上下で色を変えられる（Graphic.color に掛け合わせる）。
    public class RoundedCardGraphic : MaskableGraphic
    {
        private const int CornerSegments = 6;

        public float cornerRadius = 10f;
        public Color topTint = Color.white;
        public Color bottomTint = Color.white;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            float radius = Mathf.Min(cornerRadius, r.width * 0.5f, r.height * 0.5f);

            AddVertex(vh, r, r.center);
            var centers = new[]
            {
                new Vector2(r.xMax - radius, r.yMax - radius),
                new Vector2(r.xMin + radius, r.yMax - radius),
                new Vector2(r.xMin + radius, r.yMin + radius),
                new Vector2(r.xMax - radius, r.yMin + radius),
            };
            for (int corner = 0; corner < 4; corner++)
            {
                for (int i = 0; i <= CornerSegments; i++)
                {
                    float angle = (corner + (float)i / CornerSegments) * Mathf.PI * 0.5f;
                    AddVertex(vh, r, centers[corner] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
                }
            }
            int rim = vh.currentVertCount - 1;
            for (int i = 0; i < rim; i++) vh.AddTriangle(0, i + 1, (i + 1) % rim + 1);
        }

        private void AddVertex(VertexHelper vh, Rect r, Vector2 p)
        {
            float t = r.height > 0f ? Mathf.InverseLerp(r.yMin, r.yMax, p.y) : 0f;
            vh.AddVert(p, color * Color.Lerp(bottomTint, topTint, t), Vector2.zero);
        }
    }
}
