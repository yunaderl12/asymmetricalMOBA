using UnityEngine;
using Game.Combat;
using Game.Core;

namespace Game.Hero
{
    [RequireComponent(typeof(LevelComponent))]
    [RequireComponent(typeof(HealthComponent))]
    public class HeroLevelStatGrowth : MonoBehaviour
    {
        [SerializeField] private int hpPerLevel = 10;
        [SerializeField] private int spPerLevel = 5;

        private LevelComponent level;
        private HealthComponent health;
        private SkillPointComponent sp;

        private void Awake()
        {
            level = GetComponent<LevelComponent>();
            health = GetComponent<HealthComponent>();
            sp = GetComponent<SkillPointComponent>();
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
            if (health != null)
            {
                health.SetMaxHP(health.MaxHP + hpPerLevel);
            }
            if (sp != null)
            {
                sp.SetMaxSP(sp.MaxSP + spPerLevel);
            }
            int newHP = health != null ? health.MaxHP : -1;
            int newSP = sp != null ? sp.MaxSP : -1;
            Debug.Log($"[Hero] Lv {newLevel} stat growth: MaxHP -> {newHP}, MaxSP -> {newSP}");
        }
    }
}
