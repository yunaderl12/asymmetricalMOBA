using UnityEngine;
using UnityEngine.UI;
using Game.Skill;

namespace Game.UI
{
    /// HPバーの見た目。減った分は淡い色で少し残り、遅れて追いつく。
    public class HealthBarGraphic : Graphic
    {
        private const float TrailDelay = 0.35f;
        private const float TrailSpeed = 0.9f;

        private float value = 1f;
        private float trail = 1f;
        private float trailWait;

        private Image source;
        private bool sourceRead;

        /// プレハブや既存UIのバー画像をこの見た目に置き換える。色は fill の色を受け継ぐ。背景は fill の親の画像。
        public static HealthBarGraphic Replace(Image fill)
        {
            var background = fill.transform.parent != null ? fill.transform.parent.GetComponent<Image>() : null;
            var host = background != null ? background.rectTransform : fill.rectTransform;
            var graphic = Create(host, fill.color);
            fill.enabled = false;
            if (background != null) background.enabled = false;
            return graphic;
        }

        /// 画像の fillAmount を今までどおり書き換えるだけのUI向け。値は毎フレーム読み取る。
        public static void Adopt(Image fill)
        {
            if (fill == null) return;
            Replace(fill).source = fill;
        }

        public static HealthBarGraphic Create(RectTransform parent, Color color)
        {
            var go = new GameObject("HealthBarGraphic", typeof(RectTransform), typeof(CanvasRenderer));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var graphic = go.AddComponent<HealthBarGraphic>();
            graphic.color = color;
            graphic.raycastTarget = false;
            graphic.material = FireballVfx.GetMaterial("VFX/HealthBar");

            var canvas = parent.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                canvas.rootCanvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2;
            }
            return graphic;
        }

        public void SetValue(float amount, bool immediate = false)
        {
            amount = Mathf.Clamp01(amount);
            if (immediate || amount > value) trail = amount;
            if (amount < value) trailWait = TrailDelay;
            value = amount;
            SetVerticesDirty();
        }

        private void Update()
        {
            if (source != null && (!sourceRead || !Mathf.Approximately(source.fillAmount, value)))
            {
                SetValue(source.fillAmount, !sourceRead);
                sourceRead = true;
            }
            if (trail <= value) return;
            if (trailWait > 0f)
            {
                trailWait -= Time.deltaTime;
                return;
            }
            trail = Mathf.MoveTowards(trail, value, TrailSpeed * Time.deltaTime);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            if (r.height <= 0f) return;
            var values = new Vector2(value, trail);
            var shape = new Vector2(r.width / r.height, 0f);
            AddVertex(vh, new Vector2(r.xMin, r.yMin), new Vector2(0f, 0f), values, shape);
            AddVertex(vh, new Vector2(r.xMin, r.yMax), new Vector2(0f, 1f), values, shape);
            AddVertex(vh, new Vector2(r.xMax, r.yMax), new Vector2(1f, 1f), values, shape);
            AddVertex(vh, new Vector2(r.xMax, r.yMin), new Vector2(1f, 0f), values, shape);
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }

        private void AddVertex(VertexHelper vh, Vector2 position, Vector2 uv, Vector2 values, Vector2 shape)
        {
            var vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = color;
            vertex.uv0 = uv;
            vertex.uv1 = values;
            vertex.uv2 = shape;
            vh.AddVert(vertex);
        }
    }
}
