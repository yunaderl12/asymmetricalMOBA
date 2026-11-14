using UnityEngine;
using Game.Combat;
using Game.Skill;

namespace Game.Hero
{
    /// 重戦士の「怒りの一撃」。ダメージを受けると怒りが溜まり、満タンで出す次の通常攻撃が強くなって相手を押し飛ばす。死ぬと空になる。
    public class RagePassive : HeroPassive
    {
        private static readonly Color RageColor = new Color(1.6f, 0.35f, 0.2f, 1f);

        private HeroAttack attack;
        private float rage;

        public override string GaugeLabel => "怒り";
        public override float GaugeFill => rage;
        public override bool GaugeLit => rage >= 1f;

        protected override void OnInit()
        {
            attack = GetComponent<HeroAttack>();
            if (attack != null) attack.ModifyMeleeHit = OnMeleeHit;
            HealthComponent.AnyDamage += OnAnyDamage;
            if (Health != null) Health.OnDeath += OnDeath;
        }

        private void OnDestroy()
        {
            HealthComponent.AnyDamage -= OnAnyDamage;
            if (Health != null) Health.OnDeath -= OnDeath;
            if (attack != null && attack.ModifyMeleeHit == (System.Func<HealthComponent, int, int>)OnMeleeHit) attack.ModifyMeleeHit = null;
        }

        private void OnDeath()
        {
            rage = 0f;
        }

        private void OnAnyDamage(HealthComponent target, int amount, GameObject attacker)
        {
            if (target != Health || attacker == gameObject || Health.IsDead || rage >= 1f) return;
            float full = Mathf.Max(1f, Health.MaxHP * Data.rageFullDamageRatio);
            rage = Mathf.Min(1f, rage + amount / full);
            if (rage < 1f) return;
            ShockRing.Spawn(transform.position + Vector3.up * 0.1f, 0.3f, 2.4f, 0.35f, 0.25f, RageColor);
            for (int i = 0; i < 10; i++)
                SparkleStar.Spawn(transform.position + Vector3.up * Random.Range(0.4f, 1.8f) + Random.insideUnitSphere * 0.5f, 0.12f, 0.5f, Random.onUnitSphere * 2f);
        }

        private int OnMeleeHit(HealthComponent target, int damage)
        {
            if (rage < 1f || target == null) return damage;
            rage = 0f;
            Vector3 away = target.transform.position - transform.position;
            away.y = 0f;
            SkillKnockback.Play(target.gameObject, away, Data.rageKnockback);
            Vector3 point = target.transform.position + Vector3.up * 1f - away.normalized * 0.4f;
            MeleeFx.Impact(point, away.normalized, 1.8f, MeleeFx.Steel);
            ShockRing.Spawn(target.transform.position + Vector3.up * 0.1f, 0.4f, 3.4f, 0.32f, 0.35f, RageColor);
            if (Controller != null) Controller.Shake(0.12f, 0.25f);
            HitStop.Play(0.08f);
            return Mathf.RoundToInt(damage * Data.rageDamageScale);
        }
    }
}
