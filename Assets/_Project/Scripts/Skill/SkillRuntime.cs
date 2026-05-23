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
        private readonly int[] skillLevels = new int[SLOT_COUNT];

        public IReadOnlyList<SkillData> EquippedSkills => equippedSkills;
        public int SelectedIndex { get; private set; }
        public float GetCooldownRemaining(int index) => (index >= 0 && index < SLOT_COUNT) ? cooldownTimers[index] : 0f;
        public SkillData CurrentSkill => (SelectedIndex >= 0 && SelectedIndex < equippedSkills.Count) ? equippedSkills[SelectedIndex] : null;

        public int GetSkillLevel(int slot) => (slot >= 0 && slot < SLOT_COUNT) ? skillLevels[slot] : 0;
        public bool CanLevelUp(int slot)
        {
            if (slot < 0 || slot >= SLOT_COUNT) return false;
            var s = equippedSkills[slot];
            if (s == null) return false;
            return skillLevels[slot] < s.maxSkillLevel;
        }
        public bool LevelUp(int slot)
        {
            if (!CanLevelUp(slot)) return false;
            skillLevels[slot]++;
            OnSkillLevelChanged?.Invoke(slot, skillLevels[slot]);
            return true;
        }
        public float GetCurrentEffectValue(int slot)
        {
            if (slot < 0 || slot >= SLOT_COUNT) return 0f;
            var s = equippedSkills[slot];
            if (s == null) return 0f;
            return s.GetEffectValueAtLevel(skillLevels[slot]);
        }

        public event Action<int> OnSelectionChanged;
        public event Action<int> OnSkillCast;
        public event Action<int, int> OnSkillLevelChanged;
        public event Action<int> OnLevelUpPointsChanged;

        public int LevelUpPointsAvailable { get; private set; }

        public void AddLevelUpPoint()
        {
            LevelUpPointsAvailable++;
            OnLevelUpPointsChanged?.Invoke(LevelUpPointsAvailable);
        }

        public bool SpendLevelUpPoint(int slot)
        {
            if (LevelUpPointsAvailable <= 0) return false;
            if (!CanLevelUp(slot)) return false;
            if (!LevelUp(slot)) return false;
            LevelUpPointsAvailable--;
            OnLevelUpPointsChanged?.Invoke(LevelUpPointsAvailable);
            return true;
        }

        private HeroClassInitializer classInit;
        private Game.Combat.ManaComponent mana;

        private void Awake()
        {
            classInit = GetComponent<HeroClassInitializer>();
            mana = GetComponent<Game.Combat.ManaComponent>();

            // Allocate slots in Awake but DON'T read classInit yet — the lobby
            // selection on classInit is updated from GameSession during its own
            // Awake, and Unity Awake order is non-deterministic. Defer skill
            // population to Start so we always see the post-Awake state.
            for (int i = 0; i < SLOT_COUNT; i++) { equippedSkills.Add(null); skillLevels[i] = 1; }
        }

        private void Start()
        {
            if (classInit == null || classInit.ClassData == null) return;
            var classSkills = classInit.ClassData.classSkills;
            for (int i = 0; i < 4 && i < classSkills.Count; i++) equippedSkills[i] = classSkills[i];
            equippedSkills[4] = classInit.CommonSkill;
            Debug.Log($"[SkillRuntime] equipped: " +
                $"{(equippedSkills[0] != null ? equippedSkills[0].skillName : "-")} / " +
                $"{(equippedSkills[1] != null ? equippedSkills[1].skillName : "-")} / " +
                $"{(equippedSkills[2] != null ? equippedSkills[2].skillName : "-")} / " +
                $"{(equippedSkills[3] != null ? equippedSkills[3].skillName : "-")} / " +
                $"{(equippedSkills[4] != null ? equippedSkills[4].skillName : "-")}");
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
            if (mana != null && mana.CurrentMP < skill.spCost) return false;
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
