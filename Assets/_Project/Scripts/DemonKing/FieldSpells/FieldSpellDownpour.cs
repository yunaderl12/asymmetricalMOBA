using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Skill;

namespace Game.DemonKing
{
    /// 豪雨。狙った場所のバイオーム全体の上に雨雲が集まり（chargeSeconds）、duration 秒の大雨になる。
    /// 雨のあいだ、そのバイオームにいる勇者は遅くなり（slowMultiplier）、左右に向きが入れ替わる横風（windPush m/s）に押される。
    /// 地面は濡れ、雨がやんでも wetAfterSeconds のあいだ濡れたまま。濡れている場所では雷のダメージが増える（IsWet）。
    public class FieldSpellDownpour : FieldSpellEffect
    {
        private const int CloudCount = 22;
        private const float CloudHeight = 30f;
        private const float StatusStep = 0.25f;
        private const float WindPeriod = 6f;
        private const float ClearSeconds = 2.5f;

        /// バイオームごとの「濡れている」期限（Time.time）。
        private static readonly Dictionary<StageMap.Biome, float> WetUntil = new Dictionary<StageMap.Biome, float>();

        /// その場所の地面が豪雨で濡れているか。
        public static bool IsWet(Vector3 position)
        {
            var biome = StageMap.BiomeAt(position);
            return biome != StageMap.Biome.None && WetUntil.TryGetValue(biome, out var until) && Time.time < until;
        }

        private class Cloud
        {
            public MeshRenderer Renderer;
            public Vector3 At;
            public float Size, Delay;
        }

        private readonly List<Cloud> clouds = new List<Cloud>();
        private MaterialPropertyBlock block;
        private StageMap.Biome biome;
        private ParticleSystem rain;
        private Vector3 windBase;
        private float nextStatus;
        private bool raining;
        private float dim;

        private void OnDestroy()
        {
            if (dim > 0f) SkyController.ExtraStorm = 0f;
        }

        protected override void Begin()
        {
            biome = StageMap.BiomeAt(point);
            if (biome == StageMap.Biome.None)
            {
                Debug.Log("[FieldSpell] downpour needs a biome");
                Destroy(gameObject);
                return;
            }
            block = new MaterialPropertyBlock();
            for (int i = 0; i < CloudCount; i++)
            {
                Vector3 at = StageMap.RandomPointIn(biome);
                at.y = GroundY(at.x, at.z) + CloudHeight + Random.Range(-3f, 4f);
                var r = NewPuff(transform, new Color(0.45f, 0.47f, 0.55f), new Color(0.24f, 0.26f, 0.33f), new Color(0.07f, 0.08f, 0.12f), block);
                r.transform.position = at;
                r.transform.localScale = Vector3.zero;
                clouds.Add(new Cloud { Renderer = r, At = at, Size = Random.Range(12f, 20f), Delay = Random.Range(0f, 0.5f) });
            }
            windBase = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            rain = BuildRain();
            Game.UI.MatchNotice.Show(BiomeName(biome) + "の空が暗くなってきた…", 2.5f);
        }

        private static string BiomeName(StageMap.Biome b) => b == StageMap.Biome.Mountain ? "山" : b == StageMap.Biome.Forest ? "森" : b == StageMap.Biome.Desert ? "砂漠" : "";

        protected override void Tick(float dt)
        {
            if (block == null) return;
            float charge = Mathf.Max(0.01f, data.chargeSeconds);
            float rainTime = time - charge;
            float clear = rainTime - data.duration;
            if (clear >= ClearSeconds)
            {
                Destroy(gameObject);
                return;
            }

            float gather = Mathf.Clamp01(time / charge);
            float dissolve = clear > 0f ? Mathf.Clamp01(clear / ClearSeconds) : 0f;
            foreach (var c in clouds)
            {
                float g = Mathf.Clamp01((gather - c.Delay) / Mathf.Max(0.05f, 1f - c.Delay));
                c.Renderer.transform.localScale = new Vector3(1f, 0.55f, 1f) * (c.Size * (1f - (1f - g) * (1f - g)));
                c.Renderer.transform.position = c.At + Wind() * (time * 0.15f);
                SetDissolve(c.Renderer, dissolve, block);
            }

            bool nowRaining = rainTime >= 0f && clear < 0f;
            if (nowRaining && !raining)
            {
                raining = true;
                WetUntil[biome] = Time.time + data.duration + data.wetAfterSeconds;
                Game.UI.MatchNotice.Show(BiomeName(biome) + "に豪雨", 2.5f);
            }
            raining = nowRaining;
            UpdateRain(nowRaining);
            if (!nowRaining) return;

            Vector3 wind = Wind();
            bool refresh = time >= nextStatus;
            if (refresh) nextStatus = time + StatusStep;
            foreach (var hp in Heroes())
            {
                if (StageMap.BiomeAt(hp.transform.position) != biome) continue;
                if (refresh) StatusEffectController.Apply(hp.gameObject, StatusEffectType.Slow, StatusStep * 2.5f, 1f - data.slowMultiplier, caster);
                Push(hp.gameObject, wind * dt);
            }
        }

        /// 横風（左右に入れ替わる）。
        private Vector3 Wind()
        {
            Vector3 side = Vector3.Cross(Vector3.up, windBase);
            return side * (data.windPush * Mathf.Sin(time * Mathf.PI * 2f / WindPeriod));
        }

        private static void Push(GameObject go, Vector3 step)
        {
            if (step.sqrMagnitude < 1e-8f) return;
            var agent = go.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.Move(step);
                return;
            }
            var cc = go.GetComponent<CharacterController>();
            if (cc != null && cc.enabled) cc.Move(step);
        }

        // ---- 雨 ----

        private ParticleSystem BuildRain()
        {
            var ps = FireballVfx.NewParticles("Rain", transform);
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            var mat = new Material(FireballVfx.Particle) { name = "Rain" };
            mat.SetFloat("_Boost", 1.8f);
            renderer.sharedMaterial = mat;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.05f;
            renderer.lengthScale = 1f;
            var main = ps.main;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = 0.9f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.13f);
            main.startColor = new Color(0.85f, 0.9f, 1f, 0.85f);
            main.maxParticles = 4000;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(44f, 1f, 44f);
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0f);
            velocity.y = new ParticleSystem.MinMaxCurve(-30f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f);
            var emission = ps.emission;
            emission.rateOverTime = 2600f;
            ps.gameObject.SetActive(true);
            return ps;
        }

        /// 雨はカメラのまわりにだけ降らせる（カメラがそのバイオームにいるときだけ）。
        private void UpdateRain(bool nowRaining)
        {
            var cam = Camera.main;
            if (rain == null) return;
            bool show = nowRaining && cam != null && StageMap.BiomeAt(cam.transform.position) == biome;
            // カメラが雨の中にいるあいだは空と光を嵐の色にする。
            dim = Mathf.MoveTowards(dim, show ? 0.85f : 0f, Time.deltaTime * 0.5f);
            SkyController.ExtraStorm = dim;
            if (show)
            {
                Vector3 fwd = cam.transform.forward.WithY0();
                rain.transform.position = cam.transform.position + Vector3.up * 14f + fwd * 10f;
                rain.transform.rotation = Quaternion.identity;
                var velocity = rain.velocityOverLifetime;
                Vector3 wind = Wind() * 2f;
                velocity.x = new ParticleSystem.MinMaxCurve(wind.x);
                velocity.z = new ParticleSystem.MinMaxCurve(wind.z);
                if (!rain.isPlaying) rain.Play();
            }
            else if (rain.isPlaying) rain.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
