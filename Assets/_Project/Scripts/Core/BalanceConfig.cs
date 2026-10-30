using UnityEngine;

namespace Game.Core
{
    /// Global balance constants. Loaded from Resources/BalanceConfig; falls back to code defaults if the asset is missing.
    [CreateAssetMenu(menuName = "Game/Balance Config", fileName = "BalanceConfig")]
    public class BalanceConfig : ScriptableObject
    {
        [Header("Damage")]
        public float damageK = 20f;
        public int minDamage = 1;

        [Header("Exp Share")]
        [Tooltip("Supporters (healed a combatant) within this radius of the kill get a share")]
        public float expShareRadius = 15f;
        [Tooltip("Share of the monster's exp given to each supporter (0.25 = 25%)")]
        public float expShareRatio = 0.25f;
        [Tooltip("How recent the heal must be (seconds), same as the assist window")]
        public float expShareSupportWindow = 10f;

        [Header("Status Effects")]
        [Tooltip("Seconds after a stun ends during which new stuns are ignored")]
        public float stunImmunitySeconds = 1.5f;
        public float dotTickInterval = 1f;

        [Header("Demon King Detect Skill")]
        public int dkDetectMpCost = 40;
        public float dkDetectDuration = 5f;
        public float dkDetectCooldown = 30f;

        [Header("Warp Point Skill")]
        [Tooltip("Cast time before moving to the warp point (seconds)")]
        public float warpCastSeconds = 5f;
        [Tooltip("Moving farther than this (m, horizontal) during the cast cancels it")]
        public float warpCancelMoveDistance = 0.5f;
        [Tooltip("A warp point can always be set within this radius (m) of StartTown")]
        public float warpTownRadius = 20f;

        [Header("Monster Hate (smart monsters only)")]
        [Tooltip("Heals cast within this radius (m) of a monster add hate to the healer")]
        public float hateHealRadius = 10f;
        [Tooltip("Hate per point healed (damage adds 1 per point)")]
        public float hateHealRatio = 0.5f;
        [Tooltip("Fraction of hate lost per second (0.1 = 10%/s)")]
        public float hateDecayPerSecond = 0.1f;
        [Tooltip("A hero farther than this (m), or dead, has their hate reset to 0")]
        public float hateResetDistance = 10f;
        [Tooltip("Hate added by Taunt to each monster in range")]
        public float hateTauntAmount = 100f;
        [Tooltip("A new hero must exceed the current target's hate by this ratio to steal aggro")]
        public float hateSwitchRatio = 1.1f;

        [Header("Demon King Level")]
        [Tooltip("Monster HP / Atk growth per Demon King level (0.05 = +5%/Lv)")]
        public float monsterStatGrowthPerDkLevel = 0.05f;

        [Header("Cores")]
        public int coreDf = 30;
        public int coreMDf = 30;

        private static BalanceConfig instance;

        public static BalanceConfig Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = Resources.Load<BalanceConfig>("BalanceConfig");
                    if (instance == null)
                    {
                        instance = CreateInstance<BalanceConfig>();
                        Debug.LogWarning("[BalanceConfig] Resources/BalanceConfig not found; using code defaults.");
                    }
                }
                return instance;
            }
        }
    }
}
