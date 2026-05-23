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
        [SerializeField] private float mpAlwaysRegenInterval = 1.5f;
        [SerializeField] private float mpAlwaysRegenRate = 0.04f;

        private HealthComponent health;
        private ManaComponent mana;
        private HeroClassInitializer classInit;
        private float lastActionTime;
        private float nextTickTime;
        private int regenStep;
        private float nextMpAlwaysTickTime;

        private void Awake()
        {
            health = GetComponent<HealthComponent>();
            mana = GetComponent<ManaComponent>();
            classInit = GetComponent<HeroClassInitializer>();
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

        private void TickMpAlwaysRegen()
        {
            if (mana == null) return;
            if (classInit == null || classInit.ClassData == null || !classInit.ClassData.isMagicClass) return;
            if (Time.time < nextMpAlwaysTickTime) return;
            int restore = Mathf.CeilToInt(mana.MaxMP * mpAlwaysRegenRate);
            if (restore > 0) mana.RestoreMana(restore);
            nextMpAlwaysTickTime = Time.time + mpAlwaysRegenInterval;
        }

        private void OnAnyDamage(HealthComponent target, int amount, GameObject attacker)
        {
            if (target == health) NotifyActed();
        }

        private void Update()
        {
            if (health == null) return;
            TickMpAlwaysRegen();
            if (Time.time - lastActionTime < idleThreshold) return;
            if (Time.time < nextTickTime) return;

            float classMul = (classInit != null && classInit.ClassData != null) ? classInit.ClassData.regenMultiplier : 1f;
            float rate = baseRegenRate * Mathf.Pow(regenMultiplier, regenStep) * classMul;
            int hpRestore = health != null ? Mathf.CeilToInt(health.MaxHP * rate) : 0;
            int mpRestore = mana != null ? Mathf.CeilToInt(mana.MaxMP * rate) : 0;
            if (health != null) health.Heal(hpRestore);
            if (mana != null) mana.RestoreMana(mpRestore);
            Debug.Log($"[HeroRegen] tick step={regenStep} rate={rate:F4} +{hpRestore}HP +{mpRestore}MP");
            regenStep++;
            nextTickTime = Time.time + regenTickInterval;
        }
    }
}
