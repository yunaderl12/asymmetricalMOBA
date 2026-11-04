using UnityEngine;
using Game.Skill;

namespace Game.Hero
{
    /// 魔法使いの通常攻撃の見た目。白い芯・マゼンタ・青・紺が重なる涙滴形の炎で、丸い根元を先頭に尾をうねらせて飛ぶ。
    public class MagicBoltVisual : MonoBehaviour
    {
        private const float FlameLength = 1.25f;
        private const float GlowSize = 1.15f;
        private const float CoreGlowSize = 0.42f;
        private const int CoreGlowQueue = 3020;
        private const float TrailSeconds = 0.14f;
        private const float TrailWidth = 0.3f;
        private const float WispsPerMeter = 6f;

        private static readonly Color Navy = new Color(0.05f, 0.08f, 0.55f, 1f);
        private static readonly Color DeepNavy = new Color(0.02f, 0.03f, 0.3f, 1f);
        private static readonly Color GlowColor = new Color(0.45f, 0.1f, 0.9f, 0.7f);
        private static readonly Color CoreGlowColor = new Color(1.6f, 0.75f, 1.4f, 0.9f);
        private static readonly Color Magenta = new Color(1f, 0.25f, 0.8f, 1f);

        private static Material coreGlowMaterial;

        // 炎より後に描いて、どの向きでも芯が見えるようにする。
        private static Material CoreGlowMaterial
        {
            get
            {
                if (coreGlowMaterial == null) coreGlowMaterial = new Material(FireballVfx.Glow) { renderQueue = CoreGlowQueue };
                return coreGlowMaterial;
            }
        }

        private TrailRenderer trail;
        private ParticleSystem wisps;

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
            var flame = FireballVfx.NewMeshObject("Flame", transform, FlameMesh.Get(), FireballVfx.GetMaterial("VFX/MagicFlame"));
            flame.transform.localScale = Vector3.one * FlameLength;
            // 向きと揺れの位相を弾ごとに変えて、同じ形が続かないようにする。
            flame.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            var flameBlock = new MaterialPropertyBlock();
            flameBlock.SetFloat("_Seed", Random.Range(0f, 100f));
            flame.SetPropertyBlock(flameBlock);

            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", GlowColor);
            block.SetFloat("_Pull", 0f);
            var glow = FireballVfx.NewMeshObject("Glow", transform, FireballVfx.QuadMesh, FireballVfx.Glow);
            glow.transform.localScale = Vector3.one * GlowSize;
            glow.SetPropertyBlock(block);

            // 後ろから見ると尾しか見えないので、芯の光を別に置く。
            var coreBlock = new MaterialPropertyBlock();
            coreBlock.SetColor("_Color", CoreGlowColor);
            coreBlock.SetFloat("_Pull", 0f);
            var core = FireballVfx.NewMeshObject("CoreGlow", transform, FireballVfx.QuadMesh, CoreGlowMaterial);
            core.transform.localScale = Vector3.one * CoreGlowSize;
            core.SetPropertyBlock(coreBlock);

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
            main.startColor = new ParticleSystem.MinMaxGradient(Magenta, new Color(0.15f, 0.25f, 1f, 1f));
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
            FireFlash.Spawn(point, 1.9f, 0.14f, 0.8f, new Color(2.8f, 2.4f, 3f), new Color(1.4f, 0.15f, 1.1f), new Color(0.3f, 0.4f, 1f), 2f);
            FireballVfx.SpawnSparks(point, -direction, 12, Color.white, Magenta);
        }
    }
}
