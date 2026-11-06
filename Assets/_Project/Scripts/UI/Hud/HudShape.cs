using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// HUDの部品の形。角丸の四角・円・両端が切れ込んだリボン・ふちがやぶれた紙を描く。
    /// texture を入れると、その模様を tileSize ごとに繰り返して色を掛け合わせる（布や紙の質感）。
    public class HudShape : MaskableGraphic
    {
        public enum Kind { RoundRect, Circle, Ribbon, Torn }

        private const int CornerSegments = 6;
        private const int CircleSegments = 48;

        public Kind kind = Kind.RoundRect;
        public float radius = 12f;
        [Tooltip("Depth of the V cut at both ends of a ribbon.")]
        public float notch = 18f;
        [Tooltip("Pattern of the torn edge. Different numbers give different tears.")]
        public int seed;
        public Texture texture;
        public float tileSize = 256f;

        private static readonly List<Vector2> Outline = new List<Vector2>();

        public override Texture mainTexture => texture != null ? texture : s_WhiteTexture;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            if (r.width <= 0.5f || r.height <= 0.5f) return;

            Outline.Clear();
            switch (kind)
            {
                case Kind.Circle: BuildEllipse(r); break;
                case Kind.Ribbon: BuildRibbon(r); break;
                case Kind.Torn: BuildTorn(r); break;
                default: BuildRoundRect(r, radius); break;
            }

            // 中心から扇形に張る。どの形も中心から外周が見通せるので、これで穴なく埋まる。
            AddVertex(vh, r.center);
            for (int i = 0; i < Outline.Count; i++) AddVertex(vh, Outline[i]);
            int n = Outline.Count;
            for (int i = 0; i < n; i++) vh.AddTriangle(0, i + 1, (i + 1) % n + 1);
        }

        private void AddVertex(VertexHelper vh, Vector2 p)
        {
            Vector2 uv = texture != null && tileSize > 0f ? p / tileSize : Vector2.zero;
            vh.AddVert(p, color, uv);
        }

        /// 角丸の四角の外周（左回り）。HudStitch も同じ形で縫い目を引く。
        public static void AppendRoundRect(List<Vector2> points, Rect r, float cornerRadius, int segments = CornerSegments)
        {
            float rad = Mathf.Max(0f, Mathf.Min(cornerRadius, r.width * 0.5f, r.height * 0.5f));
            var centers = new[]
            {
                new Vector2(r.xMax - rad, r.yMax - rad),
                new Vector2(r.xMin + rad, r.yMax - rad),
                new Vector2(r.xMin + rad, r.yMin + rad),
                new Vector2(r.xMax - rad, r.yMin + rad),
            };
            for (int corner = 0; corner < 4; corner++)
            {
                for (int i = 0; i <= segments; i++)
                {
                    float angle = (corner + (float)i / segments) * Mathf.PI * 0.5f;
                    points.Add(centers[corner] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * rad);
                }
            }
        }

        public static void AppendEllipse(List<Vector2> points, Rect r, int segments = CircleSegments)
        {
            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                points.Add(r.center + new Vector2(Mathf.Cos(angle) * r.width * 0.5f, Mathf.Sin(angle) * r.height * 0.5f));
            }
        }

        private void BuildRoundRect(Rect r, float rad) => AppendRoundRect(Outline, r, rad);

        private void BuildEllipse(Rect r) => AppendEllipse(Outline, r);

        private void BuildRibbon(Rect r)
        {
            float cut = Mathf.Min(notch, r.width * 0.25f);
            float cy = r.center.y;
            Outline.Add(new Vector2(r.xMax, r.yMax));
            Outline.Add(new Vector2(r.xMin, r.yMax));
            Outline.Add(new Vector2(r.xMin + cut, cy));
            Outline.Add(new Vector2(r.xMin, r.yMin));
            Outline.Add(new Vector2(r.xMax, r.yMin));
            Outline.Add(new Vector2(r.xMax - cut, cy));
        }

        /// 上下のふちを細かくぎざぎざに、左右は少しだけ揺らす。seed が同じなら同じ破れ方になる。
        private void BuildTorn(Rect r)
        {
            var rng = new System.Random(seed * 7919 + 17);
            float jag = Mathf.Min(6f, r.height * 0.08f);
            int across = Mathf.Max(6, Mathf.RoundToInt(r.width / 34f));
            int down = Mathf.Max(2, Mathf.RoundToInt(r.height / 30f));
            float J() => (float)rng.NextDouble() * jag;

            for (int i = 0; i <= across; i++) Outline.Add(new Vector2(Mathf.Lerp(r.xMax, r.xMin, (float)i / across), r.yMax - J()));
            for (int i = 1; i < down; i++) Outline.Add(new Vector2(r.xMin + J() * 0.5f, Mathf.Lerp(r.yMax, r.yMin, (float)i / down)));
            for (int i = 0; i <= across; i++) Outline.Add(new Vector2(Mathf.Lerp(r.xMin, r.xMax, (float)i / across), r.yMin + J()));
            for (int i = 1; i < down; i++) Outline.Add(new Vector2(r.xMax - J() * 0.5f, Mathf.Lerp(r.yMin, r.yMax, (float)i / down)));
        }
    }
}
