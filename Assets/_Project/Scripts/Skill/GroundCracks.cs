using UnityEngine;

namespace Game.Skill
{
    /// 地面に走るヒビ。中心から外へ一気に広がり、はじめは赤熱していて、しばらく残ったあと薄れて消える。
    public class GroundCracks : MonoBehaviour
    {
        private const float RevealSeconds = 0.14f;
        private const float HeatSeconds = 0.9f;
        private const float FadeSeconds = 1.6f;
        private const float GroundLift = 0.05f;
        private const int CracksPerRadius = 3;
        private const int SegmentsPerCrack = 7;
        private const float RootWidth = 0.34f;
        private const float BranchChance = 0.55f;

        private Mesh mesh;
        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock block;
        private float holdSeconds;
        private float timer;

        public static void Spawn(Vector3 center, float radius, float holdSeconds)
        {
            Vector3 up = Vector3.up;
            if (Physics.Raycast(center + Vector3.up, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore))
            {
                center = hit.point;
                up = hit.normal;
            }
            var go = new GameObject("GroundCracks");
            go.transform.SetPositionAndRotation(center + up * GroundLift, Quaternion.FromToRotation(Vector3.up, up));
            var cracks = go.AddComponent<GroundCracks>();
            cracks.holdSeconds = holdSeconds;
            cracks.Build(radius);
        }

        private void Build(float radius)
        {
            var vertices = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var triangles = new System.Collections.Generic.List<int>();

            int count = Mathf.Max(5, Mathf.RoundToInt(radius * CracksPerRadius));
            for (int i = 0; i < count; i++)
            {
                float angle = (i + Random.Range(-0.3f, 0.3f)) / count * 360f;
                float length = radius * Random.Range(0.65f, 1f);
                AddCrack(vertices, uvs, triangles, Vector3.zero, angle, length, RootWidth, 0f, Random.value < BranchChance);
            }

            mesh = new Mesh { name = "GroundCracks" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = FireballVfx.GetMaterial("VFX/GroundCrack");
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            block = new MaterialPropertyBlock();
            Apply(0f, 1f, 1f);
        }

        // 折れ線に沿った細い帯。uv.x は中心からの距離の割合（0〜1）で、広がる演出に使う。
        private static void AddCrack(System.Collections.Generic.List<Vector3> vertices, System.Collections.Generic.List<Vector2> uvs, System.Collections.Generic.List<int> triangles, Vector3 start, float angle, float length, float width, float startReveal, bool branch)
        {
            float segment = length / SegmentsPerCrack;
            float heading = angle;
            Vector3 point = start;
            int branchAt = branch ? Random.Range(2, SegmentsPerCrack - 1) : -1;
            for (int i = 0; i <= SegmentsPerCrack; i++)
            {
                float along = (float)i / SegmentsPerCrack;
                Vector3 forward = Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
                Vector3 side = Quaternion.Euler(0f, 90f, 0f) * forward;
                float half = width * 0.5f * (1f - along);
                float reveal = Mathf.Lerp(startReveal, 1f, along);
                vertices.Add(point - side * half);
                vertices.Add(point + side * half);
                uvs.Add(new Vector2(reveal, 0f));
                uvs.Add(new Vector2(reveal, 1f));
                if (i > 0)
                {
                    int v = vertices.Count - 4;
                    triangles.Add(v); triangles.Add(v + 2); triangles.Add(v + 1);
                    triangles.Add(v + 1); triangles.Add(v + 2); triangles.Add(v + 3);
                }
                if (i == branchAt)
                {
                    float turn = Random.value < 0.5f ? -1f : 1f;
                    AddCrack(vertices, uvs, triangles, point, heading + turn * Random.Range(28f, 55f), length * (1f - along) * 0.6f, width * (1f - along) * 0.7f, reveal, false);
                }
                point += forward * (segment * Random.Range(0.8f, 1.2f));
                heading += Random.Range(-32f, 32f);
            }
        }

        private void Update()
        {
            timer += Time.deltaTime;
            float reveal = Mathf.Clamp01(timer / RevealSeconds);
            float heat = 1f - Mathf.Clamp01(timer / HeatSeconds);
            float fade = 1f - Mathf.Clamp01((timer - holdSeconds) / FadeSeconds);
            Apply(reveal, heat * heat, fade);
            if (fade <= 0f) Destroy(gameObject);
        }

        private void Apply(float reveal, float heat, float fade)
        {
            block.SetFloat("_Reveal", reveal);
            block.SetFloat("_Heat", heat);
            block.SetFloat("_Fade", fade);
            meshRenderer.SetPropertyBlock(block);
        }

        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }
    }
}
