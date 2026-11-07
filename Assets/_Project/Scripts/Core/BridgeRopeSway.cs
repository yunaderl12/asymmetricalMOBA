using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    /// 吊り橋に人が乗ると、まわりの主ロープと手すりのロープが少し引っぱられて下がり、横に揺れる。
    /// 地形モデルの Field_BridgeRopes（ロープ・踏み板・門柱の横木が1つのメッシュ）を写して、甲板から高い所だけ動かす。
    /// 踏み板と足元のロープは動かさない（歩く面の当たり判定は Field_Bridges のまま）。見た目だけ。
    public class BridgeRopeSway : MonoBehaviour
    {
        private const string RopesName = "Field_BridgeRopes";
        private const string DeckName = "Field_Bridges";
        private const float StillHeight = 0.35f;      // 甲板からこの高さまでは動かさない（踏み板・足元のロープ）
        private const float FullHeight = 2.2f;        // この高さより上はいっぱいに動く（主ロープ）
        private const float SagDepth = 0.45f;         // 乗っている人のすぐ近くで主ロープが下がる量 (m)
        private const float SagRadius = 9f;           // 下がる範囲（橋に沿った距離, m）
        private const float SwayAmplitude = 0.28f;    // 横揺れの大きさ (m)
        private const float SwayFrequency = 1.1f;     // 揺れの速さ (Hz)
        private const float SwayWaveNumber = 0.28f;   // 揺れが橋に沿って伝わる波の細かさ (rad/m)
        private const float EnergyHalfLife = 0.9f;    // 揺れが半分に収まるまで (s)
        private const float EnergyPerMeter = 0.09f;   // 橋の上を1m 歩くごとに足される揺れ
        private const float EnterKick = 0.45f;        // 乗った瞬間に足される揺れ
        private const float SagFollow = 6f;           // 下がり具合が人に追いつく速さ
        private const float CorridorMargin = 1.5f;    // 橋の幅からこれだけ外までは乗っているとみなす
        private const float RefreshSeconds = 0.25f;

        private class Bridge
        {
            public Vector3 Start;
            public Vector3 Dir;
            public Vector3 Side;
            public float Length;
            public float MinS = float.PositiveInfinity, MaxS = float.NegativeInfinity;
            public float Energy;
            public float[] Sag;       // 1m ごとの下がり具合（0〜1）
            public float[] SagGoal;
        }

        private class Rider
        {
            public Transform Body;
            public int Bridge = -1;
            public float S;
        }

        private Mesh mesh;
        private Vector3[] baseLocal;
        private Vector3[] work;
        private int[] vertexBridge;
        private float[] vertexS;
        private float[] vertexWeight;
        private readonly List<Bridge> bridges = new List<Bridge>();
        private readonly Dictionary<long, float> deckCache = new Dictionary<long, float>();
        private readonly Dictionary<Transform, Rider> riders = new Dictionary<Transform, Rider>();
        private float refreshTimer;
        private bool moved;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            SceneManager.sceneLoaded += (scene, mode) => Attach();
            Attach();
        }

        private static void Attach()
        {
            var ropes = GameObject.Find(RopesName);
            if (ropes == null || ropes.GetComponent<BridgeRopeSway>() != null) return;
            var filter = ropes.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.isReadable) return;
            ropes.AddComponent<BridgeRopeSway>();
        }

        private void Start()
        {
            var map = StageMap.Map;
            if (map == null || map.bridgeStarts == null || map.bridgeStarts.Length < 2) { enabled = false; return; }
            var filter = GetComponent<MeshFilter>();
            mesh = Instantiate(filter.sharedMesh);
            mesh.name = "BridgeRopesSway";
            mesh.MarkDynamic();
            filter.sharedMesh = mesh;

            var castle = new Vector3(map.castleX, 0f, map.castleZ);
            for (int i = 0; i + 1 < map.bridgeStarts.Length; i += 2)
            {
                var start = new Vector3(map.bridgeStarts[i], 0f, map.bridgeStarts[i + 1]);
                Vector3 dir = castle - start;
                dir.y = 0f;
                float length = dir.magnitude - map.islandRadius;
                dir.Normalize();
                bridges.Add(new Bridge { Start = start, Dir = dir, Side = Vector3.Cross(Vector3.up, dir), Length = length });
            }

            baseLocal = mesh.vertices;
            work = (Vector3[])baseLocal.Clone();
            vertexBridge = new int[baseLocal.Length];
            vertexS = new float[baseLocal.Length];
            vertexWeight = new float[baseLocal.Length];
            for (int v = 0; v < baseLocal.Length; v++)
            {
                Vector3 world = transform.TransformPoint(baseLocal[v]);
                vertexBridge[v] = Locate(world, out float s, 3f);
                vertexS[v] = s;
                if (vertexBridge[v] < 0) continue;
                var b = bridges[vertexBridge[v]];
                b.MinS = Mathf.Min(b.MinS, s);
                b.MaxS = Mathf.Max(b.MaxS, s);
                float deck = DeckHeight(vertexBridge[v], s, deckCache);
                vertexWeight[v] = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(StillHeight, FullHeight, world.y - deck));
            }
            foreach (var b in bridges)
            {
                int n = Mathf.Max(2, Mathf.CeilToInt(b.Length) + 1);
                b.Sag = new float[n];
                b.SagGoal = new float[n];
            }
            // 門柱の上（両端）は動かさない。
            for (int v = 0; v < baseLocal.Length; v++)
            {
                int i = vertexBridge[v];
                if (i < 0) continue;
                var b = bridges[i];
                float t = Mathf.InverseLerp(b.MinS + 1f, b.MaxS - 1f, vertexS[v]);
                vertexWeight[v] *= Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
            }
        }

        // 橋の甲板の高さ（真上から当たり判定を探す）。橋に沿って1m ごとに覚えておく。
        private float DeckHeight(int bridge, float s, Dictionary<long, float> cache)
        {
            long key = bridge * 100000L + Mathf.RoundToInt(s);
            if (cache.TryGetValue(key, out float y)) return y;
            var b = bridges[bridge];
            Vector3 p = b.Start + b.Dir * Mathf.Round(s) + Vector3.up * 200f;
            y = float.NegativeInfinity;
            foreach (var hit in Physics.RaycastAll(p, Vector3.down, 400f, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.name == DeckName && hit.point.y > y) y = hit.point.y;
            if (float.IsNegativeInfinity(y)) y = 0f;
            cache[key] = y;
            return y;
        }

        // 一番近い橋と、橋に沿った距離。どの橋の上でもなければ -1。
        private int Locate(Vector3 world, out float s, float margin)
        {
            s = 0f;
            int best = -1;
            float bestSide = float.PositiveInfinity;
            float half = StageMapBridgeHalfWidth() + margin;
            for (int i = 0; i < bridges.Count; i++)
            {
                var b = bridges[i];
                Vector3 rel = world - b.Start;
                rel.y = 0f;
                float along = Vector3.Dot(rel, b.Dir);
                float side = Mathf.Abs(Vector3.Dot(rel, b.Side));
                if (along < -2f || along > b.Length + 2f || side > half || side >= bestSide) continue;
                best = i;
                bestSide = side;
                s = along;
            }
            return best;
        }

        private static float StageMapBridgeHalfWidth()
        {
            var map = StageMap.Map;
            return map != null && map.bridgeWidth > 0f ? map.bridgeWidth * 0.5f : 4f;
        }

        private void Update()
        {
            if (mesh == null) return;
            refreshTimer -= Time.deltaTime;
            if (refreshTimer <= 0f)
            {
                refreshTimer = RefreshSeconds;
                RefreshRiders();
            }

            float decay = Mathf.Pow(0.5f, Time.deltaTime / EnergyHalfLife);
            foreach (var b in bridges)
            {
                b.Energy *= decay;
                System.Array.Clear(b.SagGoal, 0, b.SagGoal.Length);
            }
            foreach (var rider in riders.Values) UpdateRider(rider);

            bool any = false;
            float follow = 1f - Mathf.Exp(-SagFollow * Time.deltaTime);
            foreach (var b in bridges)
            {
                for (int k = 0; k < b.Sag.Length; k++)
                {
                    b.Sag[k] += (b.SagGoal[k] - b.Sag[k]) * follow;
                    if (b.Sag[k] > 0.002f) any = true;
                }
                if (b.Energy > 0.01f) any = true;
            }
            if (!any)
            {
                if (moved)
                {
                    mesh.vertices = baseLocal;
                    moved = false;
                }
                return;
            }
            Deform();
        }

        private void UpdateRider(Rider rider)
        {
            if (rider.Body == null || !rider.Body.gameObject.activeInHierarchy) return;
            Vector3 pos = rider.Body.position;
            int bridge = Locate(pos, out float s, CorridorMargin);
            // 橋より下（川や海）にいるときは乗っていない。
            if (bridge >= 0 && pos.y < DeckHeight(bridge, s, deckCache) - 1.5f) bridge = -1;
            if (bridge >= 0)
            {
                var b = bridges[bridge];
                if (rider.Bridge != bridge) b.Energy = Mathf.Min(1f, b.Energy + EnterKick);
                else b.Energy = Mathf.Min(1f, b.Energy + Mathf.Abs(s - rider.S) * EnergyPerMeter);
                int reach = Mathf.CeilToInt(SagRadius * 2f);
                int center = Mathf.RoundToInt(s);
                for (int k = Mathf.Max(0, center - reach); k <= Mathf.Min(b.SagGoal.Length - 1, center + reach); k++)
                {
                    float d = (k - s) / SagRadius;
                    b.SagGoal[k] = Mathf.Min(1f, b.SagGoal[k] + Mathf.Exp(-d * d));
                }
            }
            rider.Bridge = bridge;
            rider.S = s;
        }

        private void Deform()
        {
            float time = Time.time * SwayFrequency * Mathf.PI * 2f;
            for (int v = 0; v < baseLocal.Length; v++)
            {
                int i = vertexBridge[v];
                float w = i >= 0 ? vertexWeight[v] : 0f;
                if (w <= 0f)
                {
                    work[v] = baseLocal[v];
                    continue;
                }
                var b = bridges[i];
                float s = vertexS[v];
                int k = Mathf.Clamp(Mathf.RoundToInt(s), 0, b.Sag.Length - 1);
                float sag = b.Sag[k] * SagDepth;
                // 揺れは橋に沿って伝わる波。乗っている人のまわりで少し大きい。
                float sway = Mathf.Sin(time - s * SwayWaveNumber) * SwayAmplitude * b.Energy * (0.6f + 0.4f * b.Sag[k]);
                Vector3 offset = Vector3.down * sag * (1f + 0.25f * Mathf.Sin(time * 1.3f - s * SwayWaveNumber) * b.Energy) + b.Side * sway;
                work[v] = baseLocal[v] + transform.InverseTransformVector(offset * w);
            }
            mesh.vertices = work;
            moved = true;
        }

        private void RefreshRiders()
        {
            var seen = new HashSet<Transform>();
            foreach (var go in GameObject.FindGameObjectsWithTag("Player")) seen.Add(go.transform);
            foreach (var monster in FindObjectsByType<Game.Monster.Monster>(FindObjectsSortMode.None)) seen.Add(monster.transform);
            foreach (var king in FindObjectsByType<Game.DemonKing.DemonKingController>(FindObjectsSortMode.None)) seen.Add(king.transform);
            var gone = new List<Transform>();
            foreach (var t in riders.Keys) if (t == null || !seen.Contains(t)) gone.Add(t);
            foreach (var t in gone) riders.Remove(t);
            foreach (var t in seen)
                if (!riders.ContainsKey(t)) riders[t] = new Rider { Body = t, S = 0f };
        }

        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }
    }
}
