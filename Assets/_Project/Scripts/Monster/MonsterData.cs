using UnityEngine;

namespace Game.Monster
{
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
        [Tooltip("Resources path of a material applied to every renderer. Empty = keep the FBX materials.")]
        public string modelMaterialPath;
        public float modelScale = 1f;

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
