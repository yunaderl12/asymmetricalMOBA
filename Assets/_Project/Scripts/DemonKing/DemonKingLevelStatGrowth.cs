using UnityEngine;
using Game.Combat;
using Game.Core;

namespace Game.DemonKing
{
    /// <summary>
    /// Grows the Demon King's MaxHP and MaxMP each time LevelComponent fires OnLevelChanged.
    /// Heals to full on each level-up so the DK feels stronger immediately.
    /// </summary>
    public class DemonKingLevelStatGrowth : MonoBehaviour
    {
        [Header("Growth per level-up")]
        [SerializeField] private int hpGrowthPerLevel = 50;
        [SerializeField] private int mpGrowthPerLevel = 20;

        [Header("Heal on level-up")]
        [SerializeField] private bool healToFullOnLevelUp = true;

        private LevelComponent level;
        private HealthComponent health;
        private ManaComponent mana;

        private void Awake()
        {
            level = GetComponent<LevelComponent>();
            health = GetComponent<HealthComponent>();
            mana = GetComponent<ManaComponent>();
        }

        private void OnEnable()
        {
            if (level != null) level.OnLevelChanged += OnLevelChanged;
        }

        private void OnDisable()
        {
            if (level != null) level.OnLevelChanged -= OnLevelChanged;
        }

        private void OnLevelChanged(int lv)
        {
            int prevHp = health != null ? health.MaxHP : -1;
            int prevMp = mana != null ? mana.MaxMP : -1;

            if (health != null && hpGrowthPerLevel > 0)
            {
                int newMax = health.MaxHP + hpGrowthPerLevel;
                if (healToFullOnLevelUp)
                {
                    health.SetMaxHP(newMax);
                }
                else
                {
                    // SetMaxHP resets HP to full; if we don't want that, restore proportionally.
                    int curBefore = health.CurrentHP;
                    health.SetMaxHP(newMax);
                    int diff = newMax - curBefore;
                    if (diff > 0) health.TakeDamage(diff);
                }
            }
            if (mana != null && mpGrowthPerLevel > 0)
            {
                mana.SetMaxMP(mana.MaxMP + mpGrowthPerLevel);
            }

            int newHp = health != null ? health.MaxHP : -1;
            int newMp = mana != null ? mana.MaxMP : -1;
            Debug.Log($"[DK Growth] Lv {lv} -> HP {prevHp}->{newHp}, MP {prevMp}->{newMp}");
        }
    }
}
