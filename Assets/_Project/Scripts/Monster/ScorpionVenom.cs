using UnityEngine;
using Game.Combat;
using Game.Skill;

namespace Game.Monster
{
    /// スコーピオンの尾から飛ぶ毒の弾。まっすぐ飛び、勇者に当たると毒を残す。地面や壁に当たるとはじけて消える。
    public class ScorpionVenom : MonoBehaviour
    {
        private static readonly Color Core = new Color(0.8f, 1f, 0.35f, 1f);
        private static readonly Color Dark = new Color(0.35f, 0.8f, 0.12f, 1f);
        private static Material material;

        private GameObject owner;
        private ScorpionData data;
        private Vector3 direction;
        private int damage;
        private float traveled;
        private ParticleSystem trail;

        public static void Spawn(GameObject owner, Vector3 start, Vector3 direction, int damage, ScorpionData data)
        {
            var go = new GameObject("ScorpionVenom");
            go.transform.position = start;
            var v = go.AddComponent<ScorpionVenom>();
            v.owner = owner;
            v.data = data;
            v.direction = direction.normalized;
            v.damage = damage;
            v.Build();
        }

        private void Build()
        {
            var orb = FireballVfx.NewMeshObject("Orb", transform, FireballVfx.SphereMesh, Material());
            orb.transform.localScale = Vector3.one * 0.9f;
            var inner = FireballVfx.NewMeshObject("Core", transform, FireballVfx.SphereMesh, Material());
            inner.transform.localScale = Vector3.one * 0.5f;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", Dark);
            orb.SetPropertyBlock(block);
            block.SetColor("_BaseColor", Core);
            inner.SetPropertyBlock(block);

            trail = FireballVfx.NewParticles("Trail", transform);
            var main = trail.main;
            main.loop = true;
            main.startLifetime = 0.45f;
            main.startSpeed = 0.6f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startColor = new ParticleSystem.MinMaxGradient(Core, Dark);
            main.gravityModifier = 0.4f;
            var emission = trail.emission;
            emission.rateOverTime = 50f;
            var color = trail.colorOverLifetime;
            color.enabled = true;
            color.color = FireballVfx.FadeGradient(Color.white, new Color(0.4f, 0.6f, 0.3f));
            trail.gameObject.SetActive(true);
            trail.Play();
        }

        private static Material Material()
        {
            if (material != null) return material;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            material = new Material(shader) { name = "ScorpionVenom" };
            return material;
        }

        private void Update()
        {
            float step = Mathf.Min(data.stingSpeed * Time.deltaTime, data.stingRange - traveled);
            Vector3 from = transform.position;
            Vector3 to = from + direction * step;
            traveled += step;

            // 勇者に当たったか（通り道のまわり）。
            foreach (var col in Physics.OverlapCapsule(from, to, data.stingHitRadius, ~0, QueryTriggerInteraction.Collide))
            {
                if (!col.CompareTag("Player")) continue;
                var hp = col.GetComponentInParent<HealthComponent>();
                if (hp == null || hp.IsDead) continue;
                hp.TakeDamage(damage, owner, DamageType.Physical);
                if (hp != null && !hp.IsDead)
                    StatusEffectController.Apply(hp.gameObject, StatusEffectType.DamageOverTime, data.poisonSeconds, data.poisonDamagePerTick, owner);
                Burst(to);
                return;
            }
            // 地面・壁・柱に当たるとはじける。ボス自身と勇者は通り抜ける。
            if (Physics.Linecast(from, to, out var hit, ~0, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<BossBase>() == null && !hit.collider.CompareTag("Player"))
            {
                Burst(hit.point);
                return;
            }
            transform.position = to;
            if (traveled >= data.stingRange) Burst(to);
        }

        private void Burst(Vector3 at)
        {
            FireballVfx.SpawnSparks(at, -direction, 14, Core, Dark);
            if (trail != null)
            {
                trail.transform.SetParent(null, true);
                trail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                Destroy(trail.gameObject, 1f);
            }
            Destroy(gameObject);
        }
    }
}
