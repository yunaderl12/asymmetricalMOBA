using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// MaskableGraphic that draws the diamond silhouette baked from
    /// Design/skill_tantai.svg (viewBox 0 0 148 155.91, polygon vertices:
    /// 0,54.45  74.14,155.91  148,54.25  74.14,0).
    ///
    /// The shape fills its RectTransform. Tinting (e.g. by class color) is done
    /// via the Graphic.color property — the silhouette stays the same.
    /// </summary>
    public class DiamondGraphic : MaskableGraphic
    {
        // Source viewBox dimensions
        private const float SrcW = 148f;
        private const float SrcH = 155.91f;

        // Source polygon vertices (in SVG coordinate space, Y goes DOWN).
        private static readonly Vector2[] SrcVerts = new Vector2[]
        {
            new Vector2(0f,      54.45f),  // left
            new Vector2(74.14f,  155.91f), // bottom
            new Vector2(148f,    54.25f),  // right
            new Vector2(74.14f,  0f),      // top
        };

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = rectTransform.rect;
            Vector2 c = new Vector2(rect.center.x, rect.center.y);
            float w = rect.width;
            float h = rect.height;

            // Add a center vertex for a triangle fan.
            var center = UIVertex.simpleVert;
            center.color = color;
            center.position = new Vector3(c.x, c.y, 0f);
            vh.AddVert(center);

            for (int i = 0; i < SrcVerts.Length; i++)
            {
                // Normalize SVG coords to [-0.5, 0.5] in each axis,
                // flipping Y (Unity UI is Y-up, SVG is Y-down).
                float nx = SrcVerts[i].x / SrcW - 0.5f;
                float ny = -(SrcVerts[i].y / SrcH - 0.5f);

                var v = UIVertex.simpleVert;
                v.color = color;
                v.position = new Vector3(c.x + nx * w, c.y + ny * h, 0f);
                vh.AddVert(v);
            }

            int n = SrcVerts.Length;
            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                vh.AddTriangle(0, 1 + i, 1 + next);
            }
        }
    }
}
