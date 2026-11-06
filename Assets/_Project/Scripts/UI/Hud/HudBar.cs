using UnityEngine;

namespace Game.UI
{
    /// HUDのバー。暗い溝に色を塗り、上側に細い光を入れる。減った分は淡い色で少し残ってから追いつく。
    /// SetPreview で、これから減る範囲を斜線で示せる（選択中スキルの消費SP）。
    public class HudBar : MonoBehaviour
    {
        private const float TrailDelay = 0.35f;
        private const float TrailSpeed = 0.9f;

        private RectTransform fillRect, trailRect, previewRect;
        private HudShape fillShape;
        private bool fromRight;
        private float value = 1f;
        private float trail = 1f;
        private float trailWait;
        private bool started;

        public HudShape Fill => fillShape;

        /// parent いっぱいにバーを作る。fromRight は右端から塗る（左右対称に並べるとき）。
        public static HudBar Create(RectTransform parent, Color color, Color track, bool fromRight = false)
        {
            float radius = parent.rect.height > 0f ? parent.rect.height * 0.5f : 12f;
            radius = Mathf.Max(radius, parent.sizeDelta.y * 0.5f);
            var bar = parent.gameObject.AddComponent<HudBar>();
            bar.fromRight = fromRight;
            HudStyle.Shape(parent, "Track", HudShape.Kind.RoundRect, track, radius);

            var trailShape = HudStyle.Shape(parent, "Trail", HudShape.Kind.RoundRect, Color.Lerp(color, Color.white, 0.55f), radius);
            bar.trailRect = trailShape.rectTransform;

            bar.fillShape = HudStyle.Shape(parent, "Fill", HudShape.Kind.RoundRect, color, radius);
            bar.fillRect = bar.fillShape.rectTransform;
            var shine = HudStyle.Shape(bar.fillRect, "Shine", HudShape.Kind.RoundRect, new Color(1f, 1f, 1f, 0.3f), radius * 0.4f);
            shine.rectTransform.anchorMin = new Vector2(0f, 0.62f);
            shine.rectTransform.offsetMin = new Vector2(radius * 0.5f, 0f);
            shine.rectTransform.offsetMax = new Vector2(-radius * 0.5f, -2f);

            var preview = HudStyle.Node(parent, "Preview");
            HudStyle.Stretch(preview);
            HudStyle.Shape(preview, "Base", HudShape.Kind.RoundRect, HudStyle.SpPreview, 0f);
            var stripes = HudStyle.Shape(preview, "Stripes", HudShape.Kind.RoundRect, color, 0f, HudStyle.HatchTexture);
            stripes.tileSize = 20f;
            bar.previewRect = preview;
            preview.gameObject.SetActive(false);

            bar.Apply();
            return bar;
        }

        public void SetColor(Color color)
        {
            if (fillShape != null && fillShape.color != color) fillShape.color = color;
        }

        public void Set(float value01)
        {
            value01 = Mathf.Clamp01(value01);
            if (!started) { trail = value01; started = true; }
            if (value01 < value) trailWait = TrailDelay;
            value = value01;
            if (trail < value) trail = value;
            Apply();
        }

        /// from から to（0〜1）の範囲を斜線にする。from >= to で消える。
        public void SetPreview(float from01, float to01)
        {
            bool show = to01 - from01 > 0.001f;
            if (previewRect.gameObject.activeSelf != show) previewRect.gameObject.SetActive(show);
            if (!show) return;
            Span(previewRect, Mathf.Clamp01(from01), Mathf.Clamp01(to01));
        }

        private void Update()
        {
            if (trail <= value) return;
            if (trailWait > 0f) { trailWait -= Time.unscaledDeltaTime; return; }
            trail = Mathf.MoveTowards(trail, value, TrailSpeed * Time.unscaledDeltaTime);
            Span(trailRect, 0f, trail);
        }

        private void Apply()
        {
            if (fillRect == null) return;
            Span(fillRect, 0f, value);
            Span(trailRect, 0f, trail);
            fillRect.gameObject.SetActive(value > 0.001f);
        }

        private void Span(RectTransform rt, float from01, float to01)
        {
            if (fromRight)
            {
                rt.anchorMin = new Vector2(1f - to01, 0f);
                rt.anchorMax = new Vector2(1f - from01, 1f);
            }
            else
            {
                rt.anchorMin = new Vector2(from01, 0f);
                rt.anchorMax = new Vector2(to01, 1f);
            }
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
