using UnityEngine;

namespace Game.Skill
{
    /// 地面から立つ光の柱。rising なら下から伸び、そうでなければ最初から全長で立って細くなりながら消える。
    public class LightPillar : MonoBehaviour
    {
        private const int Sides = 18;
        private const float RiseRatio = 0.35f;
        private const float StartWidth = 0.4f;

        private static Mesh cylinder;

        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock block;
        private float radius;
        private float height;
        private float seconds;
        private bool rising;
        private float timer;

        /// layer を指定すると、その層のカメラにだけ映る（自分を包む柱で自分の視界をふさがないため）。
        public static void Spawn(Vector3 position, float radius, float height, float seconds, Color color, bool rising, int layer = -1)
        {
            if (Physics.Raycast(position + Vector3.up, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore)) position = hit.point;
            var go = new GameObject("LightPillar");
            go.transform.position = position;
            var pillar = go.AddComponent<LightPillar>();
            pillar.radius = radius;
            pillar.height = height;
            pillar.seconds = seconds;
            pillar.rising = rising;
            pillar.block = new MaterialPropertyBlock();
            pillar.block.SetColor("_Color", color);
            pillar.meshRenderer = FireballVfx.NewMeshObject("Beam", go.transform, CylinderMesh, FireballVfx.GetMaterial("VFX/LightPillar"));
            if (layer >= 0) pillar.meshRenderer.gameObject.layer = layer;
            pillar.Apply(0f);
        }

        private void Update()
        {
            timer += Time.deltaTime;
            float k = timer / seconds;
            if (k >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            Apply(k);
        }

        private void Apply(float k)
        {
            float rise = rising ? Mathf.Clamp01(k / RiseRatio) : 1f;
            rise = 1f - (1f - rise) * (1f - rise);
            float width = rising
                ? Mathf.Lerp(StartWidth, 1f, rise)
                : (k < 0.15f ? Mathf.Lerp(StartWidth, 1f, k / 0.15f) : Mathf.Lerp(1f, 0.15f, (k - 0.15f) / 0.85f));
            meshRenderer.transform.localScale = new Vector3(radius * width, height * Mathf.Max(rise, 0.01f), radius * width);
            float fade = 1f - k;
            block.SetFloat("_Fade", fade * Mathf.Sqrt(fade));
            meshRenderer.SetPropertyBlock(block);
        }

        // 半径1、高さ1で底が原点の筒。
        private static Mesh CylinderMesh
        {
            get
            {
                if (cylinder != null) return cylinder;
                var vertices = new Vector3[(Sides + 1) * 2];
                var normals = new Vector3[vertices.Length];
                var uvs = new Vector2[vertices.Length];
                var triangles = new int[Sides * 6];
                for (int i = 0; i <= Sides; i++)
                {
                    float angle = i * Mathf.PI * 2f / Sides;
                    var normal = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    vertices[i * 2] = normal;
                    vertices[i * 2 + 1] = normal + Vector3.up;
                    normals[i * 2] = normals[i * 2 + 1] = normal;
                    uvs[i * 2] = new Vector2((float)i / Sides, 0f);
                    uvs[i * 2 + 1] = new Vector2((float)i / Sides, 1f);
                    if (i == Sides) break;
                    int t = i * 6;
                    triangles[t] = i * 2;
                    triangles[t + 1] = i * 2 + 1;
                    triangles[t + 2] = i * 2 + 2;
                    triangles[t + 3] = i * 2 + 2;
                    triangles[t + 4] = i * 2 + 1;
                    triangles[t + 5] = i * 2 + 3;
                }
                cylinder = new Mesh { name = "LightPillarCylinder", vertices = vertices, normals = normals, uv = uvs, triangles = triangles };
                cylinder.RecalculateBounds();
                return cylinder;
            }
        }
    }
}
