using UnityEngine;

namespace Game.Core
{
    /// 浅瀬を歩いたときに足元から広がる波紋。
    public class WaterRipple : MonoBehaviour
    {
        private const float WaterSurfaceY = 0.15f;
        private const float Seconds = 0.9f;
        private const float RingWidth = 0.12f;
        private static readonly Color RingColor = new Color(0.85f, 0.97f, 1f, 1f);
        private static readonly int RingId = Shader.PropertyToID("_Ring");
        private static readonly int WidthId = Shader.PropertyToID("_Width");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private static Mesh quad;
        private static Material material;
        private static MaterialPropertyBlock block;

        private MeshRenderer meshRenderer;
        private float age;

        public static void Spawn(Vector3 position, float radius)
        {
            if (!EnsureAssets()) return;
            var go = new GameObject("WaterRipple");
            go.transform.position = new Vector3(position.x, WaterSurfaceY, position.z);
            go.transform.localScale = new Vector3(radius * 2f, 1f, radius * 2f);
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            var ripple = go.AddComponent<WaterRipple>();
            ripple.meshRenderer = go.AddComponent<MeshRenderer>();
            ripple.meshRenderer.sharedMaterial = material;
            ripple.meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ripple.meshRenderer.receiveShadows = false;
            ripple.Apply();
        }

        private static bool EnsureAssets()
        {
            if (material == null)
            {
                var shader = Resources.Load<Shader>("VFX/ShockRing");
                if (shader == null) return false;
                material = new Material(shader);
                block = new MaterialPropertyBlock();
            }
            if (quad == null)
            {
                quad = new Mesh { name = "WaterRippleQuad" };
                quad.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) };
                quad.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
                quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                quad.RecalculateBounds();
            }
            return true;
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= Seconds)
            {
                Destroy(gameObject);
                return;
            }
            Apply();
        }

        private void Apply()
        {
            float k = age / Seconds;
            Color color = RingColor;
            color.a = 1f - k;
            block.SetFloat(RingId, Mathf.Lerp(0.1f, 0.95f, 1f - (1f - k) * (1f - k)));
            block.SetFloat(WidthId, RingWidth);
            block.SetColor(ColorId, color);
            meshRenderer.SetPropertyBlock(block);
        }
    }
}
