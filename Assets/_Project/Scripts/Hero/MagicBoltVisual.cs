using UnityEngine;
using Game.Skill;

namespace Game.Hero
{
    /// 魔法使いの通常攻撃の見た目。中心が白く外周が紺色の魔力の球で、紺色の魔力を軌跡に残す。
    public class MagicBoltVisual : MonoBehaviour
    {
        private const float OrbDiameter = 0.34f;
        private const float GlowSize = 0.95f;
        private const float TrailSeconds = 0.4f;
        private const float TrailWidth = 0.34f;
        private const float WispsPerMeter = 7f;
        private const float PulseSpeed = 30f;

        private static readonly Color Navy = new Color(0.06f, 0.11f, 0.5f, 1f);
        private static readonly Color DeepNavy = new Color(0.03f, 0.05f, 0.3f, 1f);
        private static readonly Color GlowColor = new Color(0.05f, 0.12f, 0.9f, 0.9f);

        private Transform orb;
        private TrailRenderer trail;
        private ParticleSystem wisps;
        private float baseScale;
        private float age;

        /// parent の大きさに関係なく、世界の大きさで作る。
        public static MagicBoltVisual Attach(GameObject parent)
        {
            var root = new GameObject("MagicBoltVisual");
            root.transform.SetParent(parent.transform, false);
            float inverse = 1f / Mathf.Max(parent.transform.lossyScale.x, 0.001f);
            root.transform.localScale = Vector3.one * inverse;
            var visual = root.AddComponent<MagicBoltVisual>();
            visual.Build();
            return visual;
        }

        private void Build()
        {
            var orbRenderer = FireballVfx.NewMeshObject("Orb", transform, FireballVfx.SphereMesh, FireballVfx.GetMaterial("VFX/MagicOrb"));
            orb = orbRenderer.transform;
            baseScale = OrbDiameter;
            orb.localScale = Vector3.one * baseScale;

            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", GlowColor);
            block.SetFloat("_Pull", 0f);
            var glow = FireballVfx.NewMeshObject("Glow", transform, FireballVfx.QuadMesh, FireballVfx.Glow);
            glow.transform.localScale = Vector3.one * GlowSize;
            glow.SetPropertyBlock(block);

            var trailObject = new GameObject("Trail");
            trailObject.transform.SetParent(transform, false);
            trail = trailObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = FireballVfx.GetMaterial("VFX/MagicTrail");
            trail.time = TrailSeconds;
            trail.minVertexDistance = 0.05f;
            trail.widthMultiplier = TrailWidth;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.colorGradient = FireballVfx.FadeGradient(Navy, DeepNavy);
            trail.textureMode = LineTextureMode.Stretch;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;

            wisps = BuildWisps();
        }

        // 軌跡からこぼれる魔力の粒。進んだ距離に応じて出す。
        private ParticleSystem BuildWisps()
        {
            var ps = FireballVfx.NewParticles("Wisps", transform);
            var main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
            main.startColor = new ParticleSystem.MinMaxGradient(Navy, GlowColor);
            main.maxParticles = 64;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.rateOverDistance = WispsPerMeter;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.08f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            ps.gameObject.SetActive(true);
            ps.Play();
            return ps;
        }

        private void Update()
        {
            age += Time.deltaTime;
            float pulse = 1f + Mathf.Sin(age * PulseSpeed) * 0.07f;
            if (orb != null) orb.localScale = Vector3.one * (baseScale * pulse);
        }

        /// 弾が消えても軌跡と粒は自然に消えるまで残す。
        public void Release()
        {
            if (trail != null)
            {
                trail.transform.SetParent(null, true);
                trail.autodestruct = true;
                trail.emitting = false;
                trail = null;
            }
            if (wisps != null)
            {
                wisps.transform.SetParent(null, true);
                wisps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                wisps = null;
            }
        }

        public static void Impact(Vector3 point, Vector3 direction)
        {
            FireFlash.Spawn(point, 1.8f, 0.14f, 0.8f, new Color(2.4f, 2.6f, 3f), new Color(0.1f, 0.25f, 1.4f), new Color(0.4f, 0.5f, 1f), 2f);
            FireballVfx.SpawnSparks(point, -direction, 10, Color.white, new Color(0.2f, 0.35f, 1f));
        }
    }
}
