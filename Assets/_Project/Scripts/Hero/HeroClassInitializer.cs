using UnityEngine;
using Game.Combat;
using Game.Skill;
using Game.Core;

namespace Game.Hero
{
    public class HeroClassInitializer : MonoBehaviour
    {
        [SerializeField] private HeroClassData classData;
        [SerializeField] private SkillData commonSkill;

        public HeroClassData ClassData => classData;
        public SkillData CommonSkill => commonSkill;
        public int CurrentAtk { get; private set; }
        public int CurrentMAtk { get; private set; }
        public int CurrentDf { get; private set; }
        public float CurrentSpd { get; private set; }

        private HealthComponent health;
        private ManaComponent mana;
        private HeroFPSController controller;
        private HeroAttack attack;

        private void Awake()
        {
            health = GetComponent<HealthComponent>();
            mana = GetComponent<ManaComponent>();
            controller = GetComponent<HeroFPSController>();
            attack = GetComponent<HeroAttack>();

            if (GameSession.Instance != null)
            {
                if (GameSession.Instance.SelectedHeroClass != null) classData = GameSession.Instance.SelectedHeroClass;
                if (GameSession.Instance.SelectedCommonSkill != null) commonSkill = GameSession.Instance.SelectedCommonSkill;
            }

            if (classData == null)
            {
                Debug.LogWarning("[HeroClass] classData not assigned on " + name);
                return;
            }

            ApplyInitialStats();

            // Visual avatar: only Swordsman has the Knight model for now.
            if (classData.className == "Swordsman" && GetComponent<HeroKnightAnimator>() == null)
            {
                gameObject.AddComponent<HeroKnightAnimator>();
                Debug.Log("[HeroClass] added HeroKnightAnimator (Swordsman)");
            }
        }

        private void ApplyInitialStats()
        {
            if (health != null) health.SetMaxHP(classData.initialHP);
            if (mana != null) mana.SetMaxMP(classData.initialSP);
            if (controller != null) controller.SetMoveSpeed(classData.initialSpd);
            if (attack != null) attack.SetDamage(classData.initialAtk);

            CurrentAtk = classData.initialAtk;
            CurrentMAtk = classData.initialMAtk;
            CurrentDf = classData.initialDf;
            CurrentSpd = classData.initialSpd;

            Debug.Log($"[HeroClass] {classData.className} initialized: HP={classData.initialHP}, SP={classData.initialSP}, Atk={CurrentAtk}, MAtk={CurrentMAtk}, Df={CurrentDf}, Spd={CurrentSpd}");
        }

        public void ApplyLevelGrowth()
        {
            if (classData == null) return;
            CurrentAtk += classData.atkGrowthPerLevel;
            CurrentMAtk += classData.mAtkGrowthPerLevel;
            CurrentDf += classData.dfGrowthPerLevel;
            CurrentSpd += classData.spdGrowthPerLevel;

            if (attack != null) attack.SetDamage(CurrentAtk);
            if (controller != null) controller.SetMoveSpeed(CurrentSpd);
        }
    }
}
