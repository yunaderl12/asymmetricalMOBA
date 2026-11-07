using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Hero;
using Game.Skill;

namespace Game.DemonKing
{
    /// 雷。chargeSeconds かけて狙った場所の上に雷雲が集まり（中で稲光がちらつく、地面に予兆の輪）、雷が落ちる。
    /// 直撃（radius）は大ダメージ、その外側（outerRadius）は小〜中ダメージとスロー。豪雨で濡れた場所では wetDamageMultiplier 倍。
    public class FieldSpellLightning : FieldSpellEffect
    {
        private const float CloudHeight = 20f;
        private const int PuffCount = 26;
        private const float BoltSeconds = 0.35f;
        private const float AfterSeconds = 1.6f;
        private static readonly Color BoltColor = new Color(0.75f, 0.9f, 1f);

        private class Puff
        {
            public MeshRenderer Renderer;
            public Vector3 From, To;
            public float Size, Delay;
        }

        private readonly List<Puff> puffs = new List<Puff>();
        private MaterialPropertyBlock block;
        private FieldSpellRing inner, outer;
        private LineRenderer bolt;
        private Vector3 cloudCenter;
        private bool struck;
        private float nextFlicker;

        protected override void Begin()
        {
            block = new MaterialPropertyBlock();
            cloudCenter = point + Vector3.up * CloudHeight;
            float spread = Mathf.Max(data.outerRadius, data.radius) * 0.75f;
            for (int i = 0; i < PuffCount; i++)
            {
                var r = NewPuff(transform, new Color(0.58f, 0.59f, 0.66f), new Color(0.36f, 0.37f, 0.45f), new Color(0.12f, 0.12f, 0.17f), block);
                Vector2 ring = Random.insideUnitCircle * spread;
                Vector3 to = cloudCenter + new Vector3(ring.x, Random.Range(-0.6f, 1.2f), ring.y);
                // 雲は外から真ん中へ寄ってくる。
                Vector3 from = cloudCenter + new Vector3(ring.x, 0f, ring.y).normalized * (spread * 3f) + Vector3.up * 3f;
                r.transform.position = from;
                r.transform.localScale = Vector3.zero;
                puffs.Add(new Puff { Renderer = r, From = from, To = to, Size = Random.Range(5f, 8f), Delay = Random.Range(0f, 0.35f) });
            }
            inner = FieldSpellRing.Create(point, data.radius, new Color(1f, 0.95f, 0.5f, 0.95f));
            inner.pulse = 1f;
            outer = FieldSpellRing.Create(point, data.outerRadius, new Color(0.6f, 0.8f, 1f, 0.55f));
            outer.width = 0.2f;
        }

        protected override void Tick(float dt)
        {
            float charge = Mathf.Max(0.01f, data.chargeSeconds);
            float k = Mathf.Clamp01(time / charge);
            float after = time - charge;
            float dissolve = after > 0f ? Mathf.Clamp01((after - 0.3f) / (AfterSeconds - 0.3f)) : 0f;
            foreach (var p in puffs)
            {
                float g = Mathf.Clamp01((k - p.Delay) / Mathf.Max(0.05f, 0.8f - p.Delay));
                float eased = 1f - (1f - g) * (1f - g);
                p.Renderer.transform.position = Vector3.Lerp(p.From, p.To, eased) + Vector3.up * (0.3f * Mathf.Sin(time * 0.8f + p.Size));
                p.Renderer.transform.localScale = new Vector3(1f, 0.6f, 1f) * (p.Size * eased);
                p.Renderer.transform.Rotate(0f, 15f * dt, 0f, Space.World);
                SetDissolve(p.Renderer, dissolve, block);
            }

            if (!struck)
            {
                // 溜めの後半は雲の中で稲光がちらつく。
                if (k > 0.45f && time >= nextFlicker)
                {
                    nextFlicker = time + Random.Range(0.25f, 0.6f) * (1.2f - k);
                    Flash(cloudCenter + Random.insideUnitSphere * 3f, BoltColor, 3f + 6f * k, 30f);
                }
                if (k >= 1f) Strike();
                return;
            }

            if (bolt != null)
            {
                float b = Mathf.Clamp01(after / BoltSeconds);
                var c = BoltColor;
                c.a = 1f - b;
                bolt.startColor = c;
                bolt.endColor = c;
                if (b < 0.6f && Random.value < 0.5f) Jag();
                if (b >= 1f) Destroy(bolt.gameObject);
            }
            if (after >= AfterSeconds) Destroy(gameObject);
        }

        private void Strike()
        {
            struck = true;
            if (inner != null) Destroy(inner.gameObject);
            if (outer != null) Destroy(outer.gameObject);
            BuildBolt();
            Flash(point + Vector3.up * 3f, BoltColor, 14f, 40f);
            FireFlash.Spawn(point + Vector3.up * 0.5f, data.radius * 0.8f, 0.3f, 0f, new Color(1f, 1f, 1f), BoltColor, BoltColor, 8f);
            ShockRing.Spawn(point, 0.3f, data.outerRadius, 0.45f, 0.5f, new Color(0.6f, 0.85f, 1f, 1f));
            GroundCracks.Spawn(point, data.radius, 1.2f);
            HeroFPSController.ShakeNear(point, 0.3f, data.outerRadius * 4f, 0.45f);

            float wet = FieldSpellDownpour.IsWet(point) ? data.wetDamageMultiplier : 1f;
            foreach (var hp in new List<HealthComponent>(Heroes()))
            {
                float d = FlatDistance(hp.transform.position, point);
                if (d <= data.radius)
                {
                    Hit(hp, Mathf.RoundToInt(data.damage * wet), DamageType.Magical);
                    if (!hp.IsDead) StatusEffectController.Apply(hp.gameObject, StatusEffectType.Slow, data.slowSeconds, 1f - data.slowMultiplier, caster);
                }
                else if (d <= data.outerRadius)
                {
                    Hit(hp, Mathf.RoundToInt(data.outerDamage * wet), DamageType.Magical);
                    if (!hp.IsDead) StatusEffectController.Apply(hp.gameObject, StatusEffectType.Slow, data.slowSeconds, 1f - data.slowMultiplier, caster);
                }
                if (hp != null && !hp.IsDead && d <= data.outerRadius) FireballVfx.SpawnSparks(hp.transform.position + Vector3.up, Vector3.up, 10, Color.white, BoltColor);
            }
            if (wet > 1f) Debug.Log("[FieldSpell] lightning on wet ground x" + wet);
        }

        private void BuildBolt()
        {
            var go = new GameObject("LightningBolt");
            go.transform.SetParent(transform, false);
            bolt = go.AddComponent<LineRenderer>();
            bolt.useWorldSpace = true;
            bolt.positionCount = 14;
            bolt.widthCurve = new AnimationCurve(new Keyframe(0f, 0.9f), new Keyframe(1f, 0.5f));
            bolt.material = FireballVfx.Trail;
            bolt.numCapVertices = 2;
            bolt.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Jag();
        }

        /// 稲妻のギザギザを引き直す（光っているあいだ形がちらつく）。
        private void Jag()
        {
            int n = bolt.positionCount;
            Vector3 top = cloudCenter + Random.insideUnitSphere * 1.5f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / (n - 1);
                Vector3 p = Vector3.Lerp(top, point, t);
                if (i > 0 && i < n - 1) p += Random.insideUnitSphere.WithY0() * 1.4f;
                bolt.SetPosition(i, p);
            }
        }
    }
}
