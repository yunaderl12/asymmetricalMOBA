using System.Collections.Generic;
using UnityEngine;

namespace Game.Skill
{
    /// Explosion の爆発本体。白いドームと輪 → 根元の煙 → ドームが透けて消えたあと、黒いきのこ雲が立つ。
    public class ExplosionBurst : MonoBehaviour
    {
        private const float DomeGrowSeconds = 0.16f;
        private const float DomeCreepPerSecond = 0.05f;
        private const float DomeFadeStart = 1.4f;
        private const float DomeFadeSeconds = 0.4f;
        private const float RingStart = 0.2f;
        private const float BasePuffStart = 0.7f;
        private const float BasePuffGrowSeconds = 0.5f;
        private const float BasePuffEnd = 1.8f;
        private const float CloudStart = 1.55f;
        private const float CloudGrowSeconds = 0.9f;
        private const float CloudDissolveStart = 4.6f;
        private const float CloudDissolveSeconds = 1.1f;
        private const float CloudRisePerSecond = 0.5f;
        private const float StarInterval = 0.09f;
        private const float StarEnd = 0.55f;
        private const int BasePuffCount = 26;
        private const int StemPuffCount = 7;
        private const int HeadPuffCount = 26;
        private const int DebrisCount = 56;
        private const float DebrisLifeSeconds = 5f;
        private const float DebrisFadeSeconds = 0.7f;

        private static readonly Color RingColor = new Color(1f, 1f, 1f, 1f);
        private static readonly Color GroundWaveColor = new Color(0.65f, 1f, 0.55f, 1f);
        private static readonly Color StarColor = new Color(0.5f, 1.5f, 2f, 1f);

        private class Ring
        {
            public MeshRenderer Renderer;
            public float Height;
            public float StartRadius;
            public float EndRadius;
            public float Tilt;
        }

        private class Puff
        {
            public Transform Transform;
            public MeshRenderer Renderer;
            public Vector3 Start;
            public Vector3 End;
            public float Size;
            public float Delay;
        }

        private readonly List<Ring> rings = new List<Ring>();
        private readonly List<Puff> basePuffs = new List<Puff>();
        private readonly List<Puff> cloudPuffs = new List<Puff>();
        private MeshRenderer dome;
        private MaterialPropertyBlock block;
        private Vector3 center;
        private float radius;
        private float timer;
        private float nextStar;
        private bool cloudBuilt;

        public static void Spawn(Vector3 center, float radius)
        {
            var go = new GameObject("ExplosionBurst");
            go.transform.position = center;
            var burst = go.AddComponent<ExplosionBurst>();
            burst.center = center;
            burst.radius = radius;
            burst.block = new MaterialPropertyBlock();
            burst.Build();
        }

        private void Build()
        {
            ExplosionScreenFx.Play(center);
            FireFlash.Spawn(center + Vector3.up * radius * 0.4f, radius * 2.4f, 0.16f, 0f, new Color(3f, 3f, 3f), new Color(0.5f, 1.4f, 2f), new Color(0.6f, 0.9f, 1f), 8f);
            ShockRing.Spawn(center, radius * 0.8f, radius * 3f, 1.3f, radius * 1.1f, GroundWaveColor);
            ShockRing.Spawn(center, radius * 0.6f, radius * 2.2f, 0.7f, radius * 0.25f, Color.white);
            GroundCracks.Spawn(center, radius * 0.9f, DebrisLifeSeconds);
            VoxelDebris.Spawn(center, radius, DebrisCount, DebrisLifeSeconds, DebrisFadeSeconds);

            dome = FireballVfx.NewMeshObject("Dome", transform, FireballVfx.SphereMesh, FireballVfx.GetMaterial("VFX/ExplosionDome"));
            dome.transform.localScale = Vector3.zero;
            block.SetFloat("_Seed", Random.Range(0f, 50f));
            block.SetFloat("_Fade", 1f);
            dome.SetPropertyBlock(block);

            rings.Add(NewRing(radius * 0.38f, radius * 1.05f, radius * 1.42f, 3f));
            rings.Add(NewRing(radius * 1.22f, radius * 0.3f, radius * 0.72f, -2f));

            for (int i = 0; i < BasePuffCount; i++)
            {
                float angle = Mathf.PI * 2f * i / BasePuffCount + Random.Range(-0.12f, 0.12f);
                Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 start = center + outward * (radius * 0.92f);
                basePuffs.Add(NewPuff(start, start + outward * (radius * 0.14f) + Vector3.up * 0.4f, radius * Random.Range(0.08f, 0.17f), Random.Range(0f, 0.25f)));
            }
        }

        private Ring NewRing(float height, float startRadius, float endRadius, float tilt)
        {
            var renderer = FireballVfx.NewMeshObject("Ring", transform, FireballVfx.QuadMesh, FireballVfx.GetMaterial("VFX/ShockRing"));
            renderer.enabled = false;
            var ring = new Ring { Renderer = renderer, Height = height, StartRadius = startRadius, EndRadius = endRadius, Tilt = tilt };
            return ring;
        }

        private Puff NewPuff(Vector3 start, Vector3 end, float size, float delay)
        {
            var renderer = FireballVfx.NewMeshObject("Puff", transform, FireballVfx.SphereMesh, FireballVfx.GetMaterial("VFX/ToonSmoke"));
            renderer.transform.position = start;
            renderer.transform.localScale = Vector3.zero;
            renderer.transform.rotation = Random.rotation;
            block.SetFloat("_Seed", Random.Range(0f, 50f));
            block.SetFloat("_Dissolve", 0f);
            renderer.SetPropertyBlock(block);
            return new Puff { Transform = renderer.transform, Renderer = renderer, Start = start, End = end, Size = size, Delay = delay };
        }

        private void BuildCloud()
        {
            cloudBuilt = true;
            float stemTop = radius * 1.05f;
            for (int i = 0; i < StemPuffCount; i++)
            {
                float k = (float)i / (StemPuffCount - 1);
                Vector2 jitter = Random.insideUnitCircle * (radius * 0.07f);
                Vector3 end = center + new Vector3(jitter.x, stemTop * k, jitter.y);
                cloudPuffs.Add(NewPuff(center, end, radius * Mathf.Lerp(0.19f, 0.13f, k), k * 0.2f));
            }
            for (int i = 0; i < HeadPuffCount; i++)
            {
                Vector3 offset = Vector3.Scale(Random.onUnitSphere, new Vector3(radius * 0.62f, radius * 0.3f, radius * 0.62f));
                Vector3 end = center + Vector3.up * (radius * 1.38f) + offset;
                cloudPuffs.Add(NewPuff(center + Vector3.up * stemTop * 0.6f, end, radius * Random.Range(0.17f, 0.31f), Random.Range(0.1f, 0.3f)));
            }
        }

        private void Update()
        {
            timer += Time.deltaTime;
            UpdateStars();
            UpdateDome();
            UpdateRings();
            UpdateBasePuffs();
            if (timer >= CloudStart)
            {
                if (!cloudBuilt) BuildCloud();
                UpdateCloud();
            }
            if (timer >= CloudDissolveStart + CloudDissolveSeconds) Destroy(gameObject);
        }

        private void UpdateStars()
        {
            if (timer >= StarEnd || timer < nextStar) return;
            nextStar = timer + StarInterval;
            Vector2 flat = Random.insideUnitCircle * (radius * 0.8f);
            Vector3 position = center + new Vector3(flat.x, radius * Random.Range(1.1f, 1.8f), flat.y);
            SparkleStar.Spawn(position, Random.Range(1.6f, 3.4f), Random.Range(0.3f, 0.6f), Vector3.up * 1.5f);
        }

        private void UpdateDome()
        {
            if (dome == null) return;
            float fade = 1f - Mathf.Clamp01((timer - DomeFadeStart) / DomeFadeSeconds);
            if (fade <= 0f)
            {
                Destroy(dome.gameObject);
                dome = null;
                return;
            }
            float grow = 1f - Mathf.Pow(1f - Mathf.Clamp01(timer / DomeGrowSeconds), 3f);
            float size = radius * 2f * (grow + timer * DomeCreepPerSecond);
            dome.transform.localScale = Vector3.one * size;
            dome.GetPropertyBlock(block);
            block.SetFloat("_Fade", fade);
            dome.SetPropertyBlock(block);
        }

        private void UpdateRings()
        {
            float life = DomeFadeStart + DomeFadeSeconds - RingStart;
            float k = (timer - RingStart) / life;
            foreach (var ring in rings)
            {
                if (ring.Renderer == null) continue;
                if (k >= 1f)
                {
                    Destroy(ring.Renderer.gameObject);
                    ring.Renderer = null;
                    continue;
                }
                if (k < 0f) continue;
                ring.Renderer.enabled = true;
                float eased = 1f - (1f - k) * (1f - k);
                float r = Mathf.Lerp(ring.StartRadius, ring.EndRadius, eased);
                float width = Mathf.Lerp(0.13f, 0.03f, Mathf.Clamp01((timer - DomeFadeStart) / DomeFadeSeconds + k * 0.4f));
                float scale = r * 2f * 1.1f;
                ring.Renderer.transform.SetPositionAndRotation(center + Vector3.up * ring.Height, Quaternion.Euler(90f + ring.Tilt, 0f, ring.Tilt));
                ring.Renderer.transform.localScale = Vector3.one * scale;
                var c = RingColor;
                c.a = 1f - Mathf.Clamp01((timer - DomeFadeStart) / DomeFadeSeconds);
                block.Clear();
                block.SetColor("_Color", c);
                block.SetFloat("_Ring", 1f / 1.1f);
                block.SetFloat("_Width", width);
                ring.Renderer.SetPropertyBlock(block);
            }
        }

        private void UpdateBasePuffs()
        {
            float dissolve = Mathf.Clamp01((timer - DomeFadeStart) / (BasePuffEnd - DomeFadeStart));
            foreach (var puff in basePuffs)
            {
                if (puff.Renderer == null) continue;
                if (dissolve >= 1f)
                {
                    Destroy(puff.Renderer.gameObject);
                    puff.Renderer = null;
                    continue;
                }
                float k = Mathf.Clamp01((timer - BasePuffStart - puff.Delay) / BasePuffGrowSeconds);
                AnimatePuff(puff, k, dissolve);
            }
        }

        private void UpdateCloud()
        {
            float dissolve = Mathf.Clamp01((timer - CloudDissolveStart) / CloudDissolveSeconds);
            foreach (var puff in cloudPuffs)
            {
                float k = Mathf.Clamp01((timer - CloudStart - puff.Delay) / CloudGrowSeconds);
                AnimatePuff(puff, k, dissolve);
                puff.Transform.position += Vector3.up * (CloudRisePerSecond * Mathf.Max(0f, timer - CloudStart - CloudGrowSeconds) * 0.1f);
            }
        }

        private void AnimatePuff(Puff puff, float k, float dissolve)
        {
            float eased = 1f - (1f - k) * (1f - k) * (1f - k);
            puff.Transform.position = Vector3.Lerp(puff.Start, puff.End, eased);
            puff.Transform.localScale = Vector3.one * (puff.Size * 2f * eased);
            puff.Renderer.GetPropertyBlock(block);
            block.SetFloat("_Dissolve", dissolve);
            puff.Renderer.SetPropertyBlock(block);
        }
    }
}
