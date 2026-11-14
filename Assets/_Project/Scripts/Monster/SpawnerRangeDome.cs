using UnityEngine;
using Game.Core;

namespace Game.Monster
{
    /// 塔の射程（塔が勇者を撃つ範囲）を見せる、ごく薄い球と、球が地面と交わるところの半透明の赤い線。
    /// 勇者からも魔王からも見える。塔が壊れたら消す。
    public class SpawnerRangeDome : MonoBehaviour
    {
        private const int RingSegments = 160;
        private const float RingLift = 0.12f;
        private const float RaycastTop = 120f;

        private static Material domeMaterial;
        private static Material insideMaterial;
        private MeshRenderer domeRenderer;
        private float radius;
        private static Material ringMaterial;

        private Spawner spawner;
        private GameObject dome;
        private LineRenderer ring;

        public static SpawnerRangeDome Create(Spawner spawner, float radius)
        {
            var go = new GameObject("RangeDome");
            go.transform.SetParent(spawner.transform, false);
            var view = go.AddComponent<SpawnerRangeDome>();
            view.spawner = spawner;
            view.Build(radius);
            return view;
        }

        private void Build(float radius)
        {
            this.radius = radius;
            var config = BalanceConfig.Instance;
            if (domeMaterial == null)
            {
                var shader = Shader.Find("Game/VFX/RangeDome");
                if (shader != null)
                {
                    domeMaterial = new Material(shader);
                    domeMaterial.SetColor("_Color", config.towerRangeDomeColor);
                    domeMaterial.SetFloat("_Base", config.towerRangeDomeBaseAlpha);
                    domeMaterial.SetFloat("_Rim", config.towerRangeDomeRimAlpha);
                    domeMaterial.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Back);
                    insideMaterial = new Material(domeMaterial);
                    insideMaterial.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Front);
                }
            }
            if (ringMaterial == null) ringMaterial = new Material(Shader.Find("Sprites/Default"));

            if (domeMaterial != null)
            {
                dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dome.name = "Dome";
                Destroy(dome.GetComponent<Collider>());
                dome.transform.SetParent(transform, false);
                dome.transform.localScale = Vector3.one * (radius * 2f);
                domeRenderer = dome.GetComponent<MeshRenderer>();
                domeRenderer.sharedMaterial = domeMaterial;
                domeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                domeRenderer.receiveShadows = false;
            }

            var ringGo = new GameObject("GroundRing");
            ringGo.transform.SetParent(transform, false);
            ring = ringGo.AddComponent<LineRenderer>();
            ring.sharedMaterial = ringMaterial;
            ring.useWorldSpace = true;
            ring.loop = true;
            ring.widthMultiplier = config.towerRangeRingWidth;
            ring.startColor = ring.endColor = config.towerRangeRingColor;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;
            ring.numCornerVertices = 2;
            ring.positionCount = RingSegments;
            Vector3 center = spawner.transform.position;
            for (int i = 0; i < RingSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / RingSegments;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                ring.SetPosition(i, GroundCrossing(center, dir, radius) + Vector3.up * RingLift);
            }
        }

        // 球（中心 center、半径 radius）がその向きで地面と交わる点。地面の高さが中心とずれているほど、水平の半径は短くなる。
        private static Vector3 GroundCrossing(Vector3 center, Vector3 dir, float radius)
        {
            float r = radius;
            float y = center.y;
            for (int k = 0; k < 4; k++)
            {
                y = GroundHeight(center.x + dir.x * r, center.z + dir.z * r, center.y);
                float dy = Mathf.Min(Mathf.Abs(y - center.y), radius * 0.98f);
                r = Mathf.Sqrt(radius * radius - dy * dy);
            }
            return new Vector3(center.x + dir.x * r, y, center.z + dir.z * r);
        }

        private static float GroundHeight(float x, float z, float fallback)
        {
            if (StageGround.Available) return StageGround.HeightAt(x, z);
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(x, fallback + RaycastTop, z), Vector3.down, out hit, RaycastTop * 2f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return fallback;
        }

        private void Update()
        {
            bool show = spawner != null && spawner.IsFunctional;
            if (dome != null && dome.activeSelf != show) dome.SetActive(show);
            if (ring != null && ring.enabled != show) ring.enabled = show;
            if (!show && spawner != null && !spawner.IsFunctional) { enabled = false; return; }
            // 射程の中にいるカメラには内側の面を見せる（両面を描くと重なって濃くなるので、どちらか片方だけ）。
            var cam = Camera.main;
            if (domeRenderer != null && cam != null)
            {
                bool inside = (cam.transform.position - transform.position).sqrMagnitude < radius * radius;
                var want = inside ? insideMaterial : domeMaterial;
                if (domeRenderer.sharedMaterial != want) domeRenderer.sharedMaterial = want;
            }
        }
    }
}
