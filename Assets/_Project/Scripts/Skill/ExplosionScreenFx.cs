using UnityEngine;
using UnityEngine.UI;

namespace Game.Skill
{
    /// Explosion の瞬間の画面演出。黒地に白い集中線 → 色の反転 → 青みが抜けていく、の順に一瞬で切り替える。HUD の下に描く。
    public class ExplosionScreenFx : MonoBehaviour
    {
        private const int SortingOrder = -100;
        private const float BurstSmallSeconds = 0.04f;
        private const float BurstLargeSeconds = 0.08f;
        private const float InvertSeconds = 0.15f;
        private const float WhiteOutSeconds = 0.3f;
        private const float TintSeconds = 1.5f;
        private const float BurstSmallRadius = 0.42f;
        private const float BurstLargeRadius = 0.72f;
        private const float MaxDistance = 90f;

        private static readonly Color TintColor = new Color(0.72f, 0.88f, 1f, 1f);
        private static readonly Color WhiteStart = new Color(1f, 1f, 1f, 0.7f);

        private RawImage burst;
        private RawImage invert;
        private RawImage white;
        private RawImage tint;
        private Material burstMaterial;
        private float timer;

        public static void Play(Vector3 worldPoint)
        {
            var camera = Camera.main;
            if (camera == null || Vector3.Distance(camera.transform.position, worldPoint) > MaxDistance) return;

            var go = new GameObject("ExplosionScreenFx");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var fx = go.AddComponent<ExplosionScreenFx>();
            fx.Build(camera, worldPoint);
        }

        private RawImage NewLayer(string name, Material material)
        {
            var layer = new GameObject(name, typeof(RectTransform));
            layer.transform.SetParent(transform, false);
            var rect = (RectTransform)layer.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = layer.AddComponent<RawImage>();
            image.raycastTarget = false;
            image.material = material;
            image.enabled = false;
            return image;
        }

        private void Build(Camera camera, Vector3 worldPoint)
        {
            tint = NewLayer("Tint", FireballVfx.GetMaterial("VFX/ScreenMultiply"));
            invert = NewLayer("Invert", FireballVfx.GetMaterial("VFX/ScreenInvert"));
            white = NewLayer("White", null);
            burstMaterial = new Material(FireballVfx.GetMaterial("VFX/ImpactBurst"));
            burst = NewLayer("Burst", burstMaterial);

            Vector3 viewport = camera.WorldToViewportPoint(worldPoint);
            Vector2 center = viewport.z > 0f ? new Vector2(viewport.x, viewport.y) : new Vector2(0.5f, 0.5f);
            burstMaterial.SetVector("_Center", center);
            burstMaterial.SetFloat("_Aspect", camera.aspect);
            burstMaterial.SetFloat("_Seed", Random.Range(0f, 50f));
            Apply();
        }

        private void Update()
        {
            timer += Time.unscaledDeltaTime;
            if (timer >= TintSeconds)
            {
                Destroy(burstMaterial);
                Destroy(gameObject);
                return;
            }
            Apply();
        }

        private void Apply()
        {
            burst.enabled = timer < BurstLargeSeconds;
            burstMaterial.SetFloat("_Radius", timer < BurstSmallSeconds ? BurstSmallRadius : BurstLargeRadius);

            invert.enabled = timer >= BurstLargeSeconds && timer < InvertSeconds;

            white.enabled = timer >= BurstLargeSeconds && timer < WhiteOutSeconds;
            var w = WhiteStart;
            w.a *= 1f - Mathf.Clamp01((timer - BurstLargeSeconds) / (WhiteOutSeconds - BurstLargeSeconds));
            white.color = w;

            tint.enabled = timer >= InvertSeconds;
            tint.color = Color.Lerp(TintColor, Color.white, Mathf.Clamp01((timer - InvertSeconds) / (TintSeconds - InvertSeconds)));
        }
    }
}
