using UnityEngine;

namespace Game.Monster
{
    /// 魔物が勇者を見つけたときに頭の上に出る「！」。
    public class AlertMark : MonoBehaviour
    {
        private const float PopSeconds = 0.14f;
        private const float HoldSeconds = 1.1f;
        private const float ShrinkSeconds = 0.16f;
        private const float BaseScale = 0.62f;

        private static Mesh mesh;
        private static Material material;

        private float age;

        public static void Show(Transform owner, float height)
        {
            if (!EnsureAssets()) return;
            var go = new GameObject("AlertMark");
            go.transform.SetParent(owner, false);
            go.transform.localPosition = Vector3.up * height;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            go.AddComponent<AlertMark>().Apply();
        }

        private static bool EnsureAssets()
        {
            if (material == null)
            {
                var shader = Resources.Load<Shader>("VFX/AlertMark");
                if (shader == null) return false;
                material = new Material(shader);
            }
            if (mesh == null) mesh = BuildMesh();
            return true;
        }

        // 上が太い棒と、下の点。正面（-z）を向いた平らな角ばった形。
        private static Mesh BuildMesh()
        {
            var m = new Mesh { name = "AlertMark" };
            m.vertices = new[]
            {
                new Vector3(-0.13f, 0.28f, 0f), new Vector3(0.13f, 0.28f, 0f), new Vector3(0.2f, 1f, 0f), new Vector3(-0.2f, 1f, 0f),
                new Vector3(0f, -0.16f, 0f), new Vector3(0.17f, 0.01f, 0f), new Vector3(0f, 0.18f, 0f), new Vector3(-0.17f, 0.01f, 0f),
            };
            m.triangles = new[] { 0, 3, 2, 0, 2, 1, 4, 7, 6, 4, 6, 5 };
            m.RecalculateBounds();
            return m;
        }

        private void LateUpdate()
        {
            age += Time.deltaTime;
            if (age >= PopSeconds + HoldSeconds + ShrinkSeconds)
            {
                Destroy(gameObject);
                return;
            }
            Apply();
        }

        private void Apply()
        {
            float scale;
            if (age < PopSeconds)
            {
                float k = age / PopSeconds;
                scale = Mathf.LerpUnclamped(0f, 1f, 1f + 2.2f * (k - 1f) * (k - 1f) * (k - 1f) + 1.2f * (k - 1f) * (k - 1f));
            }
            else if (age < PopSeconds + HoldSeconds) scale = 1f;
            else scale = 1f - (age - PopSeconds - HoldSeconds) / ShrinkSeconds;
            transform.localScale = Vector3.one * (BaseScale * scale);

            var cam = Camera.main;
            if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }
    }
}
