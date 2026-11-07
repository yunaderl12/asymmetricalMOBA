using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Skill;

namespace Game.Monster
{
    /// スコーピオンの爪が放つ X の形の斬撃。進む向きに垂直な面に2本の弧を交差させて、前へまっすぐ飛ぶ。
    /// 勇者は貫通して1人1回ずつ当たる。壁や柱に当たると消える。
    public class ScorpionCrossSlash : MonoBehaviour
    {
        private const int Segments = 18;
        private const float BladeWidth = 0.9f;
        private const float Bulge = 0.9f;        // 弧の中ほどが前へふくらむ量（m）
        private const float GrowSeconds = 0.12f;
        private const float HitDepth = 1.2f;
        private static readonly Color CoreColor = new Color(2.6f, 1.9f, 1.0f, 1f);
        private static readonly Color EdgeColor = new Color(1.8f, 0.45f, 0.12f, 1f);
        private static readonly Dictionary<float, Mesh> Meshes = new Dictionary<float, Mesh>();

        private GameObject owner;
        private ScorpionData data;
        private Vector3 direction;
        private int damage;
        private float traveled;
        private float age;
        private MeshRenderer ribbon;
        private MaterialPropertyBlock block;
        private readonly HashSet<HealthComponent> alreadyHit = new HashSet<HealthComponent>();

        public static void Spawn(GameObject owner, Vector3 center, Vector3 direction, int damage, ScorpionData data)
        {
            direction.y = 0f;
            var go = new GameObject("ScorpionCrossSlash");
            go.transform.SetPositionAndRotation(center, Quaternion.LookRotation(direction.normalized));
            var s = go.AddComponent<ScorpionCrossSlash>();
            s.owner = owner;
            s.data = data;
            s.direction = direction.normalized;
            s.damage = damage;
            s.ribbon = FireballVfx.NewMeshObject("Ribbon", go.transform, GetMesh(data.slashHalfSize), FireballVfx.GetMaterial("VFX/SwordArc"));
            s.block = new MaterialPropertyBlock();
            s.Apply();
            MeleeFx.Impact(center, -direction, 2.2f, MeleeFx.Steel);
        }

        private void Update()
        {
            float step = Mathf.Min(data.slashSpeed * Time.deltaTime, data.slashRange - traveled);
            Vector3 from = transform.position;
            Vector3 to = from + direction * step;
            bool stopped = Physics.Linecast(from, to, out var hit, ~0, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<HealthComponent>() == null;
            if (stopped) to = hit.point;
            transform.position = to;
            traveled += step;
            age += Time.deltaTime;
            HitHeroes(from, to);
            if (stopped || traveled >= data.slashRange)
            {
                if (stopped) MeleeFx.Impact(hit.point, hit.normal, 2f, MeleeFx.Steel);
                Destroy(gameObject);
                return;
            }
            Apply();
        }

        private void HitHeroes(Vector3 from, Vector3 to)
        {
            Quaternion inv = Quaternion.Inverse(transform.rotation);
            float travel = (to - from).magnitude;
            foreach (var hero in GameObject.FindGameObjectsWithTag("Player"))
            {
                var hp = hero.GetComponent<HealthComponent>();
                if (hp == null || hp.IsDead || alreadyHit.Contains(hp)) continue;
                Vector3 local = inv * (hero.transform.position + Vector3.up - from);
                if (local.z < -HitDepth || local.z > travel + HitDepth) continue;
                if (Mathf.Abs(local.x) > data.slashHalfSize || Mathf.Abs(local.y) > data.slashHalfSize + 1f) continue;
                alreadyHit.Add(hp);
                hp.TakeDamage(damage, owner, DamageType.Physical);
                MeleeFx.Impact(hero.transform.position + Vector3.up, -direction, 1.8f, MeleeFx.Steel);
                if (hp == null || hp.IsDead) continue;
                FireHitEffect.PlayFlash(hp.gameObject);
                ImpactPunch.Play(hp.gameObject, direction);
            }
        }

        private void Apply()
        {
            float grow = Mathf.Clamp01(age / GrowSeconds);
            transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1f, grow);
            float fade = Mathf.InverseLerp(0.7f, 1f, traveled / Mathf.Max(0.01f, data.slashRange));
            block.SetFloat("_Head", 1f);
            block.SetFloat("_Tail", fade * fade);
            block.SetColor("_Color", CoreColor);
            block.SetColor("_EdgeColor", EdgeColor);
            ribbon.SetPropertyBlock(block);
        }

        private static Mesh GetMesh(float halfSize)
        {
            if (Meshes.TryGetValue(halfSize, out var mesh) && mesh != null) return mesh;
            mesh = BuildMesh(halfSize);
            Meshes[halfSize] = mesh;
            return mesh;
        }

        // 2本の弧。uv.x は弧に沿った位置、uv.y は 0=後ろ側 1=前側（SwordArc の内側・外側）。
        private static Mesh BuildMesh(float h)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            var blades = new[] { (new Vector3(-h, -h, 0f), new Vector3(h, h, 0f)), (new Vector3(-h, h, 0f), new Vector3(h, -h, 0f)) };
            foreach (var (a, b) in blades)
            {
                int start = vertices.Count;
                Vector3 along = (b - a).normalized;
                Vector3 across = Vector3.Cross(Vector3.forward, along).normalized;
                for (int i = 0; i <= Segments; i++)
                {
                    float t = i / (float)Segments;
                    float swell = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * t)), 0.6f); // sin(π) はわずかに負になり、0.6乗が NaN になる
                    Vector3 c = Vector3.Lerp(a, b, t) + Vector3.forward * Bulge * Mathf.Sin(Mathf.PI * t);
                    Vector3 half = across * BladeWidth * 0.5f * swell;
                    vertices.Add(c - half - Vector3.forward * 0.25f * swell);
                    vertices.Add(c + half + Vector3.forward * 0.25f * swell);
                    uvs.Add(new Vector2(t, 0f));
                    uvs.Add(new Vector2(t, 1f));
                    if (i == 0) continue;
                    int k = start + i * 2;
                    triangles.AddRange(new[] { k - 2, k - 1, k, k, k - 1, k + 1 });
                }
            }
            var mesh = new Mesh { name = "ScorpionCrossSlash" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
