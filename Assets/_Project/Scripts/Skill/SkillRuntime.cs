using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Hero;

namespace Game.Skill
{
    public class SkillRuntime : MonoBehaviour
    {
        public const int SLOT_COUNT = 5;

        private readonly List<SkillData> equippedSkills = new List<SkillData>(SLOT_COUNT);
        private readonly float[] cooldownTimers = new float[SLOT_COUNT];

        public IReadOnlyList<SkillData> EquippedSkills => equippedSkills;
        public int SelectedIndex { get; private set; }
        public float GetCooldownRemaining(int index) => (index >= 0 && index < SLOT_COUNT) ? cooldownTimers[index] : 0f;
        public SkillData CurrentSkill => (SelectedIndex >= 0 && SelectedIndex < equippedSkills.Count) ? equippedSkills[SelectedIndex] : null;

        public event Action<int> OnSelectionChanged;
        public event Action<int> OnSkillCast;

        private HeroClassInitializer classInit;
        private Game.Combat.SkillPointComponent sp;

        private void Awake()
        {
            classInit = GetComponent<HeroClassInitializer>();
            sp = GetComponent<Game.Combat.SkillPointComponent>();

            for (int i = 0; i < SLOT_COUNT; i++) equippedSkills.Add(null);
            if (classInit != null && classInit.ClassData != null)
            {
                var classSkills = classInit.ClassData.classSkills;
                for (int i = 0; i < 4 && i < classSkills.Count; i++) equippedSkills[i] = classSkills[i];
                equippedSkills[4] = classInit.CommonSkill;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < SLOT_COUNT; i++)
            {
                if (cooldownTimers[i] > 0f) cooldownTimers[i] = Mathf.Max(0f, cooldownTimers[i] - dt);
            }
        }

        public bool CanCast(int index)
        {
            if (index < 0 || index >= equippedSkills.Count) return false;
            var skill = equippedSkills[index];
            if (skill == null) return false;
            if (cooldownTimers[index] > 0f) return false;
            if (sp != null && sp.CurrentSP < skill.spCost) return false;
            return true;
        }

        public void StartCooldown(int index)
        {
            if (index < 0 || index >= equippedSkills.Count) return;
            var skill = equippedSkills[index];
            if (skill == null) return;
            cooldownTimers[index] = skill.cooldown;
        }

        public void SetSelectedIndex(int index)
        {
            if (index < 0 || index >= SLOT_COUNT) return;
            if (SelectedIndex == index) return;
            SelectedIndex = index;
            OnSelectionChanged?.Invoke(SelectedIndex);
        }

        public void NotifyCast(int index)
        {
            OnSkillCast?.Invoke(index);
        }
    }
}
