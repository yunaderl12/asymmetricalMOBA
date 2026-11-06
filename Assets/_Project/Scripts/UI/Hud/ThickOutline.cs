using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// 文字の太い縁取り。標準の Outline は4方向に少しずらすだけなので、円周上にずらした写しを何枚も重ねて丸く太く縁取る。
    /// 写しは文字の形（フォントの透明度）だけを使うので、Text 向け。
    public class ThickOutline : BaseMeshEffect
    {
        public Color effectColor = new Color(0.16f, 0.1f, 0.24f, 1f);
        public float size = 4f;
        [Range(4, 24)] public int steps = 12;
        [Tooltip("Extra copy pushed down, like the hard shadow under the HUD pieces. 0 = none.")]
        public float drop = 0f;

        private static readonly List<UIVertex> Source = new List<UIVertex>();
        private static readonly List<UIVertex> Result = new List<UIVertex>();

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || size <= 0f) return;
            Source.Clear();
            vh.GetUIVertexStream(Source);
            if (Source.Count == 0) return;

            Result.Clear();
            if (drop > 0f) AddCopies(new Vector2(0f, -drop));
            AddCopies(Vector2.zero);
            Result.AddRange(Source);

            vh.Clear();
            vh.AddUIVertexTriangleStream(Result);
        }

        private void AddCopies(Vector2 shift)
        {
            for (int k = 0; k < steps; k++)
            {
                float angle = (float)k / steps * Mathf.PI * 2f;
                Vector3 offset = new Vector3(Mathf.Cos(angle) * size + shift.x, Mathf.Sin(angle) * size + shift.y, 0f);
                for (int i = 0; i < Source.Count; i++)
                {
                    var v = Source[i];
                    v.position += offset;
                    Color32 c = effectColor;
                    c.a = (byte)(c.a * v.color.a / 255);
                    v.color = c;
                    Result.Add(v);
                }
            }
        }
    }
}
