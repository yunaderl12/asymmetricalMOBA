using System.Collections.Generic;
using UnityEngine;
using Game.Combat;

namespace Game.Monster
{
    public class ContactDamage : MonoBehaviour
    {
        [SerializeField] private int damagePerTick = 5;
        [SerializeField] private float tickInterval = 1.0f;
        [SerializeField] private string[] targetTags = new string[] { "Player" };

        private readonly Dictionary<HealthComponent, float> nextTickTime = new Dictionary<HealthComponent, float>();

        private bool IsTargetTag(string tag)
        {
            if (targetTags == null || targetTags.Length == 0) return true;
            foreach (var t in targetTags) if (t == tag) return true;
            return false;
        }

        private void OnTriggerStay(Collider other)
        {
            if (!IsTargetTag(other.tag)) return;
            var hp = other.GetComponentInParent<HealthComponent>();
            if (hp == null || hp.IsDead) return;

            if (!nextTickTime.TryGetValue(hp, out var t) || Time.time >= t)
            {
                int dmg = damagePerTick;
                var monster = GetComponentInParent<Monster>();
                if (monster != null && monster.CurrentAtk > 0) dmg = monster.CurrentAtk;
                var attacker = monster != null ? monster.gameObject : gameObject;
                hp.TakeDamage(dmg, attacker);
                nextTickTime[hp] = Time.time + tickInterval;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var hp = other.GetComponentInParent<HealthComponent>();
            if (hp != null) nextTickTime.Remove(hp);
        }
    }
}
