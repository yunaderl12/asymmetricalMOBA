using System;
using UnityEngine;
using Game.Core;
using Game.Skill;

namespace Game.Monster
{
    /// 魔物の出現演出（地面側）。警告の円 → 闇のポータルが開く → 魔物が出ている間は炎を噴く → 閉じて消える。
    /// ポータルが開ききった瞬間に onOpened を呼ぶので、魔物はそこで生成する。
    public class SpawnPortal : MonoBehaviour
    {
        private enum Phase { Warn, Open, Emerge, Close }

        private const float CloseSeconds = 0.35f;
        private const float ParticleLinger = 0.9f;
        private const float GroundLift = 0.05f;

        private Phase phase = Phase.Warn;
        private float timer;
        private float radius;
        private bool burstDone;
        private Action onOpened;
        private MeshRenderer disc;
        private MaterialPropertyBlock block;
        private Light portalLight;
        private ParticleSystem pullEmbers;
        private ParticleSystem flames;
        private ParticleSystem smoke;

        /// まだ魔物を生成していなければ true（スポナーが壊れたときに取り消せる）。
        public bool IsBeforeOpen => phase == Phase.Warn || phase == Phase.Open;

        public static SpawnPortal Begin(Vector3 position, float radius, Action onOpened)
        {
            // 起伏に沿わせて地面のすぐ上に置く。
            Vector3 up = Vector3.up;
            if (Physics.Raycast(position + Vector3.up, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore))
            {
                position = hit.point;
                up = hit.normal;
            }
            var go = new GameObject("SpawnPortal");
            go.transform.SetPositionAndRotation(position + up * GroundLift, Quaternion.FromToRotation(Vector3.up, up));
            var portal = go.AddComponent<SpawnPortal>();
            portal.radius = radius;
            portal.onOpened = onOpened;
            portal.Build();
            return portal;
        }

        /// 魔物が出る前なら、そのまま閉じて消す。
        public void Cancel()
        {
            if (!IsBeforeOpen) return;
            onOpened = null;
            EnterClose();
        }

        private void Build()
        {
            block = new MaterialPropertyBlock();
            disc = FireballVfx.NewMeshObject("Disc", transform, FireballVfx.QuadMesh, FireballVfx.GetMaterial("VFX/SpawnPortal"));
            disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            portalLight = gameObject.AddComponent<Light>();
            portalLight.type = LightType.Point;
            portalLight.color = new Color(1f, 0.32f, 0.08f);
            portalLight.range = radius * 3.5f;
            portalLight.intensity = 0f;

            BuildPullEmbers();
            BuildFlames();
            BuildSmoke();
            Apply(0f, 0f, 1f, 0.55f);
        }

        // ポータルへ吸い込まれる火の粉。
        private void BuildPullEmbers()
        {
            pullEmbers = FireballVfx.NewParticles("PullEmbers", transform);
            var main = pullEmbers.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = 0.55f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.35f), new Color(1f, 0.35f, 0.08f));
            main.maxParticles = 60;
            var emission = pullEmbers.emission;
            emission.rateOverTime = 55f;
            var shape = pullEmbers.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius * 1.15f;
            shape.radiusThickness = 0f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            var velocity = pullEmbers.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.radial = -radius / 0.55f;
            velocity.orbitalY = 4f;
            var r = pullEmbers.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.05f;
            r.lengthScale = 1.5f;
        }

        // 魔物と一緒に噴き上がる地獄の炎。
        private void BuildFlames()
        {
            flames = FireballVfx.NewParticles("HellFlames", transform);
            var main = flames.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.6f, 0.2f), new Color(1f, 0.28f, 0.05f));
            main.gravityModifier = -0.2f;
            main.maxParticles = 80;
            var emission = flames.emission;
            emission.rateOverTime = 50f;
            var shape = flames.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = radius * 0.6f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var size = flames.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            var color = flames.colorOverLifetime;
            color.enabled = true;
            color.color = FireballVfx.FadeGradient(Color.white, new Color(0.7f, 0.1f, 0.02f));
        }

        private void BuildSmoke()
        {
            smoke = FireballVfx.NewParticles("HellSmoke", transform);
            var main = smoke.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startColor = new Color(0.05f, 0.025f, 0.06f, 0.7f);
            main.maxParticles = 40;
            var emission = smoke.emission;
            emission.rateOverTime = 20f;
            var shape = smoke.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 15f;
            shape.radius = radius * 0.7f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var color = smoke.colorOverLifetime;
            color.enabled = true;
            color.color = FireballVfx.FadeGradient(Color.white, Color.white);
        }

        private void Update()
        {
            var cfg = BalanceConfig.Instance;
            timer += Time.deltaTime;
            switch (phase)
            {
                case Phase.Warn: UpdateWarn(cfg.spawnWarnSeconds); break;
                case Phase.Open: UpdateOpen(cfg.spawnPortalSeconds); break;
                case Phase.Emerge: UpdateEmerge(cfg.spawnEmergeSeconds); break;
                case Phase.Close: UpdateClose(); break;
            }
        }

        // 円がふわっと広がって、点滅しながら出現地点を知らせる。
        private void UpdateWarn(float seconds)
        {
            float appear = Mathf.Clamp01(timer / 0.3f);
            float pulse = 1f - 0.3f * (0.5f + 0.5f * Mathf.Sin(timer * 11f));
            Apply(appear * pulse, 0f, 1f, Mathf.Lerp(0.55f, 1f, EaseOutBack(appear)));
            if (timer >= seconds)
            {
                phase = Phase.Open;
                timer = 0f;
                pullEmbers.gameObject.SetActive(true);
                pullEmbers.Play();
            }
        }

        // 中心から闇が開き、周りの火の粉を吸い込む。
        private void UpdateOpen(float seconds)
        {
            float t = Mathf.Clamp01(timer / seconds);
            Apply(1f - t, EaseOutBack(t), 1f, 1f);
            portalLight.intensity = 3f * t;
            if (t < 1f) return;

            phase = Phase.Emerge;
            timer = 0f;
            flames.gameObject.SetActive(true);
            flames.Play();
            smoke.gameObject.SetActive(true);
            smoke.Play();
            var opened = onOpened;
            onOpened = null;
            opened?.Invoke();
        }

        private void UpdateEmerge(float seconds)
        {
            Apply(0f, 1f, 1f, 1f);
            portalLight.intensity = 3f + Mathf.Sin(timer * 30f) * 0.8f;
            // 魔物が飛び出す瞬間に火花と閃光を合わせる。
            if (!burstDone && timer >= seconds * MonsterEmerge.BurstRatio)
            {
                burstDone = true;
                Vector3 center = transform.position + transform.up * 0.2f;
                FireballVfx.SpawnSparks(center, transform.up, 28);
                FireFlash.Spawn(center, radius * 2f, 0.18f, 0.5f);
            }
            if (timer >= seconds) EnterClose();
        }

        private void EnterClose()
        {
            phase = Phase.Close;
            timer = 0f;
            pullEmbers.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            flames.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        // 閉じた後も、噴き上げた炎と煙が消えるまでは残す。
        private void UpdateClose()
        {
            float t = Mathf.Clamp01(timer / CloseSeconds);
            Apply(0f, 1f - t * t, 1f, 1f);
            portalLight.intensity = 3f * (1f - t);
            if (t >= 1f) disc.enabled = false;
            if (timer >= CloseSeconds + ParticleLinger) Destroy(gameObject);
        }

        private void Apply(float warn, float open, float fade, float scale)
        {
            disc.transform.localScale = Vector3.one * (radius * 2f * scale);
            block.SetFloat("_Warn", warn);
            block.SetFloat("_Open", open);
            block.SetFloat("_Fade", fade);
            disc.SetPropertyBlock(block);
        }

        private static float EaseOutBack(float t)
        {
            float u = t - 1f;
            return 1f + 2.2f * u * u * u + 1.2f * u * u;
        }
    }
}
