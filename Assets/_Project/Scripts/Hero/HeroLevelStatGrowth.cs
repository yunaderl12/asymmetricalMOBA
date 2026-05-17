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

        private LevelComponent level;
        private HealthComponent health;

        private void Awake()
        {
            level = GetComponent<LevelComponent>();
            health = GetComponent<HealthComponent>();
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
            if (health == null) return;
            health.SetMaxHP(health.MaxHP + hpPerLevel);
            Debug.Log($"[Hero] Lv {newLevel} stat growth: MaxHP -> {health.MaxHP}");
        }
    }
}
