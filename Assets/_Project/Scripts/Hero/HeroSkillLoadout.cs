using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Skill;

namespace Game.Hero
{
    /// <summary>
    /// 試合前に選んでいるジョブと5つのスキル。HeroLobby と MatchLobby で共有する。
    /// 1〜4枠: いまのジョブのスキルか共通スキル。5枠目（自由枠）: ジョブに関係なくどのスキルでも入る。
    /// 同じスキルを2つの枠に入れることはできない。
    /// </summary>
    public class HeroSkillLoadout
    {
        public const int FreeSlot = 4;

        public IList<HeroClassData> Classes { get; }
        public SkillData[] Skills { get; } = new SkillData[5];
        public int ClassIndex { get; private set; }
        public PassiveData Passive { get; private set; }

        private readonly List<SkillData> commonSkills = new List<SkillData>();

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

        /// 共通スキル（Inspector の一覧と Resources/CommonSkills）。
        public IReadOnlyList<SkillData> CommonSkills => commonSkills;

        public HeroSkillLoadout(IList<HeroClassData> classes, IList<SkillData> commonSkills)
        {
            Classes = classes;
            if (commonSkills != null) foreach (var s in commonSkills) if (s != null && !this.commonSkills.Contains(s)) this.commonSkills.Add(s);
            foreach (var s in SkillData.LoadExtraCommonSkills()) if (s != null && !this.commonSkills.Contains(s)) this.commonSkills.Add(s);
            if (classes == null) return;
            foreach (var c in classes)
            {
                if (c == null) continue;
                foreach (var s in c.ClassSkillPool())
                {
                    if (s.ownerClass == null) s.ownerClass = c;
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
            ResetToDefault();
            if (session != null && session.SelectedHeroClass == CurrentClass && session.SelectedClassSkills != null && session.SelectedClassSkills.Count == FreeSlot)
            {
                var picks = session.SelectedClassSkills;
                var seen = new HashSet<SkillData>();
                bool valid = true;
                for (int i = 0; i < FreeSlot; i++)
                    if (!CanEquip(i, picks[i]) || !seen.Add(picks[i])) { valid = false; break; }
                if (valid)
                {
                    for (int i = 0; i < FreeSlot; i++) Skills[i] = picks[i];
                    if (seen.Contains(Skills[FreeSlot])) Skills[FreeSlot] = FirstUnusedCommon();
                }
            }
            var free = session != null ? session.SelectedCommonSkill : null;
            if (free != null && System.Array.IndexOf(Skills, free) < 0) Skills[FreeSlot] = free;
            Passive = session != null ? session.SelectedPassive : null;
            FixPassive();
        }

        public void SaveToSession()
        {
            var session = GameSession.Instance;
            if (session == null) return;
            if (CurrentClass != null) session.SelectedHeroClass = CurrentClass;
            session.SelectedClassSkills = new List<SkillData>();
            for (int i = 0; i < FreeSlot; i++) session.SelectedClassSkills.Add(Skills[i]);
            session.SelectedCommonSkill = Skills[FreeSlot];
            session.SelectedPassive = Passive;
        }

        /// ジョブを変えると1〜4枠はそのジョブの標準に戻る。5枠目は残す（1〜4枠と重なったら共通スキルに戻す）。
        public void SelectClass(int index)
        {
            if (Classes == null || index < 0 || index >= Classes.Count) return;
            ClassIndex = index;
            var free = Skills[FreeSlot];
            ResetToDefault();
            if (free != null && System.Array.IndexOf(Skills, free) < 0) Skills[FreeSlot] = free;
            FixPassive();
        }

        /// いまのジョブのスキルか。
        public bool IsJobSkill(SkillData skill)
        {
            var cls = CurrentClass;
            return skill != null && cls != null && cls.ClassSkillPool().Contains(skill);
        }

        public bool IsCommonSkill(SkillData skill) => skill != null && commonSkills.Contains(skill);

        /// その枠に入れてよいスキルか。1〜4枠はジョブのスキルか共通スキル、5枠目は何でもよい。
        public bool CanEquip(int slot, SkillData skill)
        {
            if (skill == null || slot < 0 || slot > FreeSlot) return false;
            if (slot == FreeSlot) return true;
            return IsJobSkill(skill) || IsCommonSkill(skill);
        }

        /// 枠にスキルを入れる。すでに別の枠にあるときは2つを入れ替える（入れ替え先の枠に入らないときは何もしない）。
        public bool Place(int slot, SkillData skill)
        {
            if (!CanEquip(slot, skill)) return false;
            int from = System.Array.IndexOf(Skills, skill);
            if (from == slot) return true;
            if (from >= 0)
            {
                var other = Skills[slot];
                if (other != null && !CanEquip(from, other)) return false;
                Skills[from] = other;
            }
            Skills[slot] = skill;
            return true;
        }

        public void SetFreeSkill(SkillData skill) => Place(FreeSlot, skill);

        public void SetPassive(PassiveData passive)
        {
            var cls = CurrentClass;
            if (cls != null && cls.passives.Contains(passive)) Passive = passive;
        }

        /// 全部のスキル。共通、いまのジョブ、ほかのジョブの順。
        public List<SkillData> AllSkills()
        {
            var list = new List<SkillData>();
            foreach (var s in commonSkills) if (!list.Contains(s)) list.Add(s);
            var current = CurrentClass;
            if (current != null) foreach (var s in current.ClassSkillPool()) if (!list.Contains(s)) list.Add(s);
            if (Classes != null)
                foreach (var c in Classes)
                {
                    if (c == null || c == current) continue;
                    foreach (var s in c.ClassSkillPool()) if (!list.Contains(s)) list.Add(s);
                }
            return list;
        }

        // 1〜4枠をジョブの標準、5枠目を最初の共通スキルにする。
        private void ResetToDefault()
        {
            var cls = CurrentClass;
            var pool = cls != null ? cls.ClassSkillPool() : new List<SkillData>();
            for (int i = 0; i < FreeSlot; i++) Skills[i] = i < pool.Count ? pool[i] : null;
            Skills[FreeSlot] = null;
            Skills[FreeSlot] = FirstUnusedCommon();
        }

        private SkillData FirstUnusedCommon()
        {
            foreach (var s in commonSkills)
                if (System.Array.IndexOf(Skills, s) < 0) return s;
            return null;
        }

        // パッシブはジョブのもの。ジョブが変わったら新しいジョブの最初のパッシブに戻す。
        private void FixPassive()
        {
            var cls = CurrentClass;
            if (cls == null || !cls.passives.Contains(Passive))
                Passive = cls != null && cls.passives.Count > 0 ? cls.passives[0] : null;
        }
    }
}
