using System.Collections.Generic;
using UnityEngine;
using Game.Skill;

namespace Game.Hero
{
    [CreateAssetMenu(fileName = "NewHeroClass", menuName = "Game/Hero Class Data", order = 5)]
    public class HeroClassData : ScriptableObject
    {
        public string className;

        [Header("Initial Stats")]
        public int initialHP;
        public int initialSP;
        public int initialAtk;
        public int initialMAtk;
        public int initialDf;
        public int initialMDf;
        public float initialSpd;

        [Header("Per-Level Growth")]
        public int hpGrowthPerLevel;
        public int spGrowthPerLevel;
        public int atkGrowthPerLevel;
        public int mAtkGrowthPerLevel;
        public int dfGrowthPerLevel;
        public int mDfGrowthPerLevel;
        public float spdGrowthPerLevel;

        [Header("Skills")]
        public List<SkillData> classSkills = new List<SkillData>();

        [Header("Magic Class")]
        [Tooltip("SP restored even during combat: share of Max SP per tick (HeroRegen). 0.04 = 4%.")]
        public float spCombatRegenRate = 0.025f;
        public int basicAttackMpCost = 0;
        public float regenMultiplier = 1.0f;

        [Header("Lobby Visual")]
        public Color avatarColor = new Color(1f, 0.15f, 0.15f, 1f);
        [Tooltip("Full-body image shown large in the lobby settings panel. Empty = coloured oval.")]
        public Sprite portrait;
        [Tooltip("Round icon for the lobby class list. Empty = coloured circle.")]
        public Sprite icon;

        [Header("Model")]
        [Tooltip("Resources path of the character model (e.g. Knight/knight). Empty = no model (capsule body).")]
        public string modelPath;
        [Tooltip("Resources path of a material applied to every renderer (e.g. Knight/KnightMat). Empty = keep the FBX materials.")]
        public string modelMaterialPath;
        [Tooltip("Scale of the model. Size order: Demon King > Warrior > Swordsman > Mage = Priest > Goblin.")]
        public float modelScale = 1f;

        [Header("First-Person View (seen from the camera: x right, y up, z forward)")]
        [Tooltip("Scale of the arms and weapon in the first-person view.")]
        public float viewScale = 0.26f;
        [Tooltip("Point the arm comes from. The arm is drawn from this direction to the hand.")]
        public Vector3 viewRightShoulder = new Vector3(0.34f, -0.6f, 0.1f);
        public Vector3 viewRightPosition = new Vector3(0.2f, -0.15f, 0.42f);
        [Tooltip("Rotation from the character's idle hand. 0 = held as in the idle pose.")]
        public Vector3 viewRightEuler = new Vector3(30f, -15f, -20f);
        public Vector3 viewLeftShoulder = new Vector3(-0.34f, -0.6f, 0.1f);
        public Vector3 viewLeftPosition = new Vector3(-0.27f, -0.18f, 0.42f);
        public Vector3 viewLeftEuler = new Vector3(0f, 35f, 0f);
    }
}
