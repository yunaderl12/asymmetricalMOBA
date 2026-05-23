using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// MaskableGraphic that draws a regular pentagon filling its RectTransform.
    /// One center vertex + 5 outer vertices (top pointing up) connected as a
    /// triangle fan. Use as the Graphic on a Button to make a pentagon-shaped
    /// clickable icon.
    /// </summary>
    public class PentagonGraphic : MaskableGraphic
    {
        [SerializeField] private float pointyTopRotation = 0f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = rectTransform.rect;
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            Vector2 center = new Vector2(rect.center.x, rect.center.y);

            UIVertex centerVert = UIVertex.simpleVert;
            centerVert.color = color;
            centerVert.position = new Vector3(center.x, center.y, 0f);
            vh.AddVert(centerVert);

            for (int i = 0; i < 5; i++)
            {
                float deg = -90f + i * 72f + pointyTopRotation; // 0 -> top vertex up
                // Note: in screen space Y is up, but Unity UI also uses Y-up local space.
                // Using +cos/+sin produces a "spinning right" sequence; -90° starts at the top.
                float rad = deg * Mathf.Deg2Rad;
                float x = Mathf.Cos(rad) * radius;
                float y = Mathf.Sin(rad) * radius;
                // Convert math angle (CCW from +x) to UI (top is -90°): use -y so top points up.
                // With deg=-90° -> y = sin(-90°)=-1 -> -y = +1 (top).
                var v = UIVertex.simpleVert;
                v.color = color;
                v.position = new Vector3(center.x + x, center.y - y, 0f);
                vh.AddVert(v);
            }

            for (int i = 0; i < 5; i++)
            {
                int next = (i + 1) % 5;
                vh.AddTriangle(0, 1 + i, 1 + next);
            }
        }
    }
}
