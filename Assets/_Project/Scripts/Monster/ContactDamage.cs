using System.Collections.Generic;
using UnityEngine;
using Game.Combat;

namespace Game.Monster
{
    public class ContactDamage : MonoBehaviour
    {
        [SerializeField] private int damagePerTick = 5;
        [SerializeField] private float tickInterval = 0.5f;

        private readonly Dictionary<HealthComponent, float> nextTickTime = new Dictionary<HealthComponent, float>();

        private void OnTriggerStay(Collider other)
        {
            var hp = other.GetComponentInParent<HealthComponent>();
            if (hp == null || hp.IsDead) return;

            if (!nextTickTime.TryGetValue(hp, out var t) || Time.time >= t)
            {
                hp.TakeDamage(damagePerTick);
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
