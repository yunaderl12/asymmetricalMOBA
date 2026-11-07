using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Hero;
using Game.Skill;

namespace Game.Monster
{
    /// 番人の叩きつけ。着地点の小さな衝撃波のあと、岩の槍が地面から順に飛び出して前へ走り、最後に剣山のように一斉に突き出る。
    /// 叩いた瞬間は近くの勇者の画面が揺れ、地面が少しへこむ。
    public class RockSpearLine : MonoBehaviour
    {
        private const float ProbeHeight = 8f;
        private const float ShockStartRadius = 0.5f;
        private const float ShockSeconds = 0.35f;
        private const float ShockWidth = 0.7f;
        private static readonly Color ShockColor = new Color(0.75f, 0.6f, 1f, 0.9f);
        private const float RiseSeconds = 0.09f;
        private const float SinkSeconds = 0.4f;
        private const float SideSpearOffset = 0.9f;
        private const float SideSpearScale = 0.6f;
        private const float TiltDegrees = 16f;
        private const float MaxHitHeightGap = 4f;
        private const int MeshVariants = 3;
        private const int ShardsPerBurst = 4;
        private const float ShardSeconds = 0.6f;
        private const float ShardGravity = 16f;
        private const float FinalHoldExtra = 0.35f;   // 剣山は少し長く残す
        private const float FinalBloomSeconds = 0.12f; // 真ん中から外へ順に突き出る
        private const float FinalInnerRing = 0.45f;
        private const float FinalInnerTilt = 14f;
        private const float FinalOuterTilt = 30f;
        private const int FinalShards = 12;
        private const float BurstShakeShare = 0.25f;  // 槍が走るあいだの地響き（叩きつけの揺れに対する割合）
        private const float BurstShakeRadius = 14f;
        private const float BurstShakeSeconds = 0.18f;
        private const float FinalShakeShare = 0.6f;
        private const int ImpactDust = 36;
        private const float ImpactDustSpeed = 7f;
        private const int ImpactChips = 10;
        private const string RockMaterialPath = "Golem/RockSpear";
        private const string CrystalMaterialPath = "Golem/RockSpearCrystal";

        private static Mesh[] meshes;
        private static Material[] materials;

        private Vector3 origin;
        private Vector3 direction;
        private MonsterData data;
        private GameObject attacker;
        private int atk;
        private readonly HashSet<HealthComponent> alreadyHit = new HashSet<HealthComponent>();

        public static void Spawn(Vector3 impact, Vector3 direction, MonsterData data, GameObject attacker, int atk)
        {
            var go = new GameObject("RockSpearLine");
            var line = go.AddComponent<RockSpearLine>();
            line.origin = impact;
            line.direction = direction;
            line.data = data;
            line.attacker = attacker;
            line.atk = atk;
            line.StartCoroutine(line.Run());
        }

        private IEnumerator Run()
        {
            Vector3 ground = GroundAt(origin);
            ShockRing.Spawn(ground, ShockStartRadius, data.shockwaveRadius, ShockSeconds, ShockWidth, ShockColor);
            DamageNear(ground, data.shockwaveRadius, data.shockwaveDamageScale);
            Impact(ground);

            for (int i = 0; i < data.spearSteps; i++)
            {
                Vector3 point = GroundAt(origin + direction * data.spearSpacing * (i + 1));
                if (i == data.spearSteps - 1)
                {
                    FinalCluster(point);
                    DamageNear(point, data.spearFinalRadius, data.spearDamageScale);
                    HeroFPSController.ShakeNear(point, data.slamShake * FinalShakeShare, data.slamShakeRadius * FinalShakeShare, data.slamShakeSeconds * FinalShakeShare);
                }
                else
                {
                    Burst(point, i);
                    DamageNear(point, data.spearHitRadius, data.spearDamageScale);
                    HeroFPSController.ShakeNear(point, data.slamShake * BurstShakeShare, BurstShakeRadius, BurstShakeSeconds);
                }
                yield return new WaitForSeconds(data.spearInterval);
            }
            yield return new WaitForSeconds(data.spearHoldSeconds + FinalHoldExtra + FinalBloomSeconds + RiseSeconds + SinkSeconds + ShardSeconds);
            Destroy(gameObject);
        }

        // 叩きつけた瞬間の凄み。画面が揺れ、土煙が上がり、地面が少しへこむ。
        private void Impact(Vector3 ground)
        {
            HeroFPSController.ShakeNear(ground, data.slamShake, data.slamShakeRadius, data.slamShakeSeconds);
            MeleeFx.DustBurst(ground, Mathf.Max(0.5f, data.slamDentRadius * 0.8f), ImpactDust, ImpactDustSpeed);
            if (data.slamDentRadius > 0f && GroundDig.Dig(ground, data.slamDentRadius, data.slamDentDepth))
                EarthChips.Spawn(ground, data.slamDentRadius * 0.6f, ImpactChips);
        }

        // 列の最後は剣山。真ん中に一番太い槍、まわりに外へ傾いた槍を内と外の二重の輪に立てる。
        private void FinalCluster(Vector3 point)
        {
            Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;
            float hold = data.spearHoldSeconds + FinalHoldExtra;
            StartCoroutine(Spear(point, Quaternion.AngleAxis(Random.Range(-4f, 4f), side), data.spearFinalHeightScale, 0f, hold));
            int count = Mathf.Max(0, data.spearFinalCount - 1);
            for (int i = 0; i < count; i++)
            {
                bool inner = i % 2 == 0;
                float angle = (i + Random.Range(-0.3f, 0.3f)) / Mathf.Max(1, count) * 360f;
                Vector3 outward = Quaternion.Euler(0f, angle, 0f) * direction;
                float ring = (inner ? FinalInnerRing : 1f) * Random.Range(0.85f, 1.05f);
                Vector3 foot = GroundAt(point + outward * data.spearFinalRadius * ring);
                float tilt = (inner ? FinalInnerTilt : FinalOuterTilt) + Random.Range(-5f, 5f);
                float size = (inner ? 0.85f : 0.6f) * Random.Range(0.85f, 1.1f) * data.spearFinalHeightScale;
                float delay = (inner ? 0.4f : 1f) * FinalBloomSeconds * Random.Range(0.7f, 1f);
                StartCoroutine(Spear(foot, Quaternion.AngleAxis(tilt, Vector3.Cross(Vector3.up, outward)), size, delay, hold));
            }
            for (int i = 0; i < FinalShards; i++) StartCoroutine(Shard(point));
            MeleeFx.DustBurst(point, data.spearFinalRadius * 0.7f, ImpactDust / 2, ImpactDustSpeed * 0.7f);
        }

        private void Burst(Vector3 point, int step)
        {
            Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;
            // 真ん中の太い槍の左右に、小さな槍を外へ傾けて立てる。歩幅ごとに左右の出方を変える。
            float swing = step % 2 == 0 ? 1f : -1f;
            float hold = data.spearHoldSeconds;
            StartCoroutine(Spear(point, Quaternion.AngleAxis(Random.Range(-6f, 6f) + TiltDegrees * 0.4f, side), 1f, 0f, hold));
            StartCoroutine(Spear(point + side * SideSpearOffset * swing - direction * 0.3f, Quaternion.AngleAxis(TiltDegrees * swing, direction), SideSpearScale, 0f, hold));
            StartCoroutine(Spear(point - side * SideSpearOffset * swing * 0.8f + direction * 0.3f, Quaternion.AngleAxis(-TiltDegrees * swing, direction), SideSpearScale * 0.75f, 0f, hold));
            for (int i = 0; i < ShardsPerBurst; i++) StartCoroutine(Shard(point));
        }

        private IEnumerator Spear(Vector3 foot, Quaternion tilt, float size, float delay, float hold)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            float height = data.spearHeight * size;
            var go = NewPart("RockSpear", transform, foot, tilt * Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up));
            float width = size * Random.Range(0.9f, 1.1f);
            for (float t = 0f; t < RiseSeconds; t += Time.deltaTime)
            {
                go.transform.localScale = new Vector3(width, height * (t / RiseSeconds), width);
                yield return null;
            }
            go.transform.localScale = new Vector3(width, height, width);
            yield return new WaitForSeconds(hold);
            for (float t = 0f; t < SinkSeconds; t += Time.deltaTime)
            {
                go.transform.position = foot - Vector3.up * height * (t / SinkSeconds);
                yield return null;
            }
            Destroy(go);
        }

        // 割れた地面のかけら。見た目だけ。
        private IEnumerator Shard(Vector3 point)
        {
            var go = NewPart("RockShard", transform, point, Random.rotation);
            float size = Random.Range(0.12f, 0.26f);
            go.transform.localScale = Vector3.one * size;
            Vector2 flat = Random.insideUnitCircle * 3.5f;
            Vector3 velocity = new Vector3(flat.x, Random.Range(5f, 9f), flat.y);
            for (float t = 0f; t < ShardSeconds; t += Time.deltaTime)
            {
                velocity.y -= ShardGravity * Time.deltaTime;
                go.transform.position += velocity * Time.deltaTime;
                yield return null;
            }
            Destroy(go);
        }

        private void DamageNear(Vector3 point, float radius, float scale)
        {
            foreach (var hero in GameObject.FindGameObjectsWithTag("Player"))
            {
                if (hero == null) continue;
                var hp = hero.GetComponent<HealthComponent>();
                if (hp == null || hp.IsDead || alreadyHit.Contains(hp)) continue;
                Vector3 gap = hero.transform.position - point;
                if (Mathf.Abs(gap.y) > MaxHitHeightGap) continue;
                gap.y = 0f;
                if (gap.magnitude > radius) continue;
                alreadyHit.Add(hp);
                hp.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(atk * scale)), attacker, DamageType.Physical);
            }
        }

        // 木や番人自身の当たり判定ではなく、足元の地面を探す。
        private static Vector3 GroundAt(Vector3 point)
        {
            float best = float.NegativeInfinity;
            Vector3 top = new Vector3(point.x, point.y + ProbeHeight, point.z);
            foreach (var hit in Physics.RaycastAll(top, Vector3.down, ProbeHeight * 2f, ~0, QueryTriggerInteraction.Ignore))
            {
                string hitName = hit.collider.name;
                if (hitName == "Field_TreeColliders" || hit.collider.GetComponentInParent<Monster>() != null || hit.collider.CompareTag("Player")) continue;
                if (hit.point.y > best) best = hit.point.y;
            }
            return best > float.NegativeInfinity ? new Vector3(point.x, best, point.z) : point;
        }

        private static GameObject NewPart(string name, Transform parent, Vector3 position, Quaternion rotation)
        {
            EnsureAssets();
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(position, rotation);
            go.AddComponent<MeshFilter>().sharedMesh = meshes[Random.Range(0, meshes.Length)];
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            return go;
        }

        private static void EnsureAssets()
        {
            if (materials == null)
                materials = new[] { Resources.Load<Material>(RockMaterialPath), Resources.Load<Material>(CrystalMaterialPath) };
            if (meshes != null) return;
            meshes = new Mesh[MeshVariants];
            for (int i = 0; i < MeshVariants; i++) meshes[i] = BuildSpearMesh(i);
        }

        // 高さ1の角ばった槍。下が岩、先が結晶。面ごとに頂点を分けて、ローポリの見た目にする。
        private static Mesh BuildSpearMesh(int seed)
        {
            const int Sides = 5;
            var random = new System.Random(seed + 7);
            float Jitter(float range) => (float)(random.NextDouble() * 2.0 - 1.0) * range;
            float[] heights = { -0.25f, 0.45f, 0.72f };
            float[] radii = { 0.62f, 0.34f, 0.2f };
            var rings = new Vector3[heights.Length][];
            for (int r = 0; r < heights.Length; r++)
            {
                rings[r] = new Vector3[Sides];
                Vector2 lean = new Vector2(Jitter(0.05f), Jitter(0.05f));
                for (int s = 0; s < Sides; s++)
                {
                    float angle = (s + Jitter(0.2f)) / Sides * Mathf.PI * 2f;
                    float radius = radii[r] * (1f + Jitter(0.2f));
                    rings[r][s] = new Vector3(Mathf.Cos(angle) * radius + lean.x, heights[r] + Jitter(0.02f), Mathf.Sin(angle) * radius + lean.y);
                }
            }
            var tip = new Vector3(Jitter(0.05f), 1f, Jitter(0.05f));

            var vertices = new List<Vector3>();
            var rock = new List<int>();
            var crystal = new List<int>();
            void Tri(List<int> list, Vector3 a, Vector3 b, Vector3 c)
            {
                list.Add(vertices.Count); vertices.Add(a);
                list.Add(vertices.Count); vertices.Add(b);
                list.Add(vertices.Count); vertices.Add(c);
            }
            for (int s = 0; s < Sides; s++)
            {
                int n = (s + 1) % Sides;
                for (int r = 0; r + 1 < heights.Length; r++)
                {
                    var list = r == 0 ? rock : crystal;
                    Tri(list, rings[r][s], rings[r + 1][s], rings[r + 1][n]);
                    Tri(list, rings[r][s], rings[r + 1][n], rings[r][n]);
                }
                Tri(crystal, rings[heights.Length - 1][s], tip, rings[heights.Length - 1][n]);
            }
            var mesh = new Mesh { name = "RockSpear" + seed, subMeshCount = 2 };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(rock, 0);
            mesh.SetTriangles(crystal, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
