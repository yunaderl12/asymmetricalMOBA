using UnityEngine;

namespace Game.Skill
{
    /// 三日月形に走る斬撃。円弧のリボンを先端から描き、尾が追いかけて消える。
    /// 平らな面で作るので、rotation の +Z が弧の中央、+Y が面の法線になる。
    public class SwordArc : MonoBehaviour
    {
        private const int SegmentsPer90 = 12;

        private MeshRenderer ribbon;
        private Mesh mesh;
        private MaterialPropertyBlock block;
        private Color color;
        private Color edge;
        private float sweepSeconds;
        private float tailSeconds;
        private float timer;

        public static SwordArc Spawn(Vector3 center, Quaternion rotation, float radius, float width, float arcDegrees, float sweepSeconds, float tailSeconds, bool clockwise, Color color, Color edge, float lift = 0f)
        {
            var go = new GameObject("SwordArc");
            go.transform.SetPositionAndRotation(center, rotation);
            var arc = go.AddComponent<SwordArc>();
            arc.mesh = BuildMesh(radius, width, arcDegrees, clockwise, lift);
            arc.ribbon = FireballVfx.NewMeshObject("Ribbon", go.transform, arc.mesh, FireballVfx.GetMaterial("VFX/SwordArc"));
            arc.color = color;
            arc.edge = edge;
            arc.sweepSeconds = sweepSeconds;
            arc.tailSeconds = tailSeconds;
            arc.block = new MaterialPropertyBlock();
            arc.Apply();
            return arc;
        }

        // lift: 外縁を面の法線方向へ持ち上げる量。持ち上げると輪がすり鉢状になり、低い視点からも線にならない。
        private static Mesh BuildMesh(float radius, float width, float arcDegrees, bool clockwise, float lift)
        {
            int segments = Mathf.Max(4, Mathf.CeilToInt(arcDegrees / 90f * SegmentsPer90));
            var vertices = new Vector3[(segments + 1) * 2];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[segments * 6];
            float inner = Mathf.Max(0.01f, radius - width);
            for (int i = 0; i <= segments; i++)
            {
                float u = (float)i / segments;
                float degrees = (u - 0.5f) * arcDegrees * (clockwise ? 1f : -1f);
                float radians = degrees * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
                vertices[i * 2] = dir * inner;
                vertices[i * 2 + 1] = dir * radius + Vector3.up * lift;
                uvs[i * 2] = new Vector2(u, 0f);
                uvs[i * 2 + 1] = new Vector2(u, 1f);
                if (i == segments) break;
                int t = i * 6;
                triangles[t] = i * 2;
                triangles[t + 1] = i * 2 + 1;
                triangles[t + 2] = i * 2 + 2;
                triangles[t + 3] = i * 2 + 1;
                triangles[t + 4] = i * 2 + 3;
                triangles[t + 5] = i * 2 + 2;
            }
            var mesh = new Mesh { name = "SwordArc" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private void Update()
        {
            timer += Time.deltaTime;
            if (timer >= sweepSeconds + tailSeconds)
            {
                Destroy(mesh);
                Destroy(gameObject);
                return;
            }
            Apply();
        }

        private void Apply()
        {
            float head = 1f - Mathf.Pow(1f - Mathf.Clamp01(timer / sweepSeconds), 2f);
            float tailT = Mathf.Clamp01(timer / (sweepSeconds + tailSeconds));
            block.SetFloat("_Head", head);
            block.SetFloat("_Tail", tailT * tailT);
            block.SetColor("_Color", color);
            block.SetColor("_EdgeColor", edge);
            ribbon.SetPropertyBlock(block);
        }
    }
}
