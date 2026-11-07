using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Hero;
using Game.Skill;

namespace Game.DemonKing
{
    /// 竜巻。溜めのあいだは足元で土ぼこりが低く渦を巻き（予兆の輪つき）、chargeSeconds 後に大きな竜巻に育つ。
    /// 本番中はゆっくり動き回り、中に入った勇者にダメージを与えて上へ打ち上げる（BOT は打ち上げの代わりにスタンして外へ飛ばされる）。
    /// 竜巻は岩を巻き上げて周りへ投げ、当たった勇者に追加ダメージ。
    public class FieldSpellTornado : FieldSpellEffect
    {
        private const float Height = 15f;
        private const int PuffCount = 60;
        private const int OrbitRockCount = 7;
        private const float GrowSeconds = 0.7f;
        private const float FadeSeconds = 1f;
        private const float DriftSpeed = 1.3f;
        private const float RehitSeconds = 1.5f;
        private const float BotStunSeconds = 1.1f;

        private class Puff
        {
            public MeshRenderer Renderer;
            public float Angle, Rise, Speed, Size;
        }

        private class OrbitRock
        {
            public Transform Rock;
            public float Angle, Height, Speed;
        }

        private readonly List<Puff> puffs = new List<Puff>();
        private readonly List<OrbitRock> rocks = new List<OrbitRock>();
        private readonly Dictionary<HealthComponent, float> nextHit = new Dictionary<HealthComponent, float>();
        private MaterialPropertyBlock block;
        private FieldSpellRing ring;
        private Vector3 center;
        private Vector3 drift;
        private bool started;
        private int thrown;

        protected override void Begin()
        {
            center = point;
            block = new MaterialPropertyBlock();
            ring = FieldSpellRing.Create(point, data.radius, new Color(0.8f, 0.95f, 1f, 0.9f));
            ring.pulse = 1f;
            for (int i = 0; i < PuffCount; i++)
            {
                var r = NewPuff(transform, new Color(0.78f, 0.74f, 0.66f), new Color(0.5f, 0.46f, 0.4f), new Color(0.16f, 0.13f, 0.1f), block);
                r.transform.localScale = Vector3.zero;
                puffs.Add(new Puff { Renderer = r, Angle = Random.value * Mathf.PI * 2f, Rise = (float)i / PuffCount, Speed = Random.Range(0.8f, 1.2f), Size = Random.Range(0.8f, 1.2f) });
            }
            for (int i = 0; i < OrbitRockCount; i++)
            {
                var rock = NewRock(transform, Random.Range(0.35f, 0.7f), new Color(0.36f, 0.3f, 0.24f));
                rock.gameObject.SetActive(false);
                rocks.Add(new OrbitRock { Rock = rock, Angle = Random.value * Mathf.PI * 2f, Height = Random.Range(0.15f, 0.6f), Speed = Random.Range(2.2f, 3.4f) });
            }
            drift = Random.insideUnitSphere.WithY0().normalized;
        }

        protected override void Tick(float dt)
        {
            float charge = Mathf.Max(0.01f, data.chargeSeconds);
            float active = time - charge;
            float end = data.duration;
            if (active >= end + FadeSeconds)
            {
                Destroy(gameObject);
                return;
            }

            // 育ち具合: 溜めは低い渦（〜0.22）、本番で一気に伸びる。
            float build = active < 0f ? 0.22f * Mathf.SmoothStep(0f, 1f, time / charge) : Mathf.Lerp(0.22f, 1f, Mathf.SmoothStep(0f, 1f, active / GrowSeconds));
            float fade = active > end ? Mathf.Clamp01((active - end) / FadeSeconds) : 0f;

            if (active >= 0f && !started) StartTornado();
            if (started && active < end) Move(dt);
            AnimatePuffs(dt, build, fade);
            AnimateRocks(dt, build, fade);

            if (!started || active >= end) return;
            HitHeroes();
            // 岩は本番のあいだに均等に投げる。
            int due = Mathf.Min(data.debrisCount, Mathf.FloorToInt(active / end * (data.debrisCount + 0.5f)) + 1);
            while (thrown < due)
            {
                Throw();
                thrown++;
            }
        }

        private void StartTornado()
        {
            started = true;
            if (ring != null) Destroy(ring.gameObject);
            HeroFPSController.ShakeNear(center, 0.25f, data.radius * 5f, 0.6f);
            ShockRing.Spawn(center, 0.5f, data.radius * 1.6f, 0.5f, 0.6f, new Color(0.85f, 0.8f, 0.7f, 0.9f));
            EarthChips.Spawn(center, data.radius * 0.6f, 10);
            foreach (var r in rocks) r.Rock.gameObject.SetActive(true);
        }

        private void Move(float dt)
        {
            // ゆっくり向きを変えながら進む。深い水の上には出ない。
            drift = Quaternion.Euler(0f, Mathf.Sin(time * 0.7f) * 40f * dt, 0f) * drift;
            Vector3 next = center + drift * (DriftSpeed * dt);
            if (Game.Core.StageMap.IsDeepWater(next) || FlatDistance(next, point) > data.radius * 2.5f)
            {
                drift = FlatDirection(next, point);
                return;
            }
            next.y = Mathf.Lerp(center.y, GroundY(next.x, next.z, center.y), 0.2f);
            center = next;
        }

        private void AnimatePuffs(float dt, float build, float fade)
        {
            float height = Height * build;
            float top = Mathf.Lerp(1.4f, data.radius * 1.15f, build);
            foreach (var p in puffs)
            {
                p.Rise += dt * 0.22f * p.Speed;
                if (p.Rise > 1f) p.Rise -= 1f;
                float f = p.Rise;
                p.Angle += dt * Mathf.Lerp(6.5f, 3.2f, f) * p.Speed;
                float radius = Mathf.Lerp(0.5f, top, Mathf.Pow(f, 1.5f));
                Vector3 pos = center + new Vector3(Mathf.Cos(p.Angle) * radius, f * height + 0.3f, Mathf.Sin(p.Angle) * radius);
                // 上ほど少し流される。
                pos += new Vector3(Mathf.Sin(time * 0.9f), 0f, Mathf.Cos(time * 0.7f)) * (f * f * 1.2f * build);
                p.Renderer.transform.position = pos;
                float size = Mathf.Lerp(0.8f, 2.3f, f) * p.Size * Mathf.Lerp(0.6f, 1f, build);
                // 一番下と一番上でふわっと出入りする。
                size *= Mathf.Clamp01(f * 8f) * Mathf.Clamp01((1f - f) * 5f);
                p.Renderer.transform.localScale = Vector3.one * size;
                p.Renderer.transform.Rotate(0f, 120f * dt, 0f, Space.World);
                SetDissolve(p.Renderer, fade, block);
            }
        }

        private void AnimateRocks(float dt, float build, float fade)
        {
            if (!started) return;
            foreach (var r in rocks)
            {
                r.Angle += dt * r.Speed;
                float h = r.Height * Height * build;
                float radius = Mathf.Lerp(0.8f, data.radius * 0.9f, r.Height);
                r.Rock.position = center + new Vector3(Mathf.Cos(r.Angle) * radius, h, Mathf.Sin(r.Angle) * radius);
                r.Rock.Rotate(300f * dt, 200f * dt, 0f, Space.Self);
                r.Rock.gameObject.SetActive(fade < 0.5f);
            }
        }

        private void HitHeroes()
        {
            foreach (var hp in Heroes())
            {
                Vector3 at = hp.transform.position;
                if (FlatDistance(at, center) > data.radius || at.y < center.y - 3f || at.y > center.y + Height) continue;
                if (nextHit.TryGetValue(hp, out var next) && time < next) continue;
                nextHit[hp] = time + RehitSeconds;
                Hit(hp, data.damage, DamageType.Magical);
                if (hp == null || hp.IsDead) continue;
                Vector3 away = FlatDirection(center, at);
                var human = HumanOf(hp);
                if (human != null)
                {
                    human.Launch(data.launchSpeed);
                    human.Shake(0.3f, 0.5f);
                    SkillKnockback.Play(hp.gameObject, away, 2.5f);
                }
                else
                {
                    StatusEffectController.Apply(hp.gameObject, StatusEffectType.Stun, BotStunSeconds, 0f, caster);
                    SkillKnockback.Play(hp.gameObject, away, 5f);
                }
            }
        }

        private void Throw()
        {
            Vector3 dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            // 近くに勇者がいれば、半分はそちらへ投げる。
            var heroes = Heroes();
            if (heroes.Count > 0 && Random.value < 0.5f)
            {
                var target = heroes[Random.Range(0, heroes.Count)];
                if (FlatDistance(target.transform.position, center) < data.scatterRadius * 1.5f)
                    dir = Quaternion.Euler(0f, Random.Range(-12f, 12f), 0f) * FlatDirection(center, target.transform.position);
            }
            Vector3 start = center + Vector3.up * Random.Range(2f, Height * 0.5f) + dir * (data.radius * 0.6f);
            float speed = Random.Range(0.7f, 1f) * Mathf.Max(6f, data.scatterRadius * 1.1f);
            Vector3 velocity = dir * speed + Vector3.up * Random.Range(3f, 6f);
            FieldSpellDebris.Spawn(caster, start, velocity, data.debrisDamage, Random.Range(0.45f, 0.75f));
        }
    }

    /// 竜巻が投げる岩。放物線で飛び、勇者に当たると追加ダメージ。地面に落ちると砕ける。
    public class FieldSpellDebris : MonoBehaviour
    {
        private const float Gravity = 14f;
        private const float HitRadius = 0.7f;
        private const float LifeSeconds = 5f;

        private GameObject owner;
        private Vector3 velocity;
        private Vector3 spin;
        private int damage;
        private float age;

        public static void Spawn(GameObject owner, Vector3 start, Vector3 velocity, int damage, float size)
        {
            var go = new GameObject("FieldSpellDebris");
            go.transform.position = start;
            var rockMat = new MaterialPropertyBlock();
            var renderer = FireballVfx.NewMeshObject("Rock", go.transform, EarthChips.ShardMesh, RockMaterial());
            rockMat.SetColor("_BaseColor", new Color(0.34f, 0.29f, 0.23f));
            renderer.SetPropertyBlock(rockMat);
            go.transform.localScale = Vector3.one * size;
            go.transform.rotation = Random.rotation;
            var d = go.AddComponent<FieldSpellDebris>();
            d.owner = owner;
            d.velocity = velocity;
            d.damage = damage;
            d.spin = Random.insideUnitSphere * 720f;
        }

        private static Material material;

        private static Material RockMaterial()
        {
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "FieldSpellDebris" };
            material.SetFloat("_Smoothness", 0.05f);
            return material;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            Vector3 from = transform.position;
            velocity += Vector3.down * Gravity * dt;
            Vector3 to = from + velocity * dt;
            transform.Rotate(spin * dt, Space.Self);
            foreach (var col in Physics.OverlapCapsule(from, to, HitRadius, ~0, QueryTriggerInteraction.Collide))
            {
                if (!col.CompareTag("Player")) continue;
                var hp = col.GetComponentInParent<HealthComponent>();
                if (hp == null || hp.IsDead) continue;
                hp.TakeDamage(damage, owner, DamageType.Physical);
                if (!hp.IsDead) ImpactPunch.Play(hp.gameObject, new Vector3(velocity.x, 0f, velocity.z).normalized, 0.6f);
                Shatter(to);
                return;
            }
            if (Physics.Linecast(from, to, out var hit, ~0, QueryTriggerInteraction.Ignore) && !hit.collider.CompareTag("Player")
                && !hit.collider.CompareTag("DemonKing") && hit.collider.GetComponentInParent<FieldSpellDebris>() == null)
            {
                Shatter(hit.point);
                return;
            }
            transform.position = to;
            if (age > LifeSeconds || to.y < -20f) Destroy(gameObject);
        }

        private void Shatter(Vector3 at)
        {
            EarthChips.Spawn(at, 0.6f, 5);
            Destroy(gameObject);
        }
    }
}
