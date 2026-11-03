using UnityEngine;
using Game.Combat;
using Game.Skill;
using Game.Core;

namespace Game.Hero
{
    public class HeroClassInitializer : MonoBehaviour, IDefenseSource
    {
        [SerializeField] private HeroClassData classData;
        [SerializeField] private SkillData commonSkill;

        public HeroClassData ClassData => classData;
        public SkillData CommonSkill => commonSkill;
        public int CurrentAtk { get; private set; }
        public int CurrentMAtk { get; private set; }
        public int CurrentDf { get; private set; }
        public int CurrentMDf { get; private set; }
        public float CurrentSpd { get; private set; }

        // Df buffs (Guard / IronWall / PowerItem) come through StatModifierContainer.
        public int Df => Mathf.RoundToInt(CurrentDf * (mods != null ? mods.TotalDfMultiplier : 1f));
        public int MDf => CurrentMDf;

        private HealthComponent health;
        private StatModifierContainer mods;
        private ManaComponent mana;
        private HeroFPSController controller;
        private HeroAttack attack;

        private void Awake()
        {
            health = GetComponent<HealthComponent>();
            mana = GetComponent<ManaComponent>();
            controller = GetComponent<HeroFPSController>();
            attack = GetComponent<HeroAttack>();
            mods = GetComponent<StatModifierContainer>();
            if (mods != null) mods.OnModifiersChanged += ApplyMoveSpeed;

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

            // Visual avatar from the class data (driven by local input, hidden from the own FPS camera).
            if (!string.IsNullOrEmpty(classData.modelPath) && GetComponent<CharacterModelAnimator>() == null)
            {
                var anim = gameObject.AddComponent<CharacterModelAnimator>();
                anim.Configure(classData.modelPath, classData.modelMaterialPath, classData.modelScale, true, true);
                Debug.Log($"[HeroClass] added CharacterModelAnimator ({classData.className}: {classData.modelPath})");
                HeroViewModel.Attach(gameObject, classData);
            }
        }

        private void OnDestroy()
        {
            if (mods != null) mods.OnModifiersChanged -= ApplyMoveSpeed;
        }

        /// Base Spd x modifiers (Sprint, PowerItem, Slow, ...).
        private void ApplyMoveSpeed()
        {
            if (controller == null) return;
            controller.SetMoveSpeed(CurrentSpd * (mods != null ? mods.TotalSpdMultiplier : 1f));
        }

        private void ApplyInitialStats()
        {
            if (health != null) health.SetMaxHP(classData.initialHP);
            if (mana != null) mana.SetMaxMP(classData.initialSP);
            if (attack != null) attack.SetDamage(classData.initialAtk);

            CurrentAtk = classData.initialAtk;
            CurrentMAtk = classData.initialMAtk;
            CurrentDf = classData.initialDf;
            CurrentMDf = classData.initialMDf;
            CurrentSpd = classData.initialSpd;
            ApplyMoveSpeed();

            Debug.Log($"[HeroClass] {classData.className} initialized: HP={classData.initialHP}, SP={classData.initialSP}, Atk={CurrentAtk}, MAtk={CurrentMAtk}, Df={CurrentDf}, MDf={CurrentMDf}, Spd={CurrentSpd}");
        }

        public void ApplyLevelGrowth()
        {
            if (classData == null) return;
            CurrentAtk += classData.atkGrowthPerLevel;
            CurrentMAtk += classData.mAtkGrowthPerLevel;
            CurrentDf += classData.dfGrowthPerLevel;
            CurrentMDf += classData.mDfGrowthPerLevel;
            CurrentSpd += classData.spdGrowthPerLevel;

            if (attack != null) attack.SetDamage(CurrentAtk);
            ApplyMoveSpeed();
        }
    }
}
