using UnityEngine;

namespace Game.Skill
{
    /// 僧侶の回復・加護のやわらかい光。光の柱や輪のかわりに、ふわっと灯る光と、ゆっくり立ちのぼる粒で包む。
    ///  - Wrap: 相手の体を淡い光で包み、足元から粒がゆっくり昇る（相手に付いて動く）。
    ///  - Wave: 足元から外へ、光の粒が波のように広がって消える（範囲回復）。
    ///  - Orbs: 杖の先から相手へ、小さな光の玉がふわりと飛んでいく。
    public class GentleLight : MonoBehaviour
    {
        private const float BodyRadius = 0.55f;
        private const float ChestHeight = 1f;
        // 光の強さは 1 を下回ると明るい昼の地面ではほぼ見えず、2 を超えると白く飛ぶ。
        private const float AuraSize = 3f;
        private const float AuraStrength = 1.7f;
        private const int OrbCount = 3;
        private const float OrbStagger = 0.07f;
        private const float OrbSize = 0.5f;
        private const float OrbStrength = 2f;

        private enum Mode { Wrap, Orbs }

        private Mode mode;
        private float seconds;
        private float age;
        private Transform follow;
        private Vector3 from;
        private Color color;
        private float sizeScale;
        private MeshRenderer aura;
        private MeshRenderer[] orbs;
        private Vector3[] orbBends;
        private MaterialPropertyBlock block;

        /// 相手を淡い光で包む。layer を渡すと、光の本体をその層に置く（本人のカメラに映さないため）。
        public static void Wrap(Transform target, float sizeScale, float seconds, Color color, int layer = -1)
        {
            var go = new GameObject("GentleLight");
            go.transform.SetParent(target, false);
            var light = go.AddComponent<GentleLight>();
            light.mode = Mode.Wrap;
            light.seconds = seconds;
            light.color = color;
            light.sizeScale = sizeScale;
            light.block = new MaterialPropertyBlock();

            light.aura = FireballVfx.NewMeshObject("Aura", go.transform, FireballVfx.QuadMesh, FireballVfx.Glow);
            light.aura.transform.localPosition = Vector3.up * ChestHeight;
            if (layer >= 0) light.aura.gameObject.layer = layer;
            light.SetGlow(light.aura, 0f, 0f);

            // 足元の輪からゆっくり昇る粒。出はじめに多く、あとはまばらに。
            var motes = FireballVfx.NewParticles("Motes", go.transform);
            motes.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var main = motes.main;
            main.duration = seconds;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.11f * sizeScale, 0.24f * sizeScale);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, color);
            main.maxParticles = 160;
            var emission = motes.emission;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 70f * sizeScale, 1f, 10f * sizeScale));
            // かかった瞬間に分かるよう、最初にひとまとまり出す。
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.RoundToInt(16 * sizeScale)) });
            var shape = motes.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = BodyRadius * sizeScale;
            shape.radiusThickness = 0.5f;
            var noise = motes.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.6f;
            var size = motes.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0f)));
            motes.gameObject.SetActive(true);
            motes.Play();
            Destroy(go, seconds + 1.6f);
        }

        /// 足元から外へ広がる光の粒の波。
        public static void Wave(Vector3 center, float radius, float seconds, Color color)
        {
            var wave = FireballVfx.NewParticles("GentleWave", null);
            wave.transform.position = center + Vector3.up * 0.15f;
            wave.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            var main = wave.main;
            main.duration = 0.3f;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(seconds * 0.8f, seconds * 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(radius / seconds * 0.85f, radius / seconds * 1.05f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.24f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, color);
            main.gravityModifier = -0.04f;
            main.maxParticles = 260;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var emission = wave.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 120), new ParticleSystem.Burst(0.12f, 70) });
            var shape = wave.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.4f;
            shape.radiusThickness = 0f;
            var drag = wave.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 1.2f;
            drag.multiplyDragByParticleVelocity = false;
            var size = wave.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0f)));
            wave.gameObject.SetActive(true);
            wave.Play();
        }

        /// 杖の先から相手の胸へ、小さな光の玉を少しずつずらして飛ばす。
        public static void Orbs(Vector3 from, Transform target, float seconds, Color color, float sizeScale)
        {
            var go = new GameObject("GentleOrbs");
            var light = go.AddComponent<GentleLight>();
            light.mode = Mode.Orbs;
            light.seconds = seconds;
            light.follow = target;
            light.from = from;
            light.color = color;
            light.sizeScale = sizeScale;
            light.block = new MaterialPropertyBlock();
            light.orbs = new MeshRenderer[OrbCount];
            light.orbBends = new Vector3[OrbCount];
            for (int i = 0; i < OrbCount; i++)
            {
                light.orbs[i] = FireballVfx.NewMeshObject("Orb", go.transform, FireballVfx.QuadMesh, FireballVfx.Glow);
                // 玉ごとに違う向きへふくらむ弧を描く。
                light.orbBends[i] = (Vector3.up * Random.Range(0.3f, 0.8f) + Random.insideUnitSphere * 0.5f) * (i % 2 == 0 ? 1f : 0.6f);
                light.SetGlow(light.orbs[i], 0f, 0f);
            }
        }

        private void SetGlow(MeshRenderer glow, float size, float strength)
        {
            block.SetColor("_Color", color * strength);
            block.SetFloat("_Pull", 0.2f);
            glow.SetPropertyBlock(block);
            glow.transform.localScale = Vector3.one * size;
            glow.enabled = size > 0.001f && strength > 0.001f;
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (mode == Mode.Wrap)
            {
                if (aura == null) return;
                // ふわっと灯って、ゆっくり消える。
                float k = Mathf.Clamp01(age / seconds);
                float glow = k < 0.2f ? k / 0.2f : 1f - (k - 0.2f) / 0.8f;
                glow = glow * glow * (3f - 2f * glow);
                SetGlow(aura, AuraSize * sizeScale * (0.8f + 0.2f * glow), AuraStrength * glow);
                if (k >= 1f)
                {
                    Destroy(aura.gameObject);
                    aura = null;
                }
                return;
            }

            bool alive = false;
            Vector3 to = follow != null ? follow.position + Vector3.up * ChestHeight : from;
            for (int i = 0; i < orbs.Length; i++)
            {
                float k = (age - i * OrbStagger) / seconds;
                if (k < 0f) { alive = true; continue; }
                if (k >= 1f) { orbs[i].enabled = false; continue; }
                alive = true;
                float ease = k * k * (3f - 2f * k);
                orbs[i].transform.position = Vector3.Lerp(from, to, ease) + orbBends[i] * Mathf.Sin(k * Mathf.PI);
                SetGlow(orbs[i], OrbSize * sizeScale * (0.6f + 0.4f * Mathf.Sin(k * Mathf.PI)), OrbStrength);
            }
            if (!alive || follow == null) Destroy(gameObject);
        }
    }
}
