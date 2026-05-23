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
        public float initialSpd;

        [Header("Per-Level Growth")]
        public int hpGrowthPerLevel;
        public int spGrowthPerLevel;
        public int atkGrowthPerLevel;
        public int mAtkGrowthPerLevel;
        public int dfGrowthPerLevel;
        public float spdGrowthPerLevel;

        [Header("Skills")]
        public List<SkillData> classSkills = new List<SkillData>();

        [Header("Magic Class")]
        public bool isMagicClass = false;
        public int basicAttackMpCost = 0;
        public float regenMultiplier = 1.0f;

        [Header("Lobby Visual")]
        public Color avatarColor = new Color(1f, 0.15f, 0.15f, 1f);
    }
}
