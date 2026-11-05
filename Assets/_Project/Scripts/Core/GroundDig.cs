using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// 地面を削る仕組み。地面を格子の頂点で持ち、頂点ごとの深さ（段数）を更新する。
    /// 頂点を少しずらして三角形でつなぐので、ブロックではなく角ばった面が削れて見える。周りの低ポリと同じ、折り紙のような質感。
    /// 削れた場所は DiggableLit で元の地面と草を切り抜き、代わりにこのメッシュと当たり判定を置く。
    /// 結果は Dig の引数だけで決まるので、ネットワークでは「中心・半径・深さ」を送れば同じ穴になる。
    public class GroundDig : MonoBehaviour
    {
        private const string GroundName = "Ground";
        private const string GroundRendererName = "Field_Ground";
        private const string DecorRendererName = "Field_Decor";
        private const string DecorKeyword = "_DIG_DECOR";
        private const string DirtMaterialName = "LP_Dirt";
        private const string MaskTextureName = "_DigMaskTex";
        private const string MaskRectName = "_DigMaskRect";
        private const string DiggableShaderPath = "VFX/DiggableLit";
        private const string SpawnerRoot = "Spawners";

        private const int ChunkCells = 16;
        private const float FlatColliderThickness = 1f;
        private const int OutlineSpokes = 9;           // 穴の輪郭を作る多角形の角の数
        private const float OutlineWobble = 0.18f;     // 輪郭のゆがみ（半径に対する割合）
        private const float VertexNoise = 0.1f;        // 段の境目のばらつき（半径に対する割合）
        private const float VertexShift = 0.3f;        // 頂点を横にずらす量（セルに対する割合）
        private const float WideRimChance = 0.35f;     // 土の縁がもう1セル広がる確率
        private const int MaxRimCells = 2;
        private const float RimBaseHeight = 0.02f;     // 土の縁を元の地面より上に出す高さ (m)
        private const float RimLift = 0.1f;            // 土の縁の盛り上がり (m)

        private const byte MaskGround = 255;           // 地面も草も切り抜く
        private const byte MaskDecor = 128;            // 草だけ切り抜く（土の縁の下）

        private static readonly string[] BlockerRoots = { "Rocks", "Walls", "StartTown", "DemonCastle", "ArcherTowers", "LowPolyVisuals" };

        private static GroundDig instance;

        public static bool HasHoles => instance != null;

        private float cellSize;
        private float stepDepth;
        private int maxSteps;
        private Vector2 origin;
        private int cellsX;
        private int cellsZ;
        private int vertsX;
        private int vertsZ;
        private byte[] steps;     // 頂点ごとの深さ（段数）
        private bool[] rim;       // 土がむき出しの頂点（穴のすぐ外側）
        private bool[] blocked;   // 削れない頂点
        private Texture2D mask;
        private Material earth;
        private readonly List<Material> clipMaterials = new List<Material>();
        private BoxCollider[] flatColliders;
        private Chunk[] chunks;
        private int chunksX;

        private class Chunk
        {
            public GameObject Root;
            public MeshFilter Filter;
            public MeshCollider Collider;
            public Mesh Visual;
            public Mesh Solid;
        }

        /// center を中心に、半径 radius (m)、中心の深さ depth (m) で削る。少しでも削れたら true。
        /// 削れない場所（町・城・スポナー・木の周りなど）や、削る地面がないシーンでは何も起きない。
        public static bool Dig(Vector3 center, float radius, float depth)
        {
            if (radius <= 0f || depth <= 0f) return false;
            if (instance == null && !TryCreate()) return false;
            return instance.DigInternal(center, radius, depth);
        }

        /// その地点の地面が元の高さからどれだけ下がっているか (m)。
        public static float DepthAt(Vector3 position)
        {
            return instance != null ? instance.DepthAtInternal(position) : 0f;
        }

        // 削られるまでは何も変えないので、一度も削らない試合には負荷がかからない。
        private static bool TryCreate()
        {
            var ground = GameObject.Find(GroundName);
            var collider = ground != null ? ground.GetComponent<Collider>() : null;
            if (collider == null) return false;
            var go = new GameObject("GroundDig");
            instance = go.AddComponent<GroundDig>();
            instance.Build(collider);
            return true;
        }

        private void Build(Collider ground)
        {
            var config = BalanceConfig.Instance;
            cellSize = config.digCellSize;
            stepDepth = config.digStepDepth;
            maxSteps = Mathf.RoundToInt(config.digMaxDepth / stepDepth);

            Bounds bounds = ground.bounds;
            origin = new Vector2(bounds.min.x, bounds.min.z);
            cellsX = Mathf.CeilToInt(bounds.size.x / cellSize);
            cellsZ = Mathf.CeilToInt(bounds.size.z / cellSize);
            vertsX = cellsX + 1;
            vertsZ = cellsZ + 1;
            steps = new byte[vertsX * vertsZ];
            rim = new bool[vertsX * vertsZ];
            blocked = new bool[vertsX * vertsZ];
            MarkBlocked(config);

            mask = new Texture2D(cellsX, cellsZ, TextureFormat.R8, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "DigMask" };
            mask.SetPixelData(new byte[cellsX * cellsZ], 0);
            mask.Apply();
            Shader.SetGlobalTexture(MaskTextureName, mask);
            Shader.SetGlobalVector(MaskRectName, new Vector4(origin.x, origin.y, 1f / (cellsX * cellSize), 1f / (cellsZ * cellSize)));

            earth = EarthPalette.CreateMaterial(FindDirtColor());
            SwapGroundMaterials();
            BuildFlatColliders(ground, bounds.max.y);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            if (mask != null) Destroy(mask);
            if (earth != null) Destroy(earth);
            foreach (var material in clipMaterials) if (material != null) Destroy(material);
            if (chunks == null) return;
            foreach (var chunk in chunks)
            {
                if (chunk == null) continue;
                Destroy(chunk.Visual);
                Destroy(chunk.Solid);
            }
        }

        // 岩・壁・建物・塔・スポナーはコライダーの範囲、木はベイクした位置で判定する。
        private void MarkBlocked(BalanceConfig config)
        {
            foreach (string root in BlockerRoots) BlockCollidersUnder(root, config.digKeepOutMargin);
            BlockCollidersUnder(SpawnerRoot, config.digSpawnerMargin);

            var data = Resources.Load<GroundDigData>(WorldCatalog.ForActiveScene().digResource);
            if (data != null)
            {
                foreach (var tree in data.treePositions)
                {
                    float r = config.digTreeMargin;
                    BlockArea(tree.x - r, tree.y - r, tree.x + r, tree.y + r);
                }
                for (int z = 0; z < vertsZ; z++)
                    for (int x = 0; x < vertsX; x++)
                        if (data.IsBlocked(origin.x + x * cellSize, origin.y + z * cellSize)) blocked[x + z * vertsX] = true;
            }
            else Debug.LogWarning("[GroundDig] Resources/GroundDigData not found; trees are not protected.");

            // フィールドの外周も削らない（穴の側面が外から見えないように）。
            int edge = Mathf.CeilToInt(config.digEdgeMargin / cellSize);
            for (int z = 0; z < vertsZ; z++)
                for (int x = 0; x < vertsX; x++)
                    if (x < edge || z < edge || x >= vertsX - edge || z >= vertsZ - edge) blocked[x + z * vertsX] = true;
        }

        private void BlockCollidersUnder(string rootName, float margin)
        {
            var root = GameObject.Find(rootName);
            if (root == null) return;
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            {
                // 山・橋・見えない壁はフィールド全体に広がるので、GroundDigData の範囲で判定する。
                if (collider.name.StartsWith("Field_")) continue;
                Bounds b = collider.bounds;
                BlockArea(b.min.x - margin, b.min.z - margin, b.max.x + margin, b.max.z + margin);
            }
        }

        private void BlockArea(float minX, float minZ, float maxX, float maxZ)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt((minX - origin.x) / cellSize), 0, vertsX - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((maxX - origin.x) / cellSize), 0, vertsX - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((minZ - origin.y) / cellSize), 0, vertsZ - 1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((maxZ - origin.y) / cellSize), 0, vertsZ - 1);
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                    blocked[x + z * vertsX] = true;
        }

        // 穴の色をフィールドの土の道に合わせる。
        private static Color FindDirtColor()
        {
            var go = GameObject.Find(GroundRendererName);
            var renderer = go != null ? go.GetComponent<Renderer>() : null;
            if (renderer != null)
                foreach (var material in renderer.sharedMaterials)
                    if (material != null && material.name == DirtMaterialName && material.HasProperty("_BaseColor")) return material.GetColor("_BaseColor");
            return EarthPalette.FallbackDirt;
        }

        private void SwapGroundMaterials()
        {
            var shader = Resources.Load<Shader>(DiggableShaderPath);
            if (shader == null)
            {
                Debug.LogWarning("[GroundDig] Resources/" + DiggableShaderPath + " not found");
                return;
            }
            SwapMaterials(GroundRendererName, shader, false);
            SwapMaterials(DecorRendererName, shader, true);
        }

        private void SwapMaterials(string rendererName, Shader shader, bool decor)
        {
            var go = GameObject.Find(rendererName);
            var renderer = go != null ? go.GetComponent<Renderer>() : null;
            if (renderer == null)
            {
                Debug.LogWarning("[GroundDig] renderer not found: " + rendererName);
                return;
            }
            var source = renderer.sharedMaterials;
            var swapped = new Material[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                swapped[i] = new Material(shader);
                swapped[i].CopyPropertiesFromMaterial(source[i]);
                swapped[i].shaderKeywords = source[i].shaderKeywords;
                if (decor) swapped[i].EnableKeyword(DecorKeyword);
                clipMaterials.Add(swapped[i]);
            }
            renderer.sharedMaterials = swapped;
        }

        // 1枚の地面コライダーをチャンクごとの箱に分ける。削られたチャンクだけ箱を外してメッシュに替える。
        private void BuildFlatColliders(Collider ground, float top)
        {
            ground.enabled = false;
            chunksX = Mathf.CeilToInt((float)cellsX / ChunkCells);
            int chunksZ = Mathf.CeilToInt((float)cellsZ / ChunkCells);
            chunks = new Chunk[chunksX * chunksZ];
            flatColliders = new BoxCollider[chunks.Length];
            var holder = new GameObject("FlatGround");
            holder.layer = ground.gameObject.layer;
            holder.transform.SetParent(transform, false);
            float size = ChunkCells * cellSize;
            for (int z = 0; z < chunksZ; z++)
            {
                for (int x = 0; x < chunksX; x++)
                {
                    var box = holder.AddComponent<BoxCollider>();
                    box.center = new Vector3(origin.x + (x + 0.5f) * size, top - FlatColliderThickness * 0.5f, origin.y + (z + 0.5f) * size);
                    box.size = new Vector3(size, FlatColliderThickness, size);
                    flatColliders[x + z * chunksX] = box;
                }
            }
        }

        private float DepthAtInternal(Vector3 position)
        {
            float fx = (position.x - origin.x) / cellSize;
            float fz = (position.z - origin.y) / cellSize;
            int x = Mathf.FloorToInt(fx), z = Mathf.FloorToInt(fz);
            if (x < 0 || z < 0 || x >= cellsX || z >= cellsZ) return 0f;
            int i = x + z * vertsX;
            float near = Mathf.Lerp(steps[i], steps[i + 1], fx - x);
            float far = Mathf.Lerp(steps[i + vertsX], steps[i + vertsX + 1], fx - x);
            return Mathf.Lerp(near, far, fz - z) * stepDepth;
        }

        private bool DigInternal(Vector3 center, float radius, float depth)
        {
            int addSteps = Mathf.Clamp(Mathf.RoundToInt(depth / stepDepth), 1, maxSteps);
            int reach = Mathf.CeilToInt(radius * (1f + OutlineWobble) / cellSize) + MaxRimCells + addSteps + 2;
            int cx = Mathf.FloorToInt((center.x - origin.x) / cellSize);
            int cz = Mathf.FloorToInt((center.z - origin.y) / cellSize);
            int x0 = Mathf.Max(0, cx - reach), x1 = Mathf.Min(vertsX - 1, cx + reach);
            int z0 = Mathf.Max(0, cz - reach), z1 = Mathf.Min(vertsZ - 1, cz + reach);
            if (x0 > x1 || z0 > z1) return false;

            bool changed = false;
            for (int z = z0; z <= z1; z++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    int i = x + z * vertsX;
                    if (blocked[i]) continue;
                    float wx = origin.x + x * cellSize - center.x;
                    float wz = origin.y + z * cellSize - center.z;
                    float outline = radius * OutlineFactor(wx, wz, cx, cz);
                    float distance = Mathf.Sqrt(wx * wx + wz * wz) / outline + (Hash01(x, z) - 0.5f) * VertexNoise;
                    if (distance >= 1f) continue;
                    int next = Mathf.Min(maxSteps, steps[i] + Mathf.CeilToInt(addSteps * (1f - distance)));
                    if (next == steps[i]) continue;
                    steps[i] = (byte)next;
                    changed = true;
                }
            }
            if (!changed) return false;

            RemoveSpikes(x0, z0, x1, z1);
            LimitStepHeight(x0, z0, x1, z1);
            MarkRim(x0, z0, x1, z1);
            UpdateMask(x0, z0, x1, z1);
            RebuildChunks(x0, z0, x1, z1);
            return true;
        }

        // 穴の輪郭を、円ではなくいびつな多角形にする（center のセルで形が決まる）。
        private static float OutlineFactor(float wx, float wz, int cx, int cz)
        {
            float t = (Mathf.Atan2(wz, wx) + Mathf.PI) / (2f * Mathf.PI) * OutlineSpokes;
            int a = Mathf.FloorToInt(t);
            float h0 = Hash01(cx * 13 + a % OutlineSpokes, cz * 29 - a % OutlineSpokes);
            float h1 = Hash01(cx * 13 + (a + 1) % OutlineSpokes, cz * 29 - (a + 1) % OutlineSpokes);
            return 1f + (Mathf.Lerp(h0, h1, t - a) - 0.5f) * 2f * OutlineWobble;
        }

        // 1頂点だけ飛び出たり凹んだりしている所をならす。
        private void RemoveSpikes(int x0, int z0, int x1, int z1)
        {
            for (int z = Mathf.Max(1, z0); z <= Mathf.Min(vertsZ - 2, z1); z++)
            {
                for (int x = Mathf.Max(1, x0); x <= Mathf.Min(vertsX - 2, x1); x++)
                {
                    int i = x + z * vertsX;
                    if (blocked[i]) continue;
                    int a = steps[i - 1], b = steps[i + 1], c = steps[i - vertsX], d = steps[i + vertsX];
                    int low = Mathf.Min(a, Mathf.Min(b, Mathf.Min(c, d)));
                    int high = Mathf.Max(a, Mathf.Max(b, Mathf.Max(c, d)));
                    steps[i] = (byte)Mathf.Clamp(steps[i], low, high);
                }
            }
        }

        // 隣の頂点との差を1段までにする（どこからでも歩いて出られる）。削れない場所が 0 段として効くので、そこへ向かって坂になる。
        private void LimitStepHeight(int x0, int z0, int x1, int z1)
        {
            for (int z = z0; z <= z1; z++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    int i = x + z * vertsX;
                    int limit = steps[i];
                    if (x > 0) limit = Mathf.Min(limit, steps[i - 1] + 1);
                    if (z > 0) limit = Mathf.Min(limit, steps[i - vertsX] + 1);
                    steps[i] = (byte)limit;
                }
            }
            for (int z = z1; z >= z0; z--)
            {
                for (int x = x1; x >= x0; x--)
                {
                    int i = x + z * vertsX;
                    int limit = steps[i];
                    if (x < vertsX - 1) limit = Mathf.Min(limit, steps[i + 1] + 1);
                    if (z < vertsZ - 1) limit = Mathf.Min(limit, steps[i + vertsX] + 1);
                    steps[i] = (byte)limit;
                }
            }
        }

        // 穴のすぐ外も草がはがれて土が見える。削れない場所の上にも少しかかる（土の縁は当たり判定を変えない）。
        private void MarkRim(int x0, int z0, int x1, int z1)
        {
            for (int z = z0; z <= z1; z++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    int i = x + z * vertsX;
                    if (steps[i] > 0 || rim[i]) continue;
                    int cells = Hash01(z, x) < WideRimChance ? MaxRimCells : 1;
                    rim[i] = HasHoleWithin(x, z, cells);
                }
            }
        }

        private bool HasHoleWithin(int x, int z, int reach)
        {
            for (int dz = -reach; dz <= reach; dz++)
            {
                for (int dx = -reach; dx <= reach; dx++)
                {
                    int nx = x + dx, nz = z + dz;
                    if (nx >= 0 && nz >= 0 && nx < vertsX && nz < vertsZ && steps[nx + nz * vertsX] > 0) return true;
                }
            }
            return false;
        }

        private bool IsOpen(int vertex)
        {
            return steps[vertex] > 0 || rim[vertex];
        }

        private bool TouchesHole(int x, int z)
        {
            int i = x + z * vertsX;
            return steps[i] > 0 || steps[i + 1] > 0 || steps[i + vertsX] > 0 || steps[i + vertsX + 1] > 0;
        }

        private bool TouchesOpen(int x, int z)
        {
            int i = x + z * vertsX;
            return IsOpen(i) || IsOpen(i + 1) || IsOpen(i + vertsX) || IsOpen(i + vertsX + 1);
        }

        private void UpdateMask(int x0, int z0, int x1, int z1)
        {
            var data = mask.GetRawTextureData<byte>();
            for (int z = Mathf.Max(0, z0 - 1); z <= Mathf.Min(cellsZ - 1, z1); z++)
                for (int x = Mathf.Max(0, x0 - 1); x <= Mathf.Min(cellsX - 1, x1); x++)
                    data[x + z * cellsX] = TouchesHole(x, z) ? MaskGround : TouchesOpen(x, z) ? MaskDecor : (byte)0;
            mask.Apply();
        }

        private void RebuildChunks(int x0, int z0, int x1, int z1)
        {
            int firstZ = Mathf.Max(0, z0 - 1) / ChunkCells, lastZ = Mathf.Min(cellsZ - 1, z1) / ChunkCells;
            int firstX = Mathf.Max(0, x0 - 1) / ChunkCells, lastX = Mathf.Min(cellsX - 1, x1) / ChunkCells;
            for (int cz = firstZ; cz <= lastZ; cz++)
                for (int cx = firstX; cx <= lastX; cx++)
                    RebuildChunk(cx, cz, cx + cz * chunksX);
        }

        private void RebuildChunk(int cx, int cz, int index)
        {
            var visual = new ChunkMeshBuilder();
            var solid = new ChunkMeshBuilder();
            int startX = cx * ChunkCells, startZ = cz * ChunkCells;
            int endX = Mathf.Min(cellsX, startX + ChunkCells), endZ = Mathf.Min(cellsZ, startZ + ChunkCells);
            bool any = false;
            for (int z = startZ; z < endZ; z++)
            {
                for (int x = startX; x < endX; x++)
                {
                    // 対角線の向きをセルごとに変えて、格子に見えないようにする。
                    bool flip = Hash01(x * 5 + 3, z * 11 + 7) < 0.5f;
                    int bl = x + z * vertsX, br = bl + 1, tl = bl + vertsX, tr = tl + 1;
                    if (flip)
                    {
                        any |= AddTriangle(visual, solid, x, z, 0, bl, tl, tr);
                        any |= AddTriangle(visual, solid, x, z, 1, bl, tr, br);
                    }
                    else
                    {
                        any |= AddTriangle(visual, solid, x, z, 0, bl, tl, br);
                        any |= AddTriangle(visual, solid, x, z, 1, tl, tr, br);
                    }
                }
            }

            var chunk = chunks[index];
            if (!any)
            {
                if (chunk != null) chunk.Root.SetActive(false);
                flatColliders[index].enabled = true;
                return;
            }
            if (chunk == null) chunk = chunks[index] = NewChunk();
            chunk.Root.SetActive(true);
            visual.Fill(chunk.Visual);
            solid.Fill(chunk.Solid);
            chunk.Filter.sharedMesh = chunk.Visual;
            chunk.Collider.sharedMesh = null;
            chunk.Collider.sharedMesh = chunk.Solid;
            flatColliders[index].enabled = false;
        }

        // 当たり判定はチャンク全面、見た目は穴とその縁だけ。見た目の三角形を足したら true。
        private bool AddTriangle(ChunkMeshBuilder visual, ChunkMeshBuilder solid, int cellX, int cellZ, int half, int a, int b, int c)
        {
            solid.AddTriangle(SolidPoint(a), SolidPoint(b), SolidPoint(c), Vector2.zero);
            if (!IsOpen(a) && !IsOpen(b) && !IsOpen(c) && !TouchesHole(cellX, cellZ)) return false;

            int sa = steps[a], sb = steps[b], sc = steps[c];
            int low = Mathf.Min(sa, Mathf.Min(sb, sc));
            int high = Mathf.Max(sa, Mathf.Max(sb, sc));
            int row = low != high ? EarthPalette.SlopeRow(low) : low > 0 ? EarthPalette.FloorRow(low) : EarthPalette.RimRow;
            Vector2 uv = EarthPalette.Color(row, Hash01(cellX * 7 + half, cellZ * 3 - half));
            visual.AddTriangle(VisualPoint(a), VisualPoint(b), VisualPoint(c), uv);
            return true;
        }

        // 段の高さそのまま。キャラクターの足元がガタつかないよう横にはずらさない。
        private Vector3 SolidPoint(int vertex)
        {
            int x = vertex % vertsX, z = vertex / vertsX;
            return new Vector3(origin.x + x * cellSize, -steps[vertex] * stepDepth, origin.y + z * cellSize);
        }

        private Vector3 VisualPoint(int vertex)
        {
            int x = vertex % vertsX, z = vertex / vertsX;
            Vector3 point = SolidPoint(vertex);
            float shift = VertexShift * cellSize * 2f;
            point.x += (Hash01(x * 3 + 1, z * 7 + 2) - 0.5f) * shift;
            point.z += (Hash01(x * 11 + 5, z * 2 + 9) - 0.5f) * shift;
            if (steps[vertex] == 0) point.y = RimBaseHeight + (rim[vertex] ? Hash01(x * 17 + 4, z * 13 + 6) * RimLift : 0f);
            return point;
        }

        private Chunk NewChunk()
        {
            var root = new GameObject("DigChunk");
            root.transform.SetParent(transform, false);
            var chunk = new Chunk { Root = root };
            chunk.Filter = root.AddComponent<MeshFilter>();
            root.AddComponent<MeshRenderer>().sharedMaterial = earth;
            chunk.Collider = root.AddComponent<MeshCollider>();
            chunk.Visual = new Mesh { name = "DigVisual" };
            chunk.Solid = new Mesh { name = "DigSolid" };
            return chunk;
        }

        // 同じ頂点なら同じ値（ネットワーク越しでも同じ穴の形になる）。
        private static float Hash01(int x, int z)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(z * 19349663);
                h ^= h >> 13;
                h *= 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFF) / 65535f;
            }
        }
    }

    /// チャンク1つ分のメッシュを組み立てる。三角形ごとに頂点を分けて、面をはっきり出す。
    internal class ChunkMeshBuilder
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int> triangles = new List<int>();

        public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector2 uv)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            int first = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            for (int i = 0; i < 3; i++)
            {
                normals.Add(normal);
                uvs.Add(uv);
                triangles.Add(first + i);
            }
        }

        public void Fill(Mesh mesh)
        {
            mesh.Clear();
            mesh.indexFormat = vertices.Count > 60000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }
    }

    /// 穴の土の色。三角形を1色で塗る。行が層（浅いほど明るく、深いほど湿って暗い）、列が色違い。
    /// 足場と坂で色を変えて、段の形を読みやすくする。
    internal static class EarthPalette
    {
        private const int Columns = 4;
        private const string LitShader = "Universal Render Pipeline/Lit";
        private const float Smoothness = 0.05f;
        private const float ColumnStep = 0.05f;

        public static readonly Color FallbackDirt = new Color(0.62f, 0.46f, 0.3f);

        // 土の道の色に対する倍率。
        private static readonly float[] RowLight =
        {
            0.86f, 0.74f, 0.62f, 0.5f,  // 足場 1〜4段目
            0.62f, 0.5f, 0.4f, 0.32f,   // 坂 1〜4層目
            1f,                         // 草がはがれた面
        };

        private const int FloorStart = 0;
        private const int SlopeStart = 4;
        public const int RimRow = 8;
        private static int Rows => RowLight.Length;

        public static int FloorRow(int steps)
        {
            return FloorStart + Mathf.Clamp(steps - 1, 0, 3);
        }

        public static int SlopeRow(int layer)
        {
            return SlopeStart + Mathf.Clamp(layer, 0, 3);
        }

        public static Vector2 Color(int row, float variation)
        {
            int column = Mathf.Min(Columns - 1, Mathf.FloorToInt(variation * Columns));
            return new Vector2((column + 0.5f) / Columns, (row + 0.5f) / Rows);
        }

        public static Material CreateMaterial(Color dirt)
        {
            var material = new Material(Shader.Find(LitShader)) { name = "DugEarth" };
            material.SetTexture("_BaseMap", BuildTexture(dirt));
            material.SetFloat("_Smoothness", Smoothness);
            return material;
        }

        private static Texture2D BuildTexture(Color dirt)
        {
            var pixels = new Color32[Columns * Rows];
            for (int row = 0; row < Rows; row++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    Color c = dirt * (RowLight[row] * (1f + (column - (Columns - 1) * 0.5f) * ColumnStep));
                    c.a = 1f;
                    pixels[column + row * Columns] = c;
                }
            }
            var texture = new Texture2D(Columns, Rows, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "DugEarthPalette" };
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}
