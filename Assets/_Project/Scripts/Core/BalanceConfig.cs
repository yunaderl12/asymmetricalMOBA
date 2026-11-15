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
        public float expShareRadius = 30f;
        [Tooltip("Share of the monster's exp given to each supporter (0.25 = 25%)")]
        public float expShareRatio = 0.25f;
        [Tooltip("How recent the heal must be (seconds), same as the assist window")]
        public float expShareSupportWindow = 10f;

        [Header("Dash")]
        [Tooltip("Playback speed of the running clips. Keeps the feet in step with the doubled move speed.")]
        public float runAnimationSpeed = 1.7f;
        [Tooltip("Speed multiplier while dashing")]
        public float dashSpeedMultiplier = 1.6f;
        [Tooltip("Second press of forward within this time (seconds) starts the dash")]
        public float dashDoubleTapSeconds = 0.3f;
        [Tooltip("Field of view added while running at full speed (degrees)")]
        public float runFovBoost = 2f;
        [Tooltip("Field of view added while dashing (degrees)")]
        public float dashFovBoost = 9f;
        [Tooltip("Seconds for the field of view to follow the speed")]
        public float speedFovSmoothing = 0.18f;
        [Tooltip("Thin lines streaming past the edges of the screen while dashing")]
        public bool dashSpeedLines = true;

        [Header("Crouch And Slide")]
        [Tooltip("Move speed multiplier while crouching")]
        public float crouchSpeedMultiplier = 0.5f;
        [Tooltip("How far the view drops when crouching or sliding (m)")]
        public float crouchCameraDrop = 0.55f;
        [Tooltip("Seconds for the view to drop to / rise from crouch height")]
        public float crouchTransitionSeconds = 0.14f;
        [Tooltip("Slide start speed as a multiple of the running speed")]
        public float slideBoost = 1.3f;
        [Tooltip("Slide slowdown while forward is held (m/s per second)")]
        public float slideFriction = 9f;
        [Tooltip("Slide slowdown after forward is let go (m/s per second)")]
        public float slideReleaseFriction = 24f;
        [Tooltip("Camera roll while sliding (degrees)")]
        public float slideCameraRoll = 3f;
        [Tooltip("After a slide that started from a run, pressing forward (or standing up while holding it) within this many seconds resumes the run")]
        public float slideRunResumeSeconds = 3f;

        [Header("Landing And Jump Feel")]
        [Tooltip("Falling faster than this (m/s) makes the landing dip and dust")]
        public float landingFeelMinSpeed = 5f;
        [Tooltip("Camera dip per m/s of landing speed (m), capped by landingDipMax")]
        public float landingDipPerSpeed = 0.02f;
        public float landingDipMax = 0.28f;
        [Tooltip("Camera dip when a jump starts (m)")]
        public float jumpDip = 0.07f;

        [Header("Hit Reaction (monsters)")]
        [Tooltip("Knockback distance per point of damage (m)")]
        public float knockbackPerDamage = 0.03f;
        public float knockbackMax = 1.2f;
        [Tooltip("How far a hero is pushed when a monster or the Demon King hits them (m). Just enough to notice the hit.")]
        public float heroHitKnockback = 0.35f;
        [Tooltip("Screen shake when a hero is hit (camera offset, m)")]
        public float heroHitShake = 0.04f;
        [Tooltip("Flinch per point of damage (seconds). Separate from Stun, so it causes no stun immunity.")]
        public float flinchPerDamage = 0.006f;
        public float flinchMax = 0.25f;

        [Header("Status Effects")]
        [Tooltip("Seconds after a stun ends during which new stuns are ignored")]
        public float stunImmunitySeconds = 1.5f;
        public float dotTickInterval = 1f;

        [Header("Demon King Detect Skill")]
        public int dkDetectMpCost = 40;
        public float dkDetectDuration = 5f;
        public float dkDetectCooldown = 30f;

        [Header("Warp Gate Skill (Mage)")]
        [Tooltip("How far in front of the caster a gate opens (m)")]
        public float warpGateDistance = 2.5f;
        [Tooltip("Gate size (m)")]
        public float warpGateWidth = 2.6f;
        public float warpGateHeight = 3.0f;
        [Tooltip("The second gate must be at least this far from the first one (m)")]
        public float warpGateMinLinkDistance = 10f;
        [Tooltip("A first gate that is not linked closes after this many seconds")]
        public float warpGatePendingSeconds = 60f;
        [Tooltip("Linked gates stay open this many seconds")]
        public float warpGateLinkedSeconds = 30f;
        [Tooltip("Seconds for a gate to grow open")]
        public float warpGateOpenSeconds = 0.8f;
        [Tooltip("After passing, the same hero cannot pass again for this many seconds")]
        public float warpGatePassCooldown = 1.5f;
        [Tooltip("A hero comes out this far in front of the exit gate (m)")]
        public float warpGateExitDistance = 1.6f;

        [Header("Monster Alert")]
        [Tooltip("After the ! mark appears, the monster keeps turning to face the hero for this many seconds")]
        public float monsterAlertFaceSeconds = 1.2f;
        [Tooltip("Turn speed while facing the hero after the ! mark (degrees per second)")]
        public float monsterAlertTurnSpeed = 900f;

        [Header("Death Camera")]
        [Tooltip("Height of the overhead camera above the dead hero (m)")]
        public float deathCamHeight = 6f;
        [Tooltip("Horizontal distance of the overhead camera behind the dead hero (m)")]
        public float deathCamBack = 7f;
        [Tooltip("Seconds to move from the eyes to the overhead view")]
        public float deathCamBlendSeconds = 1.2f;
        [Tooltip("Slow turn around the dead hero (degrees per second)")]
        public float deathCamOrbitSpeed = 8f;
        [Tooltip("Height of the camera above the hero while the respawn animation plays (m)")]
        public float respawnCamHeight = 3.5f;
        [Tooltip("Horizontal distance of the camera from the hero while the respawn animation plays (m)")]
        public float respawnCamDistance = 5.5f;
        [Tooltip("Where the respawn camera starts, in degrees around the hero (0 = behind, 180 = in front). It swings round to behind.")]
        public float respawnCamStartAngle = 150f;
        [Tooltip("Seconds at the end of the respawn animation to move back into the eyes")]
        public float respawnCamReturnSeconds = 0.6f;

        [Header("Archer Tower")]
        [Tooltip("Enemies inside this radius get shot (m)")]
        public float archerTowerRange = 44f;
        [Tooltip("Physical attack value of one arrow")]
        public int archerTowerDamage = 15;
        [Tooltip("Seconds between shots of one tower")]
        public float archerTowerInterval = 1.5f;
        [Tooltip("Arrow flight speed (m/s)")]
        public float archerArrowSpeed = 64f;

        [Header("Blizzard Skill")]
        [Tooltip("How fast the line of ice spikes runs along the ground (m/s)")]
        public float blizzardSpikeSpeed = 40f;
        [Tooltip("On ice, how quickly speed can change (m/s^2). Lower is more slippery. Applies to heroes, the demon king, monsters and bots.")]
        public float iceAcceleration = 6f;

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
        public float hateHealRadius = 20f;
        [Tooltip("Hate per point healed (damage adds 1 per point)")]
        public float hateHealRatio = 0.5f;
        [Tooltip("Fraction of hate lost per second (0.1 = 10%/s)")]
        public float hateDecayPerSecond = 0.1f;
        [Tooltip("A hero farther than this (m), or dead, has their hate reset to 0")]
        public float hateResetDistance = 20f;
        [Tooltip("Hate added by Taunt to each monster in range")]
        public float hateTauntAmount = 100f;
        [Tooltip("A new hero must exceed the current target's hate by this ratio to steal aggro")]
        public float hateSwitchRatio = 1.1f;

        [Header("Biome Spawn")]
        [Tooltip("Natural spawns appear at least this far (m) from every hero.")]
        public float biomeSpawnMinHeroDistance = 35f;
        [Tooltip("Natural spawns stay this far (m) from water.")]
        public float biomeSpawnMinWaterDistance = 5f;
        [Tooltip("Tries per check to find a place for a natural spawn.")]
        public int biomeSpawnAttempts = 8;
        [Tooltip("Natural spawns appear only around a standing tower of the same biome: distance from the tower (m), min and max.")]
        public Vector2 biomeSpawnTowerRadius = new Vector2(6f, 22f);

        [Header("Tower Range View")]
        [Tooltip("Colour of the faint sphere showing where a tower shoots")]
        public Color towerRangeDomeColor = new Color(1f, 0.55f, 0.6f, 1f);
        [Tooltip("Alpha of the sphere where it faces the camera (almost clear)")]
        public float towerRangeDomeBaseAlpha = 0.008f;
        [Tooltip("Extra alpha at the sphere's outline")]
        public float towerRangeDomeRimAlpha = 0.1f;
        [Tooltip("Line where the sphere meets the ground")]
        public Color towerRangeRingColor = new Color(1f, 0.12f, 0.12f, 0.5f);
        public float towerRangeRingWidth = 0.35f;
        [Tooltip("Demon King's camera above a tower: move speed (m/s) inside the tower's range")]
        public float towerViewMoveSpeed = 12f;

        [Header("Bot Hunting")]
        [Tooltip("Seconds between choosing where to go next when no enemy is close.")]
        public float botGoalInterval = 2f;
        [Tooltip("Demon King level at which bots stop hunting and attack towers, then the castle. Bots have no level of their own.")]
        public int botPushDkLevel = 5;
        [Tooltip("Goes back to town below this share of HP.")]
        public float botRetreatHpRatio = 0.3f;
        [Tooltip("Leaves town again above this share of HP.")]
        public float botReturnHpRatio = 0.8f;

        [Header("Bot Skills")]
        [Tooltip("How often a bot hero considers using a skill (seconds).")]
        public float botSkillThinkInterval = 0.4f;
        [Tooltip("Minimum time between a bot hero's skills (seconds).")]
        public float botSkillCastGap = 2f;
        [Tooltip("Bots heal an ally or themselves below this share of HP.")]
        public float botHealHpRatio = 0.6f;
        [Tooltip("Bots use defensive buffs below this share of HP while an enemy is close.")]
        public float botDefendHpRatio = 0.7f;
        [Tooltip("An enemy this close counts as attacking the bot (metres).")]
        public float botDefendEnemyRange = 10f;
        [Tooltip("Bots sprint when their target is farther than this (metres).")]
        public float botSprintTargetDistance = 12f;
        [Tooltip("Bots with Teleport escape below this share of HP while an enemy is close.")]
        public float botEscapeHpRatio = 0.3f;
        [Tooltip("How far the Mage's basic magic bolt flies (metres). A little shorter than a spawner tower's shooting range (SpawnerData.detectRange 30m), so the Mage cannot outrange the tower.")]
        public float mageBoltRange = 28f;
        [Tooltip("Mage bots stop this far from their target and shoot magic bolts (metres).")]
        public float botMageAttackRange = 10f;
        [Tooltip("Seconds between a bot hero's MP regen ticks. Matches HeroRegen.")]
        [UnityEngine.Serialization.FormerlySerializedAs("botSpRegenInterval")]
        public float botMpRegenInterval = 1.5f;

        [Header("Demon King Level")]
        [Tooltip("Monster HP / Atk growth per Demon King level (0.05 = +5%/Lv)")]
        public float monsterStatGrowthPerDkLevel = 0.05f;

        [Header("Cores")]
        public int coreDf = 30;
        public int coreMDf = 30;

        [Header("Demon King Model")]
        [Tooltip("Resources path of the Demon King model. Empty = no model (capsule body).")]
        public string demonKingModelPath = "DemonKing/demon_king";
        [Tooltip("Scale of the Demon King model (heroes use HeroClassData.modelScale)")]
        public float demonKingModelScale = 1.5f;

        private static BalanceConfig instance;

        [Header("Ground Dig")]
        [Tooltip("Size of one cell of the dig grid (m). Larger = bigger, more angular facets.")]
        public float digCellSize = 0.75f;
        [Tooltip("Height of one terrace (m). Neighbouring vertices differ by one terrace at most, so keep slopes under the hero's slope limit (45 degrees).")]
        public float digStepDepth = 0.25f;
        [Tooltip("Deepest the ground can go (m).")]
        public float digMaxDepth = 1f;
        [Tooltip("Keep-out around rocks, walls, town, castle and towers (m).")]
        public float digKeepOutMargin = 1.5f;
        [Tooltip("Keep-out around spawners (m).")]
        public float digSpawnerMargin = 4f;
        [Tooltip("Keep-out around trees (m).")]
        public float digTreeMargin = 2f;
        [Tooltip("Keep-out along the outer edge of the field (m).")]
        public float digEdgeMargin = 3f;

        [Header("Terrain")]
        [Tooltip("Speed multiplier while walking in a shallow river.")]
        public float shallowSpeedMultiplier = 0.925f;
        [Tooltip("Seconds between ripples while walking in a shallow river.")]
        public float rippleInterval = 0.32f;
        [Tooltip("Radius a ripple grows to (m).")]
        public float rippleRadius = 1.6f;
        [Tooltip("浅瀬を歩いているときに水しぶきを上げる間隔 (秒)")]
        public float waterSplashInterval = 0.16f;
        [Tooltip("1回の水しぶきの粒の数（水に入った瞬間はこの2倍）")]
        public int waterSplashDrops = 7;
        [Tooltip("Speed multiplier while walking on desert sand off the road (heroes, bots and the Demon King; monsters live there and are not slowed).")]
        public float looseSandSpeedMultiplier = 0.8f;
        [Tooltip("Seconds between sand splashes while walking on loose sand.")]
        public float sandSplashInterval = 0.22f;
        [Tooltip("Sand grains thrown up by one splash.")]
        public int sandSplashGrains = 9;
        [Tooltip("Below this height, being over deep water counts as falling in (m).")]
        public float deepWaterFallHeight = 1.5f;
        [Tooltip("Speed lost by the Demon King on a 30 degree climb (0.25 = 25%).")]
        public float demonKingSlopePenalty = 0.25f;

        [Header("Hero MP")]
        [Tooltip("MP restored when a basic attack hits a monster, as a share of Max MP.")]
        [UnityEngine.Serialization.FormerlySerializedAs("spPerBasicHit")]
        public float mpPerBasicHit = 0.06f;
        [Tooltip("Inside this radius of StartTown (m), MP is refilled.")]
        public float townRefillRadius = 26f;

        [Header("Monster Behaviour")]
        [Tooltip("Until the last sky stage, monsters wander inside this radius of their spawner (m).")]
        public float monsterWanderRadius = 18f;
        [Tooltip("Seconds a wandering monster waits between moves.")]
        public float monsterWanderWaitMin = 1f;
        public float monsterWanderWaitMax = 3.5f;
        [Tooltip("Field of view for noticing a hero (degrees, total). A hero behind the monster goes unnoticed.")]
        public float monsterSightDegrees = 180f;
        [Tooltip("Height of the ! mark above the top of a monster (m).")]
        public float monsterAlertMarkLift = 0.5f;
        [Tooltip("Gap a melee monster keeps between its body and the hero's (m), so it never stands inside the hero.")]
        public float monsterKeepGap = 0.35f;

        [Header("Storm Sky")]
        [Tooltip("Radius of the red sky and clouds for each stage, as a share of the castle-to-town distance. 1 = reaches the town.")]
        public float[] stormStageRadius = { 0.2f, 0.3f, 0.45f, 0.6f, 0.8f, 1.1f };
        [Tooltip("Demon King levels per stage.")]
        public int stormLevelsPerStage = 3;
        [Tooltip("Seconds the radius takes to grow to the next stage.")]
        public float stormGrowSeconds = 20f;
        [Tooltip("Height of the bottom of the clouds (m).")]
        public float stormCloudHeight = 65f;
        [Tooltip("Rotation speed of the whole swirl (radians per second).")]
        public float stormSpinSpeed = 0.04f;
        [Tooltip("Seconds a cloud takes to travel from the outer edge to the center along an arm.")]
        public float stormInflowSeconds = 900f;
        [Tooltip("Number of cloud lumps in the whole swirl. Each is 20 triangles.")]
        public int stormCloudLumps = 2100;
        [Tooltip("How much longer a lump is along the flow than across it.")]
        public float stormCloudStretch = 2.2f;
        public int stormArmCount = 3;
        [Tooltip("Width of a spiral arm (radians, standard deviation).")]
        public float stormArmWidth = 0.24f;
        [Tooltip("Share of lumps scattered outside the arms.")]
        [Range(0f, 1f)] public float stormScatter = 0.12f;

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
