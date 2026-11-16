using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Game.Core;
using Game.UI;

namespace Game.Monster
{
    /// 橋のたもとに張られた魔法陣の結界（StageMap.json の bossGates）。
    /// 陸の側を弧で囲み、両端は海岸の見えない壁に重ねる。勇者は弧の内側（橋の側）へ入れない。
    /// その地方のボス（山はドラゴン、砂漠はスコーピオン、森はウェンディゴ）を倒すと解けて、二度と戻らない（ボスが復活しても）。
    /// 魔王は通り抜けられる（当たり判定を無視する）。BOT の勇者は NavMesh を切り抜くので、結界の先へ道を探さない。
    /// 見た目: 石の柱、魔法陣の模様の入った光るテープ2段、薄い光の幕、地面の大きな魔法陣。模様の絵は実行時に作る。
    public class BossGate : MonoBehaviour
    {
        private const float Spacing = 2.6f;
        private const float WallHeight = 12f;
        private const float NoticeRange = 3.5f;
        private static readonly float[] TapeHeights = { 0.95f, 2.05f };
        private const float TapeWidth = 0.42f;
        private static readonly Color SealColor = new Color(0.78f, 0.55f, 1f, 1f);
        private static readonly Color GlyphColor = new Color(1f, 0.86f, 0.5f, 1f);

        private static readonly List<BossGate> gates = new List<BossGate>();
        private static Texture2D tapeTexture, circleTexture, curtainTexture;
        private static Material stoneMaterial;

        public string BossKey { get; private set; }
        public string BossName { get; private set; }
        public bool IsOpen { get; private set; }
        public Vector3 Center { get; private set; }
        public float Radius { get; private set; }

        private readonly List<Collider> walls = new List<Collider>();
        private readonly List<NavMeshObstacle> obstacles = new List<NavMeshObstacle>();
        private readonly List<Transform> posts = new List<Transform>();
        private readonly List<Renderer> crystals = new List<Renderer>();
        private readonly HashSet<Collider> ignored = new HashSet<Collider>();
        private Material tapeMat, curtainMat, circleMat, crystalMat;
        private Transform circle;
        private float nextScan;
        private float nextNotice;

        public static IReadOnlyList<BossGate> All => gates;

        /// StageMap の bossGates を全部作る。names はボスの種類（dragon など）から表示名への対応。
        public static void BuildAll(Dictionary<string, string> names)
        {
            var map = StageMap.Map;
            if (map?.bossGates == null) return;
            foreach (var g in map.bossGates)
            {
                if (g == null || g.radius <= 0f) continue;
                string name = names != null && names.TryGetValue(g.boss, out var n) ? n : g.boss;
                var go = new GameObject("BossGate_" + g.boss);
                var gate = go.AddComponent<BossGate>();
                gate.Build(g, name);
            }
        }

        /// そのボスの結界を解く（ボスを倒したとき）。
        public static void OpenFor(string bossKey)
        {
            foreach (var gate in gates)
                if (gate.BossKey == bossKey) gate.Open();
        }

        /// 中ボスを置かなかった地方: 演出も知らせもなく、初めから結界をなくす。
        public static void OpenSilently(string bossKey)
        {
            // 消すと OnDisable で gates から抜けるので、写しを回す。
            foreach (var gate in gates.ToArray())
            {
                if (gate.BossKey != bossKey || gate.IsOpen) continue;
                gate.IsOpen = true;
                foreach (var w in gate.walls) if (w != null) w.enabled = false;
                foreach (var o in gate.obstacles) if (o != null) o.enabled = false;
                gate.gameObject.SetActive(false);
            }
        }

        /// 閉じた結界が a と b の間をへだてているか（片方が結界の内側、もう片方が外側）。
        public static bool SealedBetween(Vector3 a, Vector3 b)
        {
            foreach (var gate in gates)
            {
                if (gate.IsOpen) continue;
                bool ia = gate.Inside(a), ib = gate.Inside(b);
                if (ia != ib) return true;
            }
            return false;
        }

        /// 魔王が閉じた結界を通り抜けるための、壁の反対側の点。from が from と to をへだてる結界の壁ぎわ（margin 以内）にいるときだけ返す。
        /// 結界は NavMesh を切り抜いているので、NavMesh で歩く BOT 魔王はここで壁の向こうへ移る。
        public static bool TryCrossing(Vector3 from, Vector3 to, float margin, out Vector3 across)
        {
            across = from;
            foreach (var gate in gates)
            {
                if (gate.IsOpen) continue;
                bool inside = gate.Inside(from);
                if (inside == gate.Inside(to)) continue;
                Vector3 d = from - gate.Center;
                d.y = 0f;
                float r = d.magnitude;
                if (r < 0.01f || Mathf.Abs(r - gate.Radius) > margin) continue;
                float side = inside ? gate.Radius + margin : gate.Radius - margin;
                across = gate.Center + d / r * side;
                across.y = from.y;
                return true;
            }
            return false;
        }

        private bool Inside(Vector3 p)
        {
            Vector3 d = p - Center;
            d.y = 0f;
            return d.magnitude < Radius;
        }

        /// デバッグ用: 結界を張り直す。
        public static void CloseAll()
        {
            foreach (var gate in gates) gate.Close();
        }

        private void OnEnable() => gates.Add(this);
        private void OnDisable() => gates.Remove(this);

        private void Build(StageMap.Gate g, string bossName)
        {
            BossKey = g.boss;
            BossName = bossName;
            Radius = g.radius;
            Center = new Vector3(g.x, GroundY(g.x, g.z), g.z);
            transform.position = Center;

            var points = ArcPoints(Center, Radius);
            if (points.Count < 2)
            {
                Debug.LogWarning("[BossGate] " + g.boss + ": no land around the gate");
                return;
            }

            tapeMat = NewMaterial(TapeTexture(), Color.white);
            curtainMat = NewMaterial(CurtainTexture(), new Color(SealColor.r, SealColor.g, SealColor.b, 0.55f));
            circleMat = NewMaterial(CircleTexture(), new Color(SealColor.r, SealColor.g, SealColor.b, 0.85f));
            crystalMat = NewMaterial(null, Color.Lerp(SealColor, Color.white, 0.35f));

            BuildWalls(points);
            BuildPosts(points);
            BuildTape(points);
            BuildCurtain(points);
            BuildCircle(points);
            Debug.Log("[BossGate] " + g.boss + " sealed (" + points.Count + " posts, r=" + Radius + ")");
        }

        // 円周の陸の部分。海との境から2つずつ海の側へはみ出して、海岸の壁と重ねる。
        private static List<Vector3> ArcPoints(Vector3 center, float radius)
        {
            int n = Mathf.Max(8, Mathf.CeilToInt(2f * Mathf.PI * radius / Spacing));
            var land = new bool[n];
            var pos = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float a = i * 2f * Mathf.PI / n;
                pos[i] = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                land[i] = !StageMap.IsSea(pos[i]);
            }
            int start = -1;
            for (int i = 0; i < n; i++)
                if (land[i] && !land[(i - 1 + n) % n]) { start = i; break; }
            var result = new List<Vector3>();
            if (start < 0)
            {
                if (!land[0]) return result;
                for (int i = 0; i <= n; i++) result.Add(pos[i % n]);
            }
            else
            {
                int end = start;
                while (land[(end + 1) % n] && (end + 1) % n != start) end++;
                for (int i = start - 2; i <= end + 2; i++) result.Add(pos[((i % n) + n) % n]);
            }
            float floor = float.MaxValue;
            for (int i = 0; i < result.Count; i++)
            {
                var p = result[i];
                float y = StageMap.IsSea(p) ? float.NaN : GroundY(p.x, p.z);
                result[i] = new Vector3(p.x, y, p.z);
                if (!float.IsNaN(y)) floor = Mathf.Min(floor, y);
            }
            // 海の上の点は、いちばん近い陸の点と同じ高さにする。
            for (int i = 0; i < result.Count; i++)
            {
                if (!float.IsNaN(result[i].y)) continue;
                float y = floor;
                for (int k = 1; k < result.Count; k++)
                {
                    if (i - k >= 0 && !float.IsNaN(result[i - k].y)) { y = result[i - k].y; break; }
                    if (i + k < result.Count && !float.IsNaN(result[i + k].y)) { y = result[i + k].y; break; }
                }
                result[i] = new Vector3(result[i].x, y, result[i].z);
            }
            return result;
        }

        private static float GroundY(float x, float z)
        {
            float y = float.MinValue;
            foreach (var hit in Physics.RaycastAll(new Vector3(x, 200f, z), Vector3.down, 400f, ~0, QueryTriggerInteraction.Ignore))
            {
                var c = hit.collider;
                if (c.CompareTag("Player") || c.CompareTag("Monster") || c.name == "Field_TreeColliders" || c.name == "Field_Barrier"
                    || c.name == "Field_Bridges" || c.GetComponentInParent<BossGate>() != null) continue;
                y = Mathf.Max(y, hit.point.y);
            }
            return y > float.MinValue ? y : 0f;
        }

        private void BuildWalls(List<Vector3> points)
        {
            for (int i = 0; i + 1 < points.Count; i++)
            {
                Vector3 a = points[i], b = points[i + 1];
                Vector3 d = b - a;
                d.y = 0f;
                var go = new GameObject("GateWall");
                go.transform.SetParent(transform, false);
                go.transform.position = (a + b) * 0.5f + Vector3.up * (WallHeight * 0.5f - 1f);
                go.transform.rotation = Quaternion.LookRotation(d.normalized);
                var box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(0.6f, WallHeight, d.magnitude + 0.4f);
                walls.Add(box);
                var obstacle = go.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.size = new Vector3(1.2f, WallHeight, d.magnitude + 0.6f);
                obstacle.carving = true;
                obstacle.carveOnlyStationary = false;
                obstacles.Add(obstacle);
            }
        }

        private void BuildPosts(List<Vector3> points)
        {
            if (stoneMaterial == null)
            {
                stoneMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "BossGateStone" };
                stoneMaterial.SetColor("_BaseColor", new Color(0.24f, 0.21f, 0.27f));
                stoneMaterial.SetFloat("_Smoothness", 0.15f);
            }
            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];
                // 柱と結晶をまとめる入れ物（拡大率1）。結界が解けるときはこれごと地面に沈める。
                var holder = new GameObject("GatePost").transform;
                holder.SetParent(transform, false);
                holder.position = p;
                posts.Add(holder);

                var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = "Pillar";
                Destroy(post.GetComponent<Collider>());
                post.transform.SetParent(holder, false);
                post.transform.localPosition = Vector3.up * 1.4f;
                post.transform.localScale = new Vector3(0.38f, 1.7f, 0.38f);
                post.GetComponent<Renderer>().sharedMaterial = stoneMaterial;

                var gem = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                gem.name = "GateCrystal";
                Destroy(gem.GetComponent<Collider>());
                gem.transform.SetParent(holder, false);
                gem.transform.localPosition = Vector3.up * 3.35f;
                gem.transform.localScale = new Vector3(0.3f, 0.46f, 0.3f);
                var r = gem.GetComponent<Renderer>();
                r.sharedMaterial = crystalMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                crystals.Add(r);
            }
        }

        // 柱から柱へ少したるませて張ったテープ。上下2段。
        private void BuildTape(List<Vector3> points)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            const int sub = 5;
            foreach (float h in TapeHeights)
            {
                float u = 0f;
                bool first = true;
                for (int i = 0; i + 1 < points.Count; i++)
                {
                    Vector3 a = points[i], b = points[i + 1];
                    float len = Vector3.Distance(a, b);
                    for (int k = 0; k <= sub; k++)
                    {
                        if (k == 0 && i > 0) continue;
                        float t = k / (float)sub;
                        float sag = 0.16f * 4f * t * (1f - t);
                        Vector3 c = Vector3.Lerp(a, b, t) + Vector3.up * (h - sag);
                        float uu = (u + len * t) / (TapeWidth * 4f);
                        int idx = verts.Count;
                        verts.Add(c - Vector3.up * TapeWidth * 0.5f);
                        verts.Add(c + Vector3.up * TapeWidth * 0.5f);
                        uvs.Add(new Vector2(uu, 0f));
                        uvs.Add(new Vector2(uu, 1f));
                        if (!first) tris.AddRange(new[] { idx - 2, idx - 1, idx + 1, idx - 2, idx + 1, idx });
                        first = false;
                    }
                    u += len;
                }
            }
            AddMesh("GateTape", verts, uvs, tris, tapeMat);
        }

        private void BuildCurtain(List<Vector3> points)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            float u = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                if (i > 0) u += Vector3.Distance(points[i - 1], points[i]);
                int idx = verts.Count;
                verts.Add(points[i] + Vector3.up * 0.05f);
                verts.Add(points[i] + Vector3.up * 4.6f);
                uvs.Add(new Vector2(u / 6f, 0f));
                uvs.Add(new Vector2(u / 6f, 1f));
                if (i > 0) tris.AddRange(new[] { idx - 2, idx - 1, idx + 1, idx - 2, idx + 1, idx });
            }
            AddMesh("GateCurtain", verts, uvs, tris, curtainMat);
        }

        // 結界の中（橋の側）の地面に描く大きな魔法陣。
        private void BuildCircle(List<Vector3> points)
        {
            var go = new GameObject("GateCircle");
            go.transform.SetParent(transform, false);
            go.transform.position = Center + Vector3.up * 0.08f;
            float s = Radius * 0.62f;
            var mesh = new Mesh { name = "GateCircle" };
            mesh.vertices = new[] { new Vector3(-s, 0f, -s), new Vector3(-s, 0f, s), new Vector3(s, 0f, s), new Vector3(s, 0f, -s) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = circleMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            circle = go.transform;
        }

        private void AddMesh(string name, List<Vector3> verts, List<Vector2> uvs, List<int> tris, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = Vector3.zero;
            var mesh = new Mesh { name = name };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void Update()
        {
            float t = Time.time;
            if (tapeMat != null) tapeMat.mainTextureOffset = new Vector2(-t * 0.12f, 0f);
            if (curtainMat != null) curtainMat.mainTextureOffset = new Vector2(t * 0.05f, -t * 0.18f);
            if (circle != null) circle.Rotate(0f, 8f * Time.deltaTime, 0f, Space.World);
            if (IsOpen) return;
            float pulse = 0.75f + 0.25f * Mathf.Sin(t * 2.4f);
            if (crystalMat != null) crystalMat.color = Color.Lerp(SealColor, Color.white, 0.35f) * pulse + new Color(0f, 0f, 0f, 1f - pulse);

            if (Time.time >= nextScan)
            {
                nextScan = Time.time + 1f;
                LetDemonKingThrough();
            }
            NoticeLocalHero();
        }

        // 魔王（人でも BOT でも）は結界を素通りできる。
        private void LetDemonKingThrough()
        {
            foreach (var dk in FindObjectsByType<Game.DemonKing.DemonKingController>(FindObjectsSortMode.None))
                Ignore(dk.GetComponent<Collider>());
            foreach (var dk in FindObjectsByType<Game.Bot.BotDemonKing>(FindObjectsSortMode.None))
                foreach (var c in dk.GetComponentsInChildren<Collider>()) Ignore(c);
        }

        private void Ignore(Collider c)
        {
            if (c == null || ignored.Contains(c)) return;
            ignored.Add(c);
            foreach (var w in walls) if (w != null) Physics.IgnoreCollision(c, w, true);
        }

        private void NoticeLocalHero()
        {
            var hero = Game.Hero.HumanHeroMarker.Instance;
            if (hero == null || Time.unscaledTime < nextNotice) return;
            Vector3 d = hero.transform.position - Center;
            d.y = 0f;
            float dist = d.magnitude;
            if (dist > Radius + NoticeRange || dist < Radius - 2f) return;
            if (StageMap.IsSea(hero.transform.position)) return;
            nextNotice = Time.unscaledTime + 6f;
            MatchNotice.Show("魔法陣の結界が道をふさいでいる ── " + BossName + "を倒すと解ける", 3f);
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            foreach (var w in walls) if (w != null) w.enabled = false;
            foreach (var o in obstacles) if (o != null) o.enabled = false;
            Debug.Log("[BossGate] " + BossKey + " opened");
            MatchNotice.Show(BossName + "が倒れ、結界が解けた！", 4f);
            StopAllCoroutines();
            StartCoroutine(Vanish());
        }

        private void Close()
        {
            StopAllCoroutines();
            IsOpen = false;
            foreach (var w in walls) if (w != null) w.enabled = true;
            foreach (var o in obstacles) if (o != null) o.enabled = true;
            foreach (Transform child in transform) child.gameObject.SetActive(true);
            SetAlpha(1f, 1f);
            if (circle != null) circle.localScale = Vector3.one;
            for (int i = 0; i < posts.Count; i++)
            {
                var pos = posts[i].position;
                posts[i].position = new Vector3(pos.x, GroundY(pos.x, pos.z), pos.z);
            }
        }

        // 光がはじけて広がり、テープと幕が消え、柱が地面に沈む。
        private IEnumerator Vanish()
        {
            const float seconds = 1.8f;
            var startY = new float[posts.Count];
            for (int i = 0; i < posts.Count; i++) startY[i] = posts[i].position.y;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                float flash = k < 0.15f ? k / 0.15f : 1f - (k - 0.15f) / 0.85f;
                SetAlpha(1f - k, 1f + flash * 1.5f);
                if (circle != null) circle.localScale = Vector3.one * (1f + k * 0.6f);
                for (int i = 0; i < posts.Count; i++)
                {
                    var p = posts[i].position;
                    posts[i].position = new Vector3(p.x, startY[i] - 3.6f * k * k, p.z);
                }
                yield return null;
            }
            foreach (Transform child in transform) child.gameObject.SetActive(false);
        }

        private void SetAlpha(float a, float glow)
        {
            if (tapeMat != null) tapeMat.color = new Color(glow, glow, glow, a);
            if (curtainMat != null) curtainMat.color = new Color(SealColor.r * glow, SealColor.g * glow, SealColor.b * glow, 0.55f * a);
            if (circleMat != null) circleMat.color = new Color(SealColor.r * glow, SealColor.g * glow, SealColor.b * glow, 0.85f * a);
            if (crystalMat != null) { var c = Color.Lerp(SealColor, Color.white, 0.35f); crystalMat.color = new Color(c.r, c.g, c.b, a); }
        }

        // ---- 実行時に作る材質と模様 ----

        private static Material NewMaterial(Texture2D texture, Color color)
        {
            var shader = Shader.Find("Sprites/Default");
            var mat = new Material(shader) { name = "BossGate" };
            if (texture != null) mat.mainTexture = texture;
            mat.color = color;
            mat.renderQueue = 3000;
            return mat;
        }

        private static float Line(float d, float half, float aa) => Mathf.Clamp01(1f - (d - half) / aa);

        private static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (a + ab * t - p).magnitude;
        }

        // 六芒星（半径 r の円に内接する2つの三角形）までの距離。
        private static float Hexagram(Vector2 p, float r)
        {
            float best = float.MaxValue;
            for (int tri = 0; tri < 2; tri++)
            {
                for (int k = 0; k < 3; k++)
                {
                    float a0 = Mathf.PI * 0.5f + tri * Mathf.PI + k * 2f * Mathf.PI / 3f;
                    float a1 = a0 + 2f * Mathf.PI / 3f;
                    var v0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r;
                    var v1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r;
                    best = Mathf.Min(best, SegDist(p, v0, v1));
                }
            }
            return best;
        }

        /// テープの模様: 紫の帯に金の縁取り。小さな魔法陣と菱形のルーンが交互に並ぶ。横に4つ分（帯の高さの4倍）で1周。
        private static Texture2D TapeTexture()
        {
            if (tapeTexture != null) return tapeTexture;
            const int w = 256, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "BossGateTape", wrapMode = TextureWrapMode.Repeat };
            var px = new Color[w * h];
            var bg = new Color(0.20f, 0.06f, 0.36f, 0.86f);
            var gold = GlyphColor;
            float aa = 1.5f / h;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / h, v = (y + 0.5f) / h; // 帯の高さを1とした座標（u は 0〜4）
                    Color c = bg;
                    float edge = Mathf.Max(Line(Mathf.Abs(v - 0.1f), 0.03f, aa), Line(Mathf.Abs(v - 0.9f), 0.03f, aa));
                    var p1 = new Vector2(u - 1f, v - 0.5f);
                    float r1 = p1.magnitude;
                    float glyph = Mathf.Max(Line(Mathf.Abs(r1 - 0.33f), 0.022f, aa), Line(Mathf.Abs(r1 - 0.25f), 0.012f, aa));
                    glyph = Mathf.Max(glyph, Line(Hexagram(p1, 0.25f), 0.012f, aa));
                    var p2 = new Vector2(u - 3f, v - 0.5f);
                    float diamond = Mathf.Abs(p2.x) + Mathf.Abs(p2.y);
                    glyph = Mathf.Max(glyph, Line(Mathf.Abs(diamond - 0.26f), 0.018f, aa));
                    glyph = Mathf.Max(glyph, Line(new Vector2(p2.x * 0.6f, p2.y).magnitude, 0.05f, aa));
                    // 間の小さな点
                    glyph = Mathf.Max(glyph, Line(new Vector2(u - 2f, v - 0.5f).magnitude, 0.035f, aa));
                    glyph = Mathf.Max(glyph, Line(new Vector2(u - (u < 2f ? 0f : 4f), v - 0.5f).magnitude, 0.035f, aa));
                    float g = Mathf.Max(edge, glyph);
                    c = Color.Lerp(c, gold, g);
                    c.a = Mathf.Max(bg.a, g);
                    px[y * w + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tapeTexture = tex;
        }

        /// 地面の魔法陣: 二重の外円、ルーンの刻み、六芒星、内側の円。白で描き、材質の色で染める。
        private static Texture2D CircleTexture()
        {
            if (circleTexture != null) return circleTexture;
            const int n = 512;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "BossGateCircle", wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            float aa = 1.5f / n;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    var p = new Vector2((x + 0.5f) / n - 0.5f, (y + 0.5f) / n - 0.5f);
                    float r = p.magnitude;
                    float a = Mathf.Atan2(p.y, p.x);
                    float g = Line(Mathf.Abs(r - 0.47f), 0.006f, aa);
                    g = Mathf.Max(g, Line(Mathf.Abs(r - 0.415f), 0.004f, aa));
                    // 外円の間のルーン: 短い刻みと小さな点を交互に
                    if (r > 0.42f && r < 0.465f)
                    {
                        float cell = a / (2f * Mathf.PI) * 48f;
                        float f = cell - Mathf.Floor(cell);
                        bool tick = ((int)Mathf.Floor(cell + 100f)) % 2 == 0;
                        float d = Mathf.Abs(f - 0.5f) * (2f * Mathf.PI * r / 48f);
                        g = Mathf.Max(g, tick ? Line(d, 0.004f, aa) * Line(Mathf.Abs(r - 0.4425f), 0.013f, aa)
                                              : Line(new Vector2(d, r - 0.4425f).magnitude, 0.006f, aa));
                    }
                    g = Mathf.Max(g, Line(Hexagram(p, 0.405f), 0.005f, aa));
                    g = Mathf.Max(g, Line(Mathf.Abs(r - 0.2f), 0.005f, aa));
                    g = Mathf.Max(g, Line(Mathf.Abs(r - 0.12f), 0.003f, aa));
                    g = Mathf.Max(g, Line(Hexagram(new Vector2(-p.y, p.x), 0.12f), 0.003f, aa));
                    // 内側をうっすら光らせる
                    float fill = r < 0.47f ? 0.10f * (1f - r / 0.47f) : 0f;
                    px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Max(g, fill)));
                }
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return circleTexture = tex;
        }

        /// 光の幕: 下が濃く上へ消えていく、縦すじの入った模様。
        private static Texture2D CurtainTexture()
        {
            if (curtainTexture != null) return curtainTexture;
            const int w = 64, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "BossGateCurtain", wrapMode = TextureWrapMode.Repeat };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w, v = y / (float)h;
                    float streak = 0.55f + 0.45f * Mathf.Sin(u * Mathf.PI * 2f * 3f + Mathf.Sin(v * Mathf.PI * 2f) * 1.2f) * Mathf.Sin(u * Mathf.PI * 2f * 7f);
                    float fade = Mathf.Pow(1f - v, 1.6f);
                    px[y * w + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(streak * 0.5f) * fade);
                }
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return curtainTexture = tex;
        }
    }
}
