using UnityEngine;

namespace Game.Monster
{
    /// 魔物の動き方と技。Melee はゴブリンのような体当たりの近接（ContactDamage）。ほかは MonsterBehaviour の派生が動かす。
    public enum MonsterKind { Melee, Dryad, Mummy, Ifrit, Harpy, Cockatrice }

    [CreateAssetMenu(menuName = "Game/Monster Data", fileName = "MonsterData")]
    public class MonsterData : ScriptableObject
    {
        public string monsterName;
        public int hp;
        public int atk;
        public int df;
        public int mDf;
        public float spd;
        public int cost;
        public float spwnCool;
        public int maxSpwn;
        public int exp;
        [Tooltip("Demon King level required to place this monster in a spawner")]
        public int unlockDkLevel = 1;

        [Tooltip("Smart monster: targets the hero with the most hate instead of the nearest one")]
        public bool useHate;

        public bool IsUnlockedAt(int dkLevel) => dkLevel >= unlockDkLevel;
        [TextArea(2, 5)] public string description;

        [Header("Kind")]
        [Tooltip("How it moves and attacks. Melee = touches heroes (ContactDamage); others run their own MonsterBehaviour and never deal touch damage to heroes.")]
        public MonsterKind kind = MonsterKind.Melee;
        [Tooltip("Notices heroes this close (m). 0 = the prefab's aggro radius.")]
        public float noticeRadius;

        [Header("Attack (Dryad / Harpy / Ifrit)")]
        [Tooltip("Starts the attack when the hero it chases is this close (m).")]
        public float attackRange = 24f;
        [Tooltip("Seconds between attacks.")]
        public float attackCooldown = 2.6f;
        [Tooltip("Seconds of the windup before the shot leaves.")]
        public float attackWindup = 0.55f;
        [Tooltip("Speed of the shot (m/s).")]
        public float projectileSpeed = 24f;
        [Tooltip("Share of Atk dealt by one shot.")]
        public float attackDamageScale = 1f;
        [Tooltip("Shots in one volley (Dryad leaves). They fan out a little.")]
        public int projectileCount = 1;
        [Tooltip("Total fan angle of a volley (degrees).")]
        public float projectileSpread = 8f;
        [Tooltip("Radius of a shot for hitting heroes (m).")]
        public float projectileHitRadius = 0.45f;

        [Header("Mummy")]
        [Tooltip("Throws the bandage at heroes between these distances (m).")]
        public float grabMinRange = 5f;
        public float grabRange = 16f;
        [Tooltip("Speed of the bandage tip (m/s).")]
        public float grabSpeed = 34f;
        [Tooltip("Seconds between bandage throws.")]
        public float grabCooldown = 7f;
        [Tooltip("Seconds the pulled hero is stunned (so the two-handed attack lands).")]
        public float grabStunSeconds = 0.9f;
        [Tooltip("Share of Atk dealt by the bandage hit itself.")]
        public float grabDamageScale = 0.3f;
        [Tooltip("Reach of the two-handed attack (m, from the body center).")]
        public float clawRange = 2.6f;
        [Tooltip("Share of Atk dealt by the two-handed attack.")]
        public float clawDamageScale = 1.4f;
        [Tooltip("Seconds between two-handed attacks.")]
        public float clawCooldown = 2.2f;
        [Tooltip("Speed against spd while lurching (lowest, highest).")]
        public Vector2 lurchSpeedScale = new Vector2(0.35f, 1.15f);

        [Header("Floating / Flying")]
        [Tooltip("Height the body floats above the ground at rest (m). Dryad / Ifrit.")]
        public float floatHeight = 0.25f;
        [Tooltip("Height it flies at after noticing a hero (m). Harpy / Cockatrice: about half a hero.")]
        public float hoverHeight = 0.9f;

        [Header("Cockatrice Dive")]
        [Tooltip("Starts the dive when the hero is this close (m).")]
        public float diveRange = 12f;
        [Tooltip("Height it climbs to before diving (m).")]
        public float diveClimbHeight = 4f;
        [Tooltip("Seconds of the climb.")]
        public float diveWindup = 0.8f;
        [Tooltip("Speed of the glide down (m/s).")]
        public float diveSpeed = 17f;
        [Tooltip("Heroes this close to the body during the dive are hit (m).")]
        public float diveHitRadius = 1.8f;
        public float diveDamageScale = 1.5f;
        [Tooltip("Knockback of a dive hit (m).")]
        public float diveKnockback = 3f;
        [Tooltip("Seconds it stays low after the dive.")]
        public float diveRecover = 0.9f;
        [Tooltip("Seconds between dives.")]
        public float diveCooldown = 4.5f;

        [Header("Spacing")]
        [Tooltip("Stops this far from the hero it chases (m, from center to center). 0 = melee: just short of touching.")]
        public float keepDistance;
        [Tooltip("Backs off when the hero comes closer than this (m). 0 = 80% of keepDistance.")]
        public float retreatDistance;

        [Header("Bridge Guardian")]
        [Tooltip("Stands in front of a bridge instead of wandering. Chosen in the Demon King lobby, not in the deck.")]
        public bool isBridgeGuardian;
        [Tooltip("Height and radius of the body for hits and pathing (m).")]
        public float guardBodyHeight = 5f;
        public float guardBodyRadius = 1.3f;
        [Tooltip("Starts chasing heroes this close (m).")]
        public float guardAggroRadius = 30f;
        [Tooltip("Gives up on heroes this far from its post (m).")]
        public float guardLeashRadius = 45f;
        [Tooltip("Starts the slam when a hero is this close (m).")]
        public float slamRange = 11f;
        [Tooltip("Seconds raising both hands overhead.")]
        public float slamWindupSeconds = 1.2f;
        [Tooltip("Seconds swinging down to the ground.")]
        public float slamDownSeconds = 0.2f;
        [Tooltip("Seconds it stays still after the hit.")]
        public float slamRecoverSeconds = 1.3f;
        [Tooltip("Seconds between the end of one slam and the next.")]
        public float slamCooldown = 1.5f;
        [Tooltip("Where the hands land, in front of the body (m).")]
        public float slamFrontDistance = 2.6f;
        [Tooltip("Radius of the small shockwave at the point of impact (m).")]
        public float shockwaveRadius = 4f;
        [Tooltip("Share of Atk dealt by the shockwave.")]
        public float shockwaveDamageScale = 0.5f;
        [Tooltip("How many times rock spears burst out along the line.")]
        public int spearSteps = 7;
        [Tooltip("Distance between bursts (m).")]
        public float spearSpacing = 1.7f;
        [Tooltip("Seconds between bursts.")]
        public float spearInterval = 0.06f;
        [Tooltip("Height of the middle spear of a burst (m).")]
        public float spearHeight = 3.4f;
        [Tooltip("Heroes this close to a burst are hit (m).")]
        public float spearHitRadius = 1.6f;
        [Tooltip("Share of Atk dealt by the spears. A hero is hit once per slam.")]
        public float spearDamageScale = 1f;
        [Tooltip("Seconds the spears stay out before sinking.")]
        public float spearHoldSeconds = 0.45f;
        [Tooltip("Spears in the cluster at the end of the line (剣山).")]
        public int spearFinalCount = 14;
        [Tooltip("Radius of the final cluster (m). Heroes inside it are hit.")]
        public float spearFinalRadius = 2.4f;
        [Tooltip("Height of the middle spear of the final cluster, against spearHeight.")]
        public float spearFinalHeightScale = 1.35f;

        [Header("Bridge Guardian Weight")]
        [Tooltip("Speed of the walk animation against other monsters (slower = heavier steps).")]
        public float guardWalkAnimScale = 0.6f;
        [Tooltip("How far the body sinks on each step (m).")]
        public float guardStepDip = 0.18f;
        [Tooltip("Screen shake of a hero standing right next to a step (camera offset, m).")]
        public float guardStepShake = 0.05f;
        [Tooltip("Heroes this close feel the steps (m).")]
        public float guardStepShakeRadius = 20f;
        [Tooltip("Screen shake of a hero standing right next to the slam (camera offset, m).")]
        public float slamShake = 0.28f;
        [Tooltip("Heroes this close feel the slam (m).")]
        public float slamShakeRadius = 35f;
        [Tooltip("Seconds the slam shake lasts.")]
        public float slamShakeSeconds = 0.6f;
        [Tooltip("Radius of the dent the slam leaves in the ground (m). 0 = no dent.")]
        public float slamDentRadius = 2f;
        [Tooltip("Depth of the dent at its center (m). Repeated slams dig deeper, up to BalanceConfig.digMaxDepth.")]
        public float slamDentDepth = 0.25f;

        [Header("Model")]
        [Tooltip("Resources path of the model on the Knight skeleton (e.g. Goblin/goblin). Empty = keep the prefab's Body shape.")]
        public string modelPath;
        [Tooltip("The model has its own bones (not the Knight's). Its MonsterBehaviour poses them in code.")]
        public bool customRig;
        [Tooltip("Resources path of a material applied to every renderer. Empty = keep the FBX materials.")]
        public string modelMaterialPath;
        public float modelScale = 1f;
        [Tooltip("魔王ロビーの丸いアイコン（Tools/Characters/Generate Monster Icons でモデルから作る）。空なら名前の頭文字。")]
        public Sprite icon;

        [Header("Hit Reaction")]
        [Tooltip("Scale of knockback and flinch when hit. 0 = immune.")]
        public float hitReactionScale = 1f;

        [Header("Blast Death")]
        [Tooltip("Seconds from the kill to the burst: flies up and away, swells white, bursts. 0 = use the fall below.")]
        public float deathBlastSeconds = 0.75f;
        [Tooltip("Launch speed (m/s) of the blast.")]
        public float deathBlastSpeed = 11f;

        [Header("Death (models only; a monster without a model vanishes at once)")]
        [Tooltip("Seconds of the fall before it lies still.")]
        public float deathFallSeconds = 1f;
        [Tooltip("Seconds it lies still before scattering.")]
        public float deathHoldSeconds = 0.2f;
        [Tooltip("Particles in the scatter.")]
        public int deathParticleCount = 50;
    }
}
