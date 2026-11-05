using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Core
{
    /// 城のまわりの渦雲。1枚のメッシュにかたまりを詰め、動きはシェーダー（Game/VFX/StormCloud）に任せる。
    public class StormClouds : MonoBehaviour
    {
        private const int Seed = 4217;
        private static readonly int MaxRadiusId = Shader.PropertyToID("_MaxRadius");
        private static readonly int HeightId = Shader.PropertyToID("_Height");
        private static readonly int SpinId = Shader.PropertyToID("_Spin");
        private static readonly int InflowId = Shader.PropertyToID("_InflowSeconds");

        private Mesh mesh;
        private Material material;

        public static StormClouds Create(Transform parent, float maxRadius)
        {
            var shader = Resources.Load<Shader>("VFX/StormCloud");
            if (shader == null)
            {
                Debug.LogWarning("[StormClouds] shader not found");
                return null;
            }
            var go = new GameObject("StormClouds");
            go.transform.SetParent(parent, false);
            var clouds = go.AddComponent<StormClouds>();
            clouds.Build(shader, maxRadius);
            return clouds;
        }

        private void Build(Shader shader, float maxRadius)
        {
            var config = BalanceConfig.Instance;
            mesh = BuildMesh(config.stormCloudLumps, config.stormArmCount, config.stormArmWidth, config.stormScatter, config.stormCloudStretch);
            material = new Material(shader);
            material.SetFloat(MaxRadiusId, maxRadius);
            material.SetFloat(HeightId, config.stormCloudHeight);
            material.SetFloat(SpinId, config.stormSpinSpeed);
            material.SetFloat(InflowId, config.stormInflowSeconds);

            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
            if (material != null) Destroy(material);
        }

        // 12頂点の正二十面体を、底が平らで流れの向き（z）に長い形にゆがめ、らせんの腕に沿って並べる。
        private static Mesh BuildMesh(int lumps, int arms, float armWidth, float scatter, float stretch)
        {
            var rng = new System.Random(Seed);
            var baseVerts = IcosahedronVertices();
            var baseTris = IcosahedronTriangles;
            int vertexCount = lumps * baseVerts.Length;
            var positions = new Vector3[vertexCount];
            var flow = new Vector2[vertexCount];
            var shape = new Vector2[vertexCount];
            var colors = new Color32[vertexCount];
            var indices = new int[lumps * baseTris.Length];

            int v = 0, t = 0;
            for (int i = 0; i < lumps; i++)
            {
                float u = (float)rng.NextDouble();
                float angle = (i % Mathf.Max(arms, 1)) * Mathf.PI * 2f / Mathf.Max(arms, 1);
                // 腕の中心ほど暗くして、密集しても渦が読めるようにする。
                float armShade = 1f;
                if (rng.NextDouble() < scatter) angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                else
                {
                    float offset = Gaussian(rng);
                    angle += offset * armWidth;
                    armShade = Mathf.Lerp(0.62f, 1f, Mathf.Abs(offset) / 1.6f);
                }
                float height = (float)rng.NextDouble() * 2f - 1f;
                float scale = 0.6f + (float)rng.NextDouble() * 0.9f;
                byte shade = (byte)(armShade * (205 + rng.Next(51)));
                float squash = 0.3f + (float)rng.NextDouble() * 0.2f;

                for (int k = 0; k < baseTris.Length; k++) indices[t++] = v + baseTris[k];
                foreach (var b in baseVerts)
                {
                    float jitter = 0.8f + (float)rng.NextDouble() * 0.45f;
                    float y = b.y * jitter * squash;
                    if (y < 0f) y *= 0.7f;
                    positions[v] = new Vector3(b.x * jitter, y, b.z * jitter * stretch);
                    flow[v] = new Vector2(u, angle);
                    shape[v] = new Vector2(height, scale);
                    colors[v] = new Color32(shade, shade, shade, 255);
                    v++;
                }
            }

            var result = new Mesh { name = "StormClouds", indexFormat = IndexFormat.UInt32 };
            result.vertices = positions;
            result.uv = flow;
            result.uv2 = shape;
            result.colors32 = colors;
            result.triangles = indices;
            // 実際の位置はシェーダーで決まるので、視錐台で消えないよう十分大きくしておく。
            result.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            result.UploadMeshData(true);
            return result;
        }

        private static float Gaussian(System.Random rng)
        {
            double a = 1.0 - rng.NextDouble();
            double b = rng.NextDouble();
            return (float)(System.Math.Sqrt(-2.0 * System.Math.Log(a)) * System.Math.Cos(2.0 * System.Math.PI * b));
        }

        private static Vector3[] IcosahedronVertices()
        {
            float g = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var verts = new[]
            {
                new Vector3(-1, g, 0), new Vector3(1, g, 0), new Vector3(-1, -g, 0), new Vector3(1, -g, 0),
                new Vector3(0, -1, g), new Vector3(0, 1, g), new Vector3(0, -1, -g), new Vector3(0, 1, -g),
                new Vector3(g, 0, -1), new Vector3(g, 0, 1), new Vector3(-g, 0, -1), new Vector3(-g, 0, 1),
            };
            for (int i = 0; i < verts.Length; i++) verts[i] = verts[i].normalized;
            return verts;
        }

        private static readonly int[] IcosahedronTriangles =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
            1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
            4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        };
    }
}
