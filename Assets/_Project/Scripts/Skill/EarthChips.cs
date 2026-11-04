using System.Collections.Generic;
using UnityEngine;

namespace Game.Skill
{
    /// 地面を削ったときに飛び散る、角ばった土のかけら。放物線を描いて飛び、地面に落ちる前に小さくなって消える。
    public class EarthChips : MonoBehaviour
    {
        private const float Gravity = 16f;
        private const float MinSize = 0.14f;
        private const float MaxSize = 0.5f;
        private const float MinSpeed = 3.5f;
        private const float MaxSpeed = 7.5f;
        private const float LifeSeconds = 0.9f;
        private const float ShrinkFrom = 0.6f;

        private static readonly Color[] Palette =
        {
            new Color(0.5f, 0.35f, 0.22f),
            new Color(0.4f, 0.28f, 0.18f),
            new Color(0.32f, 0.22f, 0.16f),
            new Color(0.36f, 0.42f, 0.2f),
        };

        private class Chip
        {
            public Transform Transform;
            public Vector3 Velocity;
            public Vector3 Spin;
            public float Size;
        }

        private static Mesh shardMesh;

        // 面を分けた四面体。シーンをまたいで使い回す。
        internal static Mesh ShardMesh
        {
            get
            {
                if (shardMesh != null) return shardMesh;
                Vector3 a = new Vector3(0f, 0.75f, 0f), b = new Vector3(-0.6f, -0.3f, -0.4f), c = new Vector3(0.7f, -0.3f, -0.3f), d = new Vector3(0f, -0.2f, 0.7f);
                shardMesh = new Mesh { name = "EarthShard" };
                shardMesh.vertices = new[] { a, c, b, a, d, c, a, b, d, b, c, d };
                shardMesh.triangles = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
                shardMesh.RecalculateNormals();
                shardMesh.RecalculateBounds();
                return shardMesh;
            }
        }

        private readonly List<Chip> chips = new List<Chip>();
        private float timer;

        public static void Spawn(Vector3 center, float radius, int count)
        {
            var go = new GameObject("EarthChips");
            go.AddComponent<EarthChips>().Build(center, radius, count);
        }

        private void Build(Vector3 center, float radius, int count)
        {
            var material = FireballVfx.GetMaterial("VFX/VoxelChunk");
            var block = new MaterialPropertyBlock();
            for (int i = 0; i < count; i++)
            {
                var renderer = FireballVfx.NewMeshObject("Chip", transform, ShardMesh, material);
                Vector2 flat = Random.insideUnitCircle;
                Vector3 outward = new Vector3(flat.x, 0f, flat.y);
                float size = Random.Range(MinSize, MaxSize);
                renderer.transform.position = center + outward * radius + Vector3.up * 0.1f;
                renderer.transform.rotation = Random.rotation;
                renderer.transform.localScale = Vector3.one * size;
                block.SetColor("_Color", Palette[Random.Range(0, Palette.Length)]);
                block.SetFloat("_Fade", 1f);
                renderer.SetPropertyBlock(block);
                chips.Add(new Chip
                {
                    Transform = renderer.transform,
                    Velocity = (outward + Vector3.up * Random.Range(0.9f, 1.8f)).normalized * Random.Range(MinSpeed, MaxSpeed),
                    Spin = Random.insideUnitSphere * 600f,
                    Size = size,
                });
            }
        }

        private void Update()
        {
            timer += Time.deltaTime;
            if (timer >= LifeSeconds)
            {
                Destroy(gameObject);
                return;
            }
            float shrink = 1f - Mathf.Clamp01((timer / LifeSeconds - ShrinkFrom) / (1f - ShrinkFrom));
            foreach (var chip in chips)
            {
                chip.Velocity += Vector3.down * (Gravity * Time.deltaTime);
                chip.Transform.position += chip.Velocity * Time.deltaTime;
                chip.Transform.Rotate(chip.Spin * Time.deltaTime);
                chip.Transform.localScale = Vector3.one * (chip.Size * shrink);
            }
        }
    }
}
