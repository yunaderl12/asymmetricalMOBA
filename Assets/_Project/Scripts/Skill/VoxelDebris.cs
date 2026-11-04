using System.Collections.Generic;
using UnityEngine;

namespace Game.Skill
{
    /// Explosion で吹き飛ぶボクセルの破片。最初はゆっくり宙に舞い、そのあと地面へ落ちて弾む。一定時間で透明になって消える。
    public class VoxelDebris : MonoBehaviour
    {
        private const float Gravity = 20f;
        private const float HangSeconds = 1.1f;
        private const float HangSpeedScale = 0.22f;
        private const float BounceScale = 0.3f;
        private const float RestSpeed = 1.5f;
        private const float BigChunkRatio = 0.15f;

        private static readonly Color[] Palette =
        {
            new Color(0.38f, 0.62f, 0.25f, 1f),
            new Color(0.46f, 0.34f, 0.22f, 1f),
            new Color(0.36f, 0.27f, 0.18f, 1f),
            new Color(0.45f, 0.45f, 0.49f, 1f),
        };

        private class Chunk
        {
            public Transform Transform;
            public MeshRenderer Renderer;
            public Vector3 Velocity;
            public Vector3 Spin;
            public float HalfSize;
            public bool Resting;
        }

        private readonly List<Chunk> chunks = new List<Chunk>();
        private MaterialPropertyBlock block;
        private float age;
        private float lifeSeconds;
        private float fadeSeconds;

        public static void Spawn(Vector3 center, float radius, int count, float lifeSeconds, float fadeSeconds)
        {
            var go = new GameObject("VoxelDebris");
            var debris = go.AddComponent<VoxelDebris>();
            debris.lifeSeconds = lifeSeconds;
            debris.fadeSeconds = fadeSeconds;
            debris.block = new MaterialPropertyBlock();
            debris.Build(center, radius, count);
        }

        private void Build(Vector3 center, float radius, int count)
        {
            var shard = EarthChips.ShardMesh;
            var material = FireballVfx.GetMaterial("VFX/VoxelChunk");

            for (int i = 0; i < count; i++)
            {
                var renderer = FireballVfx.NewMeshObject("Chunk", transform, shard, material);
                bool big = i < count * BigChunkRatio;
                float size = big ? Random.Range(1.6f, 2.8f) : Random.Range(0.25f, 0.7f);
                Vector2 flat = Random.insideUnitCircle.normalized;
                Vector3 outward = new Vector3(flat.x, 0f, flat.y);
                // 大きい破片はドームの表面に見えるように、小さい破片は根元から飛ばす。
                Vector3 surface = (outward + Vector3.up * Random.Range(0.2f, 1.2f)).normalized * (radius * 1.08f);
                renderer.transform.position = big ? center + surface : center + outward * (radius * Random.Range(0.8f, 1.05f)) + Vector3.up * Random.Range(0.4f, 2f);
                renderer.transform.rotation = Random.rotation;
                renderer.transform.localScale = big
                    ? new Vector3(size, size * Random.Range(0.25f, 0.5f), size * Random.Range(0.5f, 0.8f))
                    : new Vector3(size, size * Random.Range(0.6f, 1f), size * Random.Range(0.7f, 1.1f));

                block.SetColor("_Color", Palette[Random.Range(0, Palette.Length)]);
                block.SetFloat("_Fade", 1f);
                renderer.SetPropertyBlock(block);

                chunks.Add(new Chunk
                {
                    Transform = renderer.transform,
                    Renderer = renderer,
                    Velocity = outward * Random.Range(big ? 6f : 4f, 15f) + Vector3.up * Random.Range(big ? 8f : 9f, big ? 16f : 26f),
                    Spin = Random.insideUnitSphere * (big ? 70f : 260f),
                    HalfSize = size * 0.5f,
                });
            }
        }

        private void Update()
        {
            age += Time.deltaTime;
            float dt = Time.deltaTime * Mathf.Lerp(HangSpeedScale, 1f, Mathf.Clamp01((age - HangSeconds) / 0.35f));
            float fade = 1f - Mathf.Clamp01((age - lifeSeconds) / fadeSeconds);
            foreach (var chunk in chunks)
            {
                if (!chunk.Resting) Move(chunk, dt);
                if (fade < 1f)
                {
                    chunk.Renderer.GetPropertyBlock(block);
                    block.SetFloat("_Fade", fade);
                    chunk.Renderer.SetPropertyBlock(block);
                }
            }
            if (fade <= 0f) Destroy(gameObject);
        }

        private void Move(Chunk chunk, float dt)
        {
            chunk.Velocity += Vector3.down * (Gravity * dt);
            Vector3 step = chunk.Velocity * dt;
            if (chunk.Velocity.y < 0f && Physics.Raycast(chunk.Transform.position, Vector3.down, out var hit, -step.y + chunk.HalfSize, ~0, QueryTriggerInteraction.Ignore))
            {
                chunk.Transform.position = new Vector3(chunk.Transform.position.x, hit.point.y + chunk.HalfSize, chunk.Transform.position.z);
                chunk.Velocity = new Vector3(chunk.Velocity.x * 0.5f, -chunk.Velocity.y * BounceScale, chunk.Velocity.z * 0.5f);
                chunk.Spin *= 0.5f;
                if (chunk.Velocity.magnitude < RestSpeed) chunk.Resting = true;
                return;
            }
            chunk.Transform.position += step;
            chunk.Transform.Rotate(chunk.Spin * dt, Space.Self);
        }
    }
}
