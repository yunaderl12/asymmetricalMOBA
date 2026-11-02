using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;

namespace Game.Skill
{
    /// Blizzard の本体。氷柱の列が足元から狙った地点へ走り、先へ行くほど大きくなる。
    /// 着弾点で氷の塊が立ち上がり、閃光と氷の地面を残す。そのあと氷柱は先端から溶けて砕ける。
    public class BlizzardStrike : MonoBehaviour
    {
        private struct Spike
        {
            public Transform transform;
            public MeshRenderer renderer;
            public Vector3 scale;
            public float start;
            public float melt;
            public bool shattered;
        }

        private const float StartOffset = 1f;
        private const float LineStep = 0.65f;
        private const float GrowSeconds = 0.12f;
        private const float HoldSeconds = 0.55f;
        private const float MeltSeconds = 0.45f;
        private const float MeltStagger = 0.25f;
        private const float ShardLifeSeconds = 0.9f;
        private const float BackOvershoot = 1.7f;
        private const float LineHeightNear = 1f;
        private const float LineHeightFar = 3.4f;
        private const float SideHeightRatio = 0.65f;
        private const float WidthPerHeight = 0.26f;
        private const float FanStart = 0.4f;

        private static readonly Color IcePale = new Color(0.75f, 0.95f, 1f);
        private static readonly Color IceBlue = new Color(0.25f, 0.65f, 1f);
        private static Mesh[] spikeMeshes;

        private readonly List<Spike> spikes = new List<Spike>();
        private MaterialPropertyBlock block;
        private ParticleSystem shards;
        private Vector3 target;
        private float radius;
        private float fieldSeconds;
        private Action<Vector3> onImpact;
        private float timer;
        private float impactTime;
        private bool impacted;
        private float endTime;

        public static void Spawn(Vector3 from, Vector3 target, float radius, float fieldSeconds, Action<Vector3> onImpact)
        {
            var go = new GameObject("BlizzardStrike");
            go.transform.position = target;
            var strike = go.AddComponent<BlizzardStrike>();
            strike.target = target;
            strike.radius = radius;
            strike.fieldSeconds = fieldSeconds;
            strike.onImpact = onImpact;
            strike.block = new MaterialPropertyBlock();
            strike.Build(from);
        }

        private void Build(Vector3 from)
        {
            Vector3 flat = target - from;
            flat.y = 0f;
            float distance = flat.magnitude;
            Vector3 forward = distance > 0.01f ? flat / distance : transform.forward;
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            impactTime = distance / Mathf.Max(1f, BalanceConfig.Instance.blizzardSpikeSpeed);

            for (float d = StartOffset; d < distance - 0.5f; d += LineStep)
            {
                float n = d / Mathf.Max(0.01f, distance);
                float time = d / Mathf.Max(1f, BalanceConfig.Instance.blizzardSpikeSpeed);
                float height = Mathf.Lerp(LineHeightNear, LineHeightFar, n) * UnityEngine.Random.Range(0.85f, 1.15f);
                Vector3 point = from + forward * d;
                AddSpike(point, forward, UnityEngine.Random.Range(-6f, 14f), height, time);
                // 両脇の小さな氷柱は外側へ傾く。
                for (int s = -1; s <= 1; s += 2)
                {
                    float offset = UnityEngine.Random.Range(0.3f, 0.9f) * (0.4f + n);
                    Vector3 sidePoint = point + side * (s * offset);
                    Vector3 lean = (side * s + forward * 0.35f).normalized;
                    AddSpike(sidePoint, lean, UnityEngine.Random.Range(18f, 40f), height * SideHeightRatio * UnityEngine.Random.Range(0.7f, 1.1f), time + 0.02f);
                    // 先へ行くほど幅も広げ、扇形に開いて見せる。
                    if (n > FanStart) AddSpike(point + side * (s * offset * 2f), lean, UnityEngine.Random.Range(35f, 55f), height * SideHeightRatio * 0.7f, time + 0.04f);
                }
            }

            // 着弾点の氷の塊。内側ほど高く、外側ほど寝る。
            float ring = radius * 0.3f;
            AddSpike(target, forward, 4f, 4.8f, impactTime);
            AddClusterRing(9, ring * 0.45f, 3f, 4.2f, 12f, 26f);
            AddClusterRing(13, ring, 1.5f, 2.8f, 34f, 56f);

            CreateShards();
            endTime = impactTime + HoldSeconds + MeltStagger + MeltSeconds + ShardLifeSeconds;
        }

        private void AddClusterRing(int count, float ringRadius, float minHeight, float maxHeight, float minTilt, float maxTilt)
        {
            float offset = UnityEngine.Random.value * 360f;
            for (int i = 0; i < count; i++)
            {
                float angle = (offset + i * 360f / count + UnityEngine.Random.Range(-10f, 10f)) * Mathf.Deg2Rad;
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                AddSpike(target + outward * ringRadius, outward, UnityEngine.Random.Range(minTilt, maxTilt), UnityEngine.Random.Range(minHeight, maxHeight), impactTime + UnityEngine.Random.Range(0f, 0.05f));
            }
        }

        private void AddSpike(Vector3 flatPosition, Vector3 leanDirection, float tilt, float height, float start)
        {
            Vector3 ground = IceField.SnapToGround(flatPosition);
            float width = height * WidthPerHeight * UnityEngine.Random.Range(0.8f, 1.2f);
            var renderer = FireballVfx.NewMeshObject("Spike", transform, GetSpikeMesh(UnityEngine.Random.Range(0, 3)), FireballVfx.GetMaterial("VFX/IceSpike"));
            Vector3 axis = Vector3.Cross(Vector3.up, leanDirection);
            Quaternion rotation = (axis.sqrMagnitude > 0.0001f ? Quaternion.AngleAxis(tilt, axis.normalized) : Quaternion.identity)
                * Quaternion.Euler(0f, UnityEngine.Random.value * 360f, 0f);
            renderer.transform.SetPositionAndRotation(ground - Vector3.up * 0.05f, rotation);
            renderer.transform.localScale = Vector3.zero;
            renderer.enabled = false;
            spikes.Add(new Spike
            {
                transform = renderer.transform,
                renderer = renderer,
                scale = new Vector3(width, height, width),
                start = start,
                melt = impactTime + HoldSeconds + UnityEngine.Random.value * MeltStagger
            });
        }

        private void Update()
        {
            timer += Time.deltaTime;
            if (!impacted && timer >= impactTime) Impact();
            for (int i = 0; i < spikes.Count; i++)
            {
                var spike = spikes[i];
                if (timer < spike.start) continue;
                spike.renderer.enabled = timer < spike.melt + MeltSeconds;
                if (!spike.renderer.enabled) continue;
                float grow = Mathf.Clamp01((timer - spike.start) / GrowSeconds);
                spike.transform.localScale = spike.scale * BackOut(grow);
                if (timer >= spike.melt)
                {
                    if (!spike.shattered)
                    {
                        spike.shattered = true;
                        spikes[i] = spike;
                        EmitShards(spike);
                    }
                    block.SetFloat("_Melt", (timer - spike.melt) / MeltSeconds);
                    spike.renderer.SetPropertyBlock(block);
                }
            }
            if (timer >= endTime) Destroy(gameObject);
        }

        private void Impact()
        {
            impacted = true;
            FireFlash.Spawn(target + Vector3.up * 1.4f, 7f, 0.26f, 0.8f, new Color(1.5f, 2.4f, 2.8f), new Color(0.2f, 0.9f, 1.8f), IcePale, 2f);
            ShockRing.Spawn(target, 0.5f, radius * 1.15f, 0.42f, 1.1f, IcePale);
            FireballVfx.SpawnSparks(target + Vector3.up * 0.3f, Vector3.up, 46, IcePale, IceBlue);
            // ひび割れた地面から氷のかけらが跳ねる。
            for (int i = 0; i < 16; i++)
            {
                Vector2 c = UnityEngine.Random.insideUnitCircle * radius * 0.7f;
                var shard = new ParticleSystem.EmitParams
                {
                    position = target + new Vector3(c.x, 0.1f, c.y),
                    velocity = new Vector3(c.x, UnityEngine.Random.Range(3f, 7f), c.y),
                    startSize = UnityEngine.Random.Range(0.1f, 0.3f)
                };
                shards.Emit(shard, 1);
            }
            IceField.Spawn(target, radius, fieldSeconds);
            onImpact?.Invoke(target);
        }

        private void EmitShards(Spike spike)
        {
            int count = spike.scale.y > 2.5f ? 5 : 2;
            for (int i = 0; i < count; i++)
            {
                Vector3 position = spike.transform.position + spike.transform.up * (spike.scale.y * UnityEngine.Random.value);
                var shard = new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = UnityEngine.Random.insideUnitSphere * 3f + Vector3.up * 1.5f,
                    startSize = UnityEngine.Random.Range(0.08f, 0.26f)
                };
                shards.Emit(shard, 1);
            }
        }

        private void CreateShards()
        {
            shards = FireballVfx.NewParticles("IceShards", transform);
            var main = shards.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, ShardLifeSeconds);
            main.startSpeed = 0f;
            main.gravityModifier = 1.6f;
            main.maxParticles = 400;
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var r = shards.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = GetSpikeMesh(0);
            r.sharedMaterial = FireballVfx.GetMaterial("VFX/IceSpike");
            shards.gameObject.SetActive(true);
            shards.Play();
        }

        private static float BackOut(float t)
        {
            float p = t - 1f;
            return 1f + (BackOvershoot + 1f) * p * p * p + BackOvershoot * p * p;
        }

        private static Mesh GetSpikeMesh(int variant)
        {
            if (spikeMeshes == null) spikeMeshes = new Mesh[3];
            if (spikeMeshes[variant] == null) spikeMeshes[variant] = BuildSpikeMesh(variant);
            return spikeMeshes[variant];
        }

        // 底が高さ0、先端が高さ1、幅1の尖った結晶。面ごとに頂点を分けて平面シェーディングにする。
        private static Mesh BuildSpikeMesh(int seed)
        {
            const int sides = 5;
            var random = new System.Random(seed + 11);
            float Jitter(float size) => ((float)random.NextDouble() * 2f - 1f) * size;

            var bottom = new Vector3[sides];
            var middle = new Vector3[sides];
            float midHeight = 0.42f + Jitter(0.1f);
            for (int i = 0; i < sides; i++)
            {
                float angle = (i + Jitter(0.2f)) * Mathf.PI * 2f / sides;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                bottom[i] = dir * (0.5f + Jitter(0.1f));
                middle[i] = dir * (0.36f + Jitter(0.08f)) + Vector3.up * (midHeight + Jitter(0.06f));
            }
            var tip = new Vector3(Jitter(0.07f), 1f, Jitter(0.07f));

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                triangles.Add(vertices.Count); vertices.Add(a);
                triangles.Add(vertices.Count); vertices.Add(b);
                triangles.Add(vertices.Count); vertices.Add(c);
            }
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                Tri(bottom[i], middle[next], bottom[next]);
                Tri(bottom[i], middle[i], middle[next]);
                Tri(middle[i], tip, middle[next]);
            }
            var mesh = new Mesh { name = "IceSpike" + seed };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
