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

        [Header("Archer Tower")]
        [Tooltip("Enemies inside this radius get shot (m)")]
        public float archerTowerRange = 22f;
        [Tooltip("Physical attack value of one arrow")]
        public int archerTowerDamage = 15;
        [Tooltip("Seconds between shots of one tower")]
        public float archerTowerInterval = 1.5f;
        [Tooltip("Arrow flight speed (m/s)")]
        public float archerArrowSpeed = 32f;

        [Header("Blizzard Skill")]
        [Tooltip("How fast the line of ice spikes runs along the ground (m/s)")]
        public float blizzardSpikeSpeed = 40f;
        [Tooltip("On ice, how quickly speed can change (m/s^2). Lower is more slippery. Applies to heroes, the demon king, monsters and bots.")]
        public float iceAcceleration = 3f;

        [Header("Teleport Skill")]
        [Tooltip("Time to climb straight up from the ground to the hover point (seconds)")]
        public float teleportRiseSeconds = 1.6f;
        [Tooltip("Time spent hovering in the sky while turning toward StartTown (seconds)")]
        public float teleportHoverSeconds = 0.8f;
        [Tooltip("Time to fly from the hover point down to StartTown (seconds)")]
        public float teleportTravelSeconds = 2.5f;
        [Tooltip("Minimum height of the hover point above the ground (m); longer flights go higher")]
        public float teleportArcHeight = 20f;
        [Tooltip("Hero stays locked after landing (seconds)")]
        public float teleportLandingSeconds = 0.3f;

        [Header("Monster Spawn Animation")]
        [Tooltip("Warning circle on the ground before the portal opens (seconds)")]
        public float spawnWarnSeconds = 1.2f;
        [Tooltip("Portal opening time; the monster appears when it finishes (seconds)")]
        public float spawnPortalSeconds = 0.9f;
        [Tooltip("Monster rising out of the portal; it cannot act or be hit until this ends (seconds)")]
        public float spawnEmergeSeconds = 1.1f;
        [Tooltip("Radius of the warning circle and portal (m)")]
        public float spawnPortalRadius = 1.4f;

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

        [Header("Demon King Model")]
        [Tooltip("Resources path of the Demon King model. Empty = no model (capsule body).")]
        public string demonKingModelPath = "DemonKing/demonking";
        [Tooltip("Scale of the Demon King model (heroes use HeroClassData.modelScale)")]
        public float demonKingModelScale = 1.5f;

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
