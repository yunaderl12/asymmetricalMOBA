using UnityEngine;
using UnityEngine.AI;
using Game.Combat;
using Game.Core;
using Game.Skill;

namespace Game.Monster
{
    /// 攻撃を受けたときの小さなノックバックと極小のけぞり。強さはダメージに比例し、MonsterData.hitReactionScale で変わる。
    public class MonsterHitReaction : MonoBehaviour
    {
        private const float KnockbackSeconds = 0.16f;
        private const float MaxPunchRecoil = 1f;

        private Monster monster;
        private HealthComponent health;
        private NavMeshAgent agent;
        private Vector3 knockVelocity;
        private float knockTimer;
        private float flinchTimer;

        public bool IsFlinching => flinchTimer > 0f;

        private void Awake()
        {
            monster = GetComponent<Monster>();
            health = GetComponent<HealthComponent>();
            agent = GetComponent<NavMeshAgent>();
        }

        private void OnEnable()
        {
            HealthComponent.AnyDamage += OnAnyDamage;
        }

        private void OnDisable()
        {
            HealthComponent.AnyDamage -= OnAnyDamage;
        }

        private void OnAnyDamage(HealthComponent target, int amount, GameObject attacker)
        {
            if (target != health || attacker == null || health.CurrentHP <= 0 || health.SuppressHitReaction || monster.IsEmerging) return;
            float scale = monster.Data != null ? monster.Data.hitReactionScale : 1f;
            if (scale <= 0f) return;

            var cfg = BalanceConfig.Instance;
            float distance = Mathf.Min(amount * cfg.knockbackPerDamage, cfg.knockbackMax) * scale;
            float flinch = Mathf.Min(amount * cfg.flinchPerDamage, cfg.flinchMax) * scale;

            Vector3 away = transform.position - attacker.transform.position;
            away.y = 0f;
            away = away.sqrMagnitude > 0.001f ? away.normalized : -transform.forward;
            // 速さは直線的に落ちていくので、初速は平均の2倍。
            knockVelocity = away * (2f * distance / KnockbackSeconds);
            knockTimer = KnockbackSeconds;
            flinchTimer = Mathf.Max(flinchTimer, flinch);
            ImpactPunch.Play(gameObject, away, Mathf.Min(distance, MaxPunchRecoil));
        }

        private void Update()
        {
            if (flinchTimer > 0f) flinchTimer -= Time.deltaTime;
            if (knockTimer <= 0f) return;
            knockTimer -= Time.deltaTime;
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
            agent.Move(knockVelocity * (Mathf.Clamp01(knockTimer / KnockbackSeconds) * Time.deltaTime));
        }
    }
}
