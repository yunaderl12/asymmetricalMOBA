using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Skill;

namespace Game.Hero
{
    /// <summary>
    /// The class and 5 skills a hero is choosing before a match. Slots 1-4 follow the class;
    /// slot 5 is free (other classes' skills + common skills). Shared by HeroLobby and MatchLobby.
    /// </summary>
    public class HeroSkillLoadout
    {
        public const int FreeSlot = 4;

        public IList<HeroClassData> Classes { get; }
        public SkillData[] Skills { get; } = new SkillData[5];
        public int ClassIndex { get; private set; }

        private readonly IList<SkillData> commonSkills;

        public HeroClassData CurrentClass
        {
            get
            {
                if (Classes == null || Classes.Count == 0) return null;
                return Classes[Mathf.Clamp(ClassIndex, 0, Classes.Count - 1)];
            }
        }

        public Color ClassColor
        {
            get
            {
                var c = CurrentClass;
                return c != null ? c.avatarColor : new Color(0.4f, 0.4f, 0.4f, 1f);
            }
        }

        public HeroSkillLoadout(IList<HeroClassData> classes, IList<SkillData> commonSkills)
        {
            Classes = classes;
            this.commonSkills = commonSkills;
            if (classes == null) return;
            foreach (var c in classes)
            {
                if (c == null || c.classSkills == null) continue;
                foreach (var s in c.classSkills)
                {
                    if (s != null && s.ownerClass == null) s.ownerClass = c;
                }
            }
        }

        public void LoadFromSession()
        {
            var session = GameSession.Instance;
            if (session != null && session.SelectedHeroClass != null && Classes != null)
            {
                int i = Classes.IndexOf(session.SelectedHeroClass);
                if (i >= 0) ClassIndex = i;
            }
            Skills[FreeSlot] = session != null ? session.SelectedCommonSkill : null;
            RefreshSkills();
        }

        public void SaveToSession()
        {
            var session = GameSession.Instance;
            if (session == null) return;
            if (CurrentClass != null) session.SelectedHeroClass = CurrentClass;
            session.SelectedCommonSkill = Skills[FreeSlot];
        }

        public void SelectClass(int index)
        {
            if (Classes == null || index < 0 || index >= Classes.Count) return;
            ClassIndex = index;
            RefreshSkills();
        }

        public void SetFreeSkill(SkillData skill)
        {
            if (skill != null) Skills[FreeSlot] = skill;
        }

        /// Skills that can go into slot 5. Equipped ones are left out, so a swap puts the old skill back into the list.
        public List<SkillData> BuildAvailableSkills()
        {
            var list = new List<SkillData>();
            var current = CurrentClass;
            if (Classes != null)
            {
                foreach (var c in Classes)
                {
                    if (c == null || c == current || c.classSkills == null) continue;
                    foreach (var s in c.classSkills) if (s != null) list.Add(s);
                }
            }
            if (commonSkills != null) foreach (var s in commonSkills) if (s != null && !list.Contains(s)) list.Add(s);
            foreach (var s in SkillData.LoadExtraCommonSkills()) if (s != null && !list.Contains(s)) list.Add(s);
            list.RemoveAll(s => System.Array.IndexOf(Skills, s) >= 0);
            return list;
        }

        // Slots 1-4 follow the class. Slot 5 is kept unless it is now one of the class's own skills.
        private void RefreshSkills()
        {
            var cls = CurrentClass;
            var classSkills = cls != null ? cls.classSkills : null;
            for (int i = 0; i < FreeSlot; i++)
                Skills[i] = classSkills != null && i < classSkills.Count ? classSkills[i] : null;

            var free = Skills[FreeSlot];
            if (free == null || (classSkills != null && classSkills.Contains(free)))
                Skills[FreeSlot] = commonSkills != null && commonSkills.Count > 0 ? commonSkills[0] : null;
        }
    }
}
