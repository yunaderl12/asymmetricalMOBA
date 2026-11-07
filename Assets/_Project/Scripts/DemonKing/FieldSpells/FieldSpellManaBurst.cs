using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Hero;
using Game.Skill;

namespace Game.DemonKing
{
    /// 魔力暴発。狙った場所に魔力の球を置き、fuseSeconds（ランダム）で爆発して radius 内の勇者に大ダメージ。
    /// 球には HP（orbHp）があり、攻撃されるとその場で爆発する（近くで殴ると巻き込まれる）。爆発が近づくほど速く脈打つ。
    public class FieldSpellManaBurst : FieldSpellEffect
    {
        private const float OrbHeight = 1.4f;
        private const float OrbRadius = 1f;
        private static readonly Color Core = new Color(0.85f, 0.55f, 1f);
        private static readonly Color Edge = new Color(0.45f, 0.1f, 0.75f);

        private HealthComponent orb;
        private Transform body;
        private MeshRenderer glow;
        private Light lamp;
        private FieldSpellRing ring;
        private MaterialPropertyBlock block;
        private float fuse;
        private bool burst;

        protected override void Begin()
        {
            fuse = Random.Range(Mathf.Min(data.fuseSeconds.x, data.fuseSeconds.y), Mathf.Max(data.fuseSeconds.x, data.fuseSeconds.y)) + data.chargeSeconds;
            block = new MaterialPropertyBlock();

            var orbGo = new GameObject("ManaBurstOrb");
            orbGo.transform.SetParent(transform, false);
            orbGo.transform.position = point + Vector3.up * OrbHeight;
            body = orbGo.transform;
            var col = orbGo.AddComponent<SphereCollider>();
            col.radius = OrbRadius;
            orb = orbGo.AddComponent<HealthComponent>();
            orb.SetMaxHP(Mathf.Max(1, data.orbHp));
            orb.OnHPChanged += OnOrbHit;

            var shell = FireballVfx.NewMeshObject("Shell", body, FireballVfx.SphereMesh, FireballVfx.GetMaterial("VFX/EnergyFilm"));
            shell.transform.localScale = Vector3.one * (OrbRadius * 2f);
            var shellBlock = new MaterialPropertyBlock();
            shellBlock.SetColor("_Color", new Color(1.1f, 0.45f, 1.8f, 0.75f));
            shellBlock.SetFloat("_Hex", 0.5f);
            shell.SetPropertyBlock(shellBlock);
            glow = FireballVfx.NewMeshObject("Glow", body, FireballVfx.QuadMesh, FireballVfx.Glow);
            glow.gameObject.AddComponent<FieldSpellBillboard>();
            block.SetColor("_Color", Core);
            glow.SetPropertyBlock(block);
            glow.transform.localScale = Vector3.one * (OrbRadius * 4f);

            lamp = new GameObject("Light").AddComponent<Light>();
            lamp.transform.SetParent(body, false);
            lamp.type = LightType.Point;
            lamp.color = Core;
            lamp.range = 10f;
            lamp.intensity = 3f;

            ring = FieldSpellRing.Create(point, data.radius, new Color(Core.r, Core.g, Core.b, 0.75f));
            ring.pulse = 0.5f;
            RuneFlash.Spawn(point + Vector3.up * 0.1f, Quaternion.identity, data.radius * 0.5f, 0.6f, Core);
        }

        private void OnOrbHit(int current, int max)
        {
            if (current < max) Burst();
        }

        protected override void Tick(float dt)
        {
            if (burst) return;
            float left = fuse - time;
            if (left <= 0f)
            {
                Burst();
                return;
            }
            // 残りが少ないほど速く、大きく脈打つ。
            float rate = Mathf.Lerp(3f, 16f, 1f - Mathf.Clamp01(left / Mathf.Max(0.1f, fuse)));
            float beat = 0.5f + 0.5f * Mathf.Sin(time * rate);
            body.localScale = Vector3.one * (1f + 0.12f * beat);
            body.position = point + Vector3.up * (OrbHeight + 0.15f * Mathf.Sin(time * 2f));
            lamp.intensity = 2f + 4f * beat;
            ring.pulse = Mathf.Lerp(0.3f, 1f, 1f - Mathf.Clamp01(left / 3f));
            if (Random.value < dt * rate) FireballVfx.SpawnSparks(body.position, Random.onUnitSphere, 3, Core, Edge);
        }

        private void Burst()
        {
            if (burst) return;
            burst = true;
            if (orb != null) orb.OnHPChanged -= OnOrbHit;
            Vector3 at = body.position;
            if (ring != null) Destroy(ring.gameObject);
            Destroy(body.gameObject);

            ExplosionBurst.Spawn(point, data.radius * 0.8f, true);
            FireFlash.Spawn(at, data.radius * 0.7f, 0.4f, 0f, Color.white, Core, Core, 10f);
            ShockRing.Spawn(point, 0.5f, data.radius * 1.2f, 0.5f, 0.6f, new Color(Core.r, Core.g, Core.b, 1f));
            HeroFPSController.ShakeNear(point, 0.35f, data.radius * 6f, 0.5f);
            foreach (var hp in new List<HealthComponent>(Heroes()))
            {
                Vector3 p = hp.transform.position;
                if (Vector3.Distance(p + Vector3.up, at) > data.radius) continue;
                Hit(hp, data.damage, DamageType.Magical);
                if (!hp.IsDead) SkillKnockback.Play(hp.gameObject, FlatDirection(at, p), 3f);
            }
            Destroy(gameObject, 0.1f);
        }
    }

    /// いつもカメラの方を向く板（光の玉の輝き）。
    public class FieldSpellBillboard : MonoBehaviour
    {
        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }
    }
}
