using System.Collections.Generic;
using UnityEngine;

namespace Game.Skill
{
    /// 六角形のセルを敷き詰めた魔法陣。防いだときは波紋が走って薄れ、破られたときはセルがばらばらに飛び散る。
    /// セルの動きはシェーダーで付ける。uv は (縁までの近さ, セルごとの乱数)、uv2 はセルの中心。
    public class HexWard : MonoBehaviour
    {
        private const float CellRadius = 0.17f;
        private const float CellGap = 0.9f;
        private const float BoundsSize = 8f;
        private const float PopSeconds = 0.06f;
        private const float BreakDelay = 0.05f;   // 割れる前に一瞬だけ全体が光る
        private const float BlockHoldRatio = 0.25f;
        private const float RuneScale = 2.25f;
        private const float RuneFadeRatio = 0.3f;

        private static readonly Color Blue = new Color(0.45f, 1.3f, 1.7f, 1f);
        private static readonly Color BrokenBlue = new Color(0.8f, 1.5f, 2f, 1f);
        private static Mesh sharedMesh;

        private MeshRenderer cells;
        private MeshRenderer rune;
        private MaterialPropertyBlock block;
        private MaterialPropertyBlock runeBlock;
        private bool shatter;
        private float radius;
        private float seconds;
        private float opacity;
        private float timer;

        /// opacity は全体の濃さ。自分の視界をふさがないよう、本人の画面では薄くする。
        public static void SpawnBlock(Vector3 position, Quaternion rotation, float radius, float seconds, float opacity)
        {
            Spawn(position, rotation, radius, seconds, opacity, false);
        }

        public static void SpawnShatter(Vector3 position, Quaternion rotation, float radius, float seconds, float opacity)
        {
            Spawn(position, rotation, radius, seconds, opacity, true);
            FireballVfx.SpawnSparks(position, rotation * Vector3.back, 26, Color.white, new Color(0.45f, 1f, 1.5f));
        }

        private static void Spawn(Vector3 position, Quaternion rotation, float radius, float seconds, float opacity, bool shatter)
        {
            var go = new GameObject(shatter ? "HexWardBroken" : "HexWard");
            go.transform.SetPositionAndRotation(position, rotation);
            var ward = go.AddComponent<HexWard>();
            ward.shatter = shatter;
            ward.radius = radius;
            ward.seconds = seconds;
            ward.opacity = opacity;
            ward.block = new MaterialPropertyBlock();
            ward.runeBlock = new MaterialPropertyBlock();
            ward.cells = FireballVfx.NewMeshObject("Cells", go.transform, GetMesh(), FireballVfx.GetMaterial("VFX/HexWard"));
            ward.rune = FireballVfx.NewMeshObject("Rune", go.transform, FireballVfx.QuadMesh, FireballVfx.GetMaterial("VFX/RuneCircle"));
            ward.rune.transform.localScale = new Vector3(RuneScale, RuneScale, 1f);
            ward.Apply();
        }

        private void Update()
        {
            timer += Time.deltaTime;
            if (timer >= seconds)
            {
                Destroy(gameObject);
                return;
            }
            Apply();
        }

        private void Apply()
        {
            float k = timer / seconds;
            float pop = Mathf.Clamp01(timer / PopSeconds);
            transform.localScale = Vector3.one * (radius * Mathf.Lerp(0.8f, 1f, pop));

            float fade = shatter ? 1f - Mathf.Pow(k, 4f) : 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(BlockHoldRatio, 1f, k));
            float broken = shatter ? Mathf.Clamp01((timer - BreakDelay) / (seconds - BreakDelay)) : 0f;
            block.SetColor("_Color", shatter ? BrokenBlue : Blue);
            block.SetFloat("_Fade", fade * opacity);
            block.SetFloat("_Shatter", broken);
            block.SetFloat("_Ripple", k * 2.6f);
            block.SetFloat("_Flash", (shatter ? 1f - Mathf.Clamp01(timer / (BreakDelay * 2f)) : 1f - pop) * opacity);
            cells.SetPropertyBlock(block);

            // 外周の魔法陣は、破られたときは最初に消える。
            float runeAlpha = shatter ? 1f - Mathf.Clamp01(k / RuneFadeRatio) : fade;
            Color runeColor = Blue;
            runeColor.a = runeAlpha * opacity;
            runeBlock.SetColor("_Color", runeColor);
            runeBlock.SetFloat("_Fill", 0f);
            rune.SetPropertyBlock(runeBlock);
        }

        private static Mesh GetMesh()
        {
            if (sharedMesh == null) sharedMesh = BuildMesh();
            return sharedMesh;
        }

        // 半径1の円の中に、頂点が上下を向く六角形を蜂の巣状に並べる。
        private static Mesh BuildMesh()
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var centers = new List<Vector2>();
            var triangles = new List<int>();
            var random = new System.Random(7);

            float stepX = CellRadius * Mathf.Sqrt(3f);
            float stepY = CellRadius * 1.5f;
            int reach = Mathf.CeilToInt(1f / stepY) + 1;
            for (int row = -reach; row <= reach; row++)
            {
                for (int col = -reach; col <= reach; col++)
                {
                    var center = new Vector2((col + (row & 1) * 0.5f) * stepX, row * stepY);
                    if (center.magnitude + CellRadius * 0.5f > 1f) continue;
                    AddCell(center, (float)random.NextDouble(), vertices, uvs, centers, triangles);
                }
            }

            var mesh = new Mesh { name = "HexWard" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetUVs(1, centers);
            mesh.SetTriangles(triangles, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * BoundsSize);
            return mesh;
        }

        private static void AddCell(Vector2 center, float seed, List<Vector3> vertices, List<Vector2> uvs, List<Vector2> centers, List<int> triangles)
        {
            int first = vertices.Count;
            vertices.Add(center);
            uvs.Add(new Vector2(0f, seed));
            centers.Add(center);
            for (int i = 0; i < 6; i++)
            {
                float angle = (i * 60f + 30f) * Mathf.Deg2Rad;
                vertices.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (CellRadius * CellGap));
                uvs.Add(new Vector2(1f, seed));
                centers.Add(center);
                triangles.Add(first);
                triangles.Add(first + 1 + i);
                triangles.Add(first + 1 + (i + 1) % 6);
            }
        }
    }
}
