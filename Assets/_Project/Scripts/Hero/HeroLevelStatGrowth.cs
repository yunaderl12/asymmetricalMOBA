using UnityEngine;
using Game.Combat;
using Game.Core;

namespace Game.Hero
{
    [RequireComponent(typeof(LevelComponent))]
    [RequireComponent(typeof(HealthComponent))]
    public class HeroLevelStatGrowth : MonoBehaviour
    {
        private LevelComponent level;
        private HealthComponent health;
        private ManaComponent mana;
        private HeroClassInitializer classInit;

        private void Awake()
        {
            level = GetComponent<LevelComponent>();
            health = GetComponent<HealthComponent>();
            mana = GetComponent<ManaComponent>();
            classInit = GetComponent<HeroClassInitializer>();
        }

        private void OnEnable()
        {
            if (level != null) level.OnLevelChanged += OnLevelChanged;
        }

        private void OnDisable()
        {
            if (level != null) level.OnLevelChanged -= OnLevelChanged;
        }

        private void OnLevelChanged(int newLevel)
        {
            if (classInit == null || classInit.ClassData == null)
            {
                Debug.LogWarning("[Hero] LevelGrowth has no classData; skipping growth.");
                return;
            }
            var data = classInit.ClassData;

            if (health != null) health.SetMaxHP(health.MaxHP + data.hpGrowthPerLevel);
            if (mana != null) mana.SetMaxMP(mana.MaxMP + data.mpGrowthPerLevel);
            classInit.ApplyLevelGrowth();

            int newHP = health != null ? health.MaxHP : -1;
            int newMP = mana != null ? mana.MaxMP : -1;
            Debug.Log($"[Hero] Lv {newLevel} stat growth: MaxHP={newHP}, MaxMP={newMP}, Atk={classInit.CurrentAtk}, MAtk={classInit.CurrentMAtk}, Df={classInit.CurrentDf}, MDf={classInit.CurrentMDf}, Spd={classInit.CurrentSpd}");
        }
    }
}
