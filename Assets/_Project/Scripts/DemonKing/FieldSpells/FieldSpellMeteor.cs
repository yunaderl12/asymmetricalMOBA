using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Hero;
using Game.Skill;

namespace Game.DemonKing
{
    /// 隕石。狙った場所の周り（scatterRadius）に count 個の隕石が、chargeSeconds のあと duration 秒かけてばらばらに降る。
    /// 落ちる場所には先に赤い輪が出る。直撃（radius）は大ダメージで、落ちた所にクレーター（crater = 半径, 深さ）ができる。
    public class FieldSpellMeteor : FieldSpellEffect
    {
        private const float FallSeconds = 1.2f;
        private const float FallHeight = 55f;
        private const float FallSide = 22f;
        private static readonly Color WarnColor = new Color(1f, 0.35f, 0.15f, 0.95f);

        private class Rock
        {
            public Transform Body;
            public FieldSpellRing Ring;
            public TrailRenderer Trail;
            public Light Glow;
            public Vector3 From, To;
            public float Start;
        }

        private readonly List<Rock> rocks = new List<Rock>();
        private readonly List<float> schedule = new List<float>();
        private FieldSpellRing area;
        private Vector3 fallDirection;

        protected override void Begin()
        {
            // 狙った範囲の輪（降り終わるまで残す）。
            area = FieldSpellRing.Create(point, data.scatterRadius, new Color(1f, 0.55f, 0.3f, 0.5f));
            area.width = 0.25f;
            fallDirection = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            int count = Mathf.Max(1, data.count);
            for (int i = 0; i < count; i++)
                schedule.Add(data.chargeSeconds + data.duration * (count == 1 ? 0f : (i + Random.Range(-0.3f, 0.3f)) / (count - 1)));
            schedule.Sort();
        }

        protected override void Tick(float dt)
        {
            // 落ちる FallSeconds 前に輪を出し、上空から隕石を落とす。
            while (schedule.Count > 0 && time >= schedule[0] - FallSeconds)
            {
                schedule.RemoveAt(0);
                Launch();
            }
            for (int i = rocks.Count - 1; i >= 0; i--)
            {
                var r = rocks[i];
                float k = (time - r.Start) / FallSeconds;
                if (k >= 1f)
                {
                    Impact(r);
                    rocks.RemoveAt(i);
                    continue;
                }
                r.Body.position = Vector3.Lerp(r.From, r.To, k * k);
                r.Body.Rotate(400f * dt, 250f * dt, 0f, Space.Self);
                r.Ring.radius = Mathf.Lerp(data.radius * 0.3f, data.radius, k);
                r.Ring.alpha = Mathf.Lerp(0.4f, 1f, k);
                if (r.Glow != null) r.Glow.intensity = Mathf.Lerp(1f, 6f, k);
            }
            if (schedule.Count == 0 && rocks.Count == 0)
            {
                if (area != null) Destroy(area.gameObject);
                Destroy(gameObject, 0.1f);
            }
        }

        private void Launch()
        {
            Vector2 offset = Random.insideUnitCircle * data.scatterRadius;
            Vector3 to = Ground(point + new Vector3(offset.x, 0f, offset.y));
            Vector3 from = to + Vector3.up * FallHeight - fallDirection * FallSide + Random.insideUnitSphere * 4f;
            var body = new GameObject("Meteor").transform;
            body.SetParent(transform, false);
            body.position = from;
            var rock = NewRock(body, data.radius * 0.45f, new Color(0.25f, 0.13f, 0.08f));
            rock.localPosition = Vector3.zero;

            var trail = body.gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.45f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, data.radius * 0.55f), new Keyframe(1f, 0f));
            trail.material = FireballVfx.Trail;
            trail.colorGradient = FireballVfx.FadeGradient(new Color(1f, 0.75f, 0.3f), new Color(0.8f, 0.15f, 0.05f));
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(body, false);
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.5f, 0.2f);
            glow.range = 14f;
            glow.intensity = 1f;

            var ring = FieldSpellRing.Create(to, data.radius * 0.3f, WarnColor);
            ring.pulse = 1f;
            rocks.Add(new Rock { Body = body, Ring = ring, Trail = trail, Glow = glow, From = from, To = to, Start = time });
        }

        private void Impact(Rock r)
        {
            Vector3 at = r.To;
            Destroy(r.Ring.gameObject);
            // 尾は少し残してから消す。
            r.Body.SetParent(null, true);
            foreach (Transform child in r.Body) Destroy(child.gameObject);
            Destroy(r.Body.gameObject, r.Trail.time);

            ExplosionBurst.Spawn(at, data.radius, true);
            if (data.crater.x > 0f && data.crater.y > 0f) Game.Core.GroundDig.Dig(at, data.crater.x, data.crater.y);
            EarthChips.Spawn(at, data.radius * 0.6f, 10);
            Flash(at + Vector3.up * 2f, new Color(1f, 0.6f, 0.25f), 8f, data.radius * 6f);
            HeroFPSController.ShakeNear(at, 0.3f, data.radius * 8f, 0.45f);
            foreach (var hp in new List<HealthComponent>(Heroes()))
            {
                Vector3 p = hp.transform.position;
                if (FlatDistance(p, at) > data.radius || Mathf.Abs(p.y - at.y) > data.radius + 2f) continue;
                Hit(hp, data.damage, DamageType.Physical);
                if (!hp.IsDead) SkillKnockback.Play(hp.gameObject, FlatDirection(at, p), 2.5f);
            }
        }
    }
}
