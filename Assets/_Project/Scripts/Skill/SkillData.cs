using UnityEngine;
using Game.Hero;
using Game.Combat;

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

        [Header("Status Effect (applied to targets hit)")]
        public StatusEffectType statusEffect = StatusEffectType.None;
        public float statusDuration;
        [Tooltip("Slow: speed reduction (0.4 = -40%) / DamageOverTime: damage per tick / others: unused")]
        public float statusPower;

        [Header("Leveling")]
        public float levelGrowthRate = 0.2f;
        public int maxSkillLevel = 5;

        [Header("Lobby Visual")]
        public Color iconColor = new Color(0f, 0f, 0f, 0f); // alpha=0 -> use ownerClass.avatarColor or effectType fallback
        public HeroClassData ownerClass; // null = common skill

        public float GetEffectValueAtLevel(int level)
        {
            int lv = Mathf.Clamp(level, 1, maxSkillLevel);
            return effectValue * (1f + (lv - 1) * levelGrowthRate);
        }

        public Color EffectiveIconColor()
        {
            if (iconColor.a > 0.01f) return iconColor;
            if (ownerClass != null) return ownerClass.avatarColor;
            switch (effectType)
            {
                case SkillEffectType.Damage: return new Color(1f, 0.25f, 0.25f, 1f);
                case SkillEffectType.Heal:   return new Color(0.3f, 0.85f, 0.4f, 1f);
                case SkillEffectType.Buff:   return new Color(0.4f, 0.55f, 1f, 1f);
                case SkillEffectType.Debuff: return new Color(0.85f, 0.3f, 0.9f, 1f);
                default: return new Color(0.6f, 0.6f, 0.6f, 1f);
            }
        }
    }
}
