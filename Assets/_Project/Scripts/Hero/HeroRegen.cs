using UnityEngine;
using Game.Combat;

namespace Game.Hero
{
    public class HeroRegen : MonoBehaviour
    {
        [SerializeField] private float idleThreshold = 5f;
        [SerializeField] private float baseRegenRate = 0.005f;
        [SerializeField] private float regenMultiplier = 1.1f;
        [SerializeField] private float regenTickInterval = 1.0f;

        private HealthComponent health;
        private ManaComponent mana;
        private SkillPointComponent sp;
        private float lastActionTime;
        private float nextTickTime;
        private int regenStep;

        private void Awake()
        {
            health = GetComponent<HealthComponent>();
            mana = GetComponent<ManaComponent>();
            sp = GetComponent<SkillPointComponent>();
        }

        private void OnEnable()
        {
            HealthComponent.AnyDamage += OnAnyDamage;
            NotifyActed();
        }

        private void OnDisable()
        {
            HealthComponent.AnyDamage -= OnAnyDamage;
        }

        public void NotifyActed()
        {
            lastActionTime = Time.time;
            regenStep = 0;
            nextTickTime = Time.time + idleThreshold + regenTickInterval;
        }

        private void OnAnyDamage(HealthComponent target, int amount, GameObject attacker)
        {
            if (target == health) NotifyActed();
        }

        private void Update()
        {
            if (health == null) return;
            if (Time.time - lastActionTime < idleThreshold) return;
            if (Time.time < nextTickTime) return;

            float rate = baseRegenRate * Mathf.Pow(regenMultiplier, regenStep);
            int hpRestore = health != null ? Mathf.CeilToInt(health.MaxHP * rate) : 0;
            int spRestore = sp != null ? Mathf.CeilToInt(sp.MaxSP * rate) : 0;
            int mpRestore = mana != null ? Mathf.CeilToInt(mana.MaxMP * rate) : 0;
            if (health != null) health.Heal(hpRestore);
            if (sp != null) sp.RestoreSP(spRestore);
            if (mana != null) mana.RestoreMana(mpRestore);
            Debug.Log($"[HeroRegen] tick step={regenStep} rate={rate:F4} +{hpRestore}HP +{spRestore}SP +{mpRestore}MP");
            regenStep++;
            nextTickTime = Time.time + regenTickInterval;
        }
    }
}
