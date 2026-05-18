using UnityEngine;
using Game.Combat;
using Game.Core;

namespace Game.Hero
{
    public class StatTracker : MonoBehaviour
    {
        public MatchStats Stats { get; private set; } = new MatchStats();

        private HealthComponent ownHealth;
        private ExpComponent ownExp;
        private int lastExp;

        private void Awake()
        {
            ownHealth = GetComponent<HealthComponent>();
            ownExp = GetComponent<ExpComponent>();
            Stats = new MatchStats();
            if (ownExp != null) lastExp = ownExp.CurrentExp;
        }

        private void OnEnable()
        {
            HealthComponent.AnyDamage += OnAnyDamage;
            HealthComponent.AnyDeath += OnAnyDeath;
            if (ownExp != null) ownExp.OnExpChanged += OnExpChanged;
        }

        private void OnDisable()
        {
            HealthComponent.AnyDamage -= OnAnyDamage;
            HealthComponent.AnyDeath -= OnAnyDeath;
            if (ownExp != null) ownExp.OnExpChanged -= OnExpChanged;
        }

        private void OnAnyDamage(HealthComponent target, int amount, GameObject attacker)
        {
            if (attacker == gameObject) Stats.damageDealt += amount;
            if (target == ownHealth) Stats.damageTaken += amount;
        }

        private void OnAnyDeath(HealthComponent target, HealthComponent.DeathInfo info)
        {
            if (target == ownHealth) { Stats.deaths++; return; }
            if (info.lastHitter == gameObject) { Stats.kills++; return; }
            if (info.assistContributors != null)
            {
                foreach (var a in info.assistContributors)
                {
                    if (a == gameObject) { Stats.assists++; return; }
                }
            }
        }

        private void OnExpChanged(int cur, int req)
        {
            int delta = cur - lastExp;
            if (delta > 0) Stats.expGained += delta;
            lastExp = cur;
        }
    }
}
