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

        /// 最後の空の段階か。魔物はここからはじまりのまちへ進軍する。
        public static bool IsFinalPhase
        {
            get
            {
                if (Time.time >= nextPhaseCheck)
                {
                    nextPhaseCheck = Time.time + PhaseCheckInterval;
                    var radii = BalanceConfig.Instance.stormStageRadius;
                    finalPhase = SkyPalette.StageForLevel(Current) >= radii.Length - 1;
                }
                return finalPhase;
            }
        }
        private const float PhaseCheckInterval = 0.5f;
        private static float nextPhaseCheck;
        private static bool finalPhase;

        /// Multiplier for monster HP / Atk: 1 + rate * (Lv - 1).
        public static float MonsterStatMultiplier(int dkLevel)
        {
            return 1f + BalanceConfig.Instance.monsterStatGrowthPerDkLevel * Mathf.Max(0, dkLevel - 1);
        }
    }
}
