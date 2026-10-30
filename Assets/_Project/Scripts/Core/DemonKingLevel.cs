using UnityEngine;

namespace Game.Core
{
    /// Current Demon King level (human or BOT). Returns 1 when no Demon King is in the scene.
    public static class DemonKingLevel
    {
        public static int Current
        {
            get
            {
                var dkGO = GameObject.FindWithTag("DemonKing");
                if (dkGO == null) return 1;
                var lc = dkGO.GetComponent<LevelComponent>();
                return lc != null ? lc.CurrentLevel : 1;
            }
        }

        /// Multiplier for monster HP / Atk: 1 + rate * (Lv - 1).
        public static float MonsterStatMultiplier(int dkLevel)
        {
            return 1f + BalanceConfig.Instance.monsterStatGrowthPerDkLevel * Mathf.Max(0, dkLevel - 1);
        }
    }
}
