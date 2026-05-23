using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// MaskableGraphic that draws a filled ellipse inscribed in the RectTransform.
    /// Approximated by a triangle fan with `segments` outer vertices.
    /// </summary>
    public class EllipseGraphic : MaskableGraphic
    {
        [SerializeField] private int segments = 48;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = rectTransform.rect;
            float rx = rect.width * 0.5f;
            float ry = rect.height * 0.5f;
            Vector2 c = new Vector2(rect.center.x, rect.center.y);
            int n = Mathf.Max(6, segments);

            var center = UIVertex.simpleVert;
            center.color = color;
            center.position = new Vector3(c.x, c.y, 0f);
            vh.AddVert(center);

            for (int i = 0; i < n; i++)
            {
                float deg = -90f + (360f / n) * i;
                float rad = deg * Mathf.Deg2Rad;
                var v = UIVertex.simpleVert;
                v.color = color;
                v.position = new Vector3(c.x + Mathf.Cos(rad) * rx, c.y - Mathf.Sin(rad) * ry, 0f);
                vh.AddVert(v);
            }
            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                vh.AddTriangle(0, 1 + i, 1 + next);
            }
        }
    }
}
