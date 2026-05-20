using UnityEngine;

namespace Game.Skill
{
    public enum SkillEffectType { Damage, Heal, Buff, Debuff }
    public enum SkillTargetType { Self, Single, Area }
    public enum SkillDamageType { Physical, Magical, None }

    [CreateAssetMenu(fileName = "NewSkill", menuName = "Game/Skill Data", order = 10)]
    public class SkillData : ScriptableObject
    {
        public string skillName;
        [TextArea] public string description;
        public int spCost;
        public float cooldown;
        public SkillEffectType effectType;
        public SkillDamageType damageType;
        public SkillTargetType targetType;
        public float effectValue;
        public float range;
        public float duration;

        [Header("Leveling")]
        public float levelGrowthRate = 0.2f;
        public int maxSkillLevel = 5;

        public float GetEffectValueAtLevel(int level)
        {
            int lv = Mathf.Clamp(level, 1, maxSkillLevel);
            return effectValue * (1f + (lv - 1) * levelGrowthRate);
        }
    }
}
