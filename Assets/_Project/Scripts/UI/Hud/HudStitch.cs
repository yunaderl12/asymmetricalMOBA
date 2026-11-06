using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// フェルトの縫い目。角丸の四角か円のふちに沿って、短い線を間隔をあけて並べる。
    public class HudStitch : MaskableGraphic
    {
        public bool circle;
        public float radius = 12f;
        public float dash = 10f;
        public float gap = 8f;
        public float thickness = 3f;

        private static readonly List<Vector2> Path = new List<Vector2>();

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            if (r.width <= 1f || r.height <= 1f) return;

            Path.Clear();
            if (circle) HudShape.AppendEllipse(Path, r, 96);
            else HudShape.AppendRoundRect(Path, r, radius, 8);
            Path.Add(Path[0]);

            // 線と隙間の繰り返しの中の位置（phase）を小さいまま持ち歩く。
            // 通った距離の合計から割り算で求めると、長い縫い目で小数の誤差が出て先へ進まなくなる（無限ループ）ので使わない。
            const float MinStep = 0.01f;
            float d = Mathf.Max(1f, dash), g = Mathf.Max(0f, gap);
            float period = d + g;
            float phase = 0f;
            for (int i = 0; i + 1 < Path.Count; i++)
            {
                Vector2 a = Path[i], b = Path[i + 1];
                float len = Vector2.Distance(a, b);
                if (len < MinStep) continue;
                Vector2 dir = (b - a) / len;
                float s = 0f;
                while (len - s > MinStep)
                {
                    bool drawing = phase < d;
                    float run = Mathf.Min(drawing ? d - phase : period - phase, len - s);
                    run = Mathf.Max(run, MinStep);
                    if (drawing) AddSegment(vh, a + dir * s, a + dir * Mathf.Min(len, s + run), dir);
                    s += run;
                    phase += run;
                    if (phase >= period - MinStep) phase = 0f;
                }
            }
        }

        private void AddSegment(VertexHelper vh, Vector2 p0, Vector2 p1, Vector2 dir)
        {
            Vector2 n = new Vector2(-dir.y, dir.x) * (thickness * 0.5f);
            int start = vh.currentVertCount;
            vh.AddVert(p0 - n, color, Vector2.zero);
            vh.AddVert(p0 + n, color, Vector2.zero);
            vh.AddVert(p1 + n, color, Vector2.zero);
            vh.AddVert(p1 - n, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
