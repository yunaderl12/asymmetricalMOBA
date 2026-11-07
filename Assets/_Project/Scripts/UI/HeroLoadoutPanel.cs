using System.Collections.Generic;
using UnityEngine;
using Game.Hero;
using Game.Skill;

namespace Game.UI
{
    /// マッチロビー（勇者側）で選べるジョブと共通スキル。画面は MatchLobby が作る。
    public class HeroLoadoutPanel : MonoBehaviour
    {
        [Header("All Hero Classes")]
        [SerializeField] private List<HeroClassData> allClasses = new List<HeroClassData>();
        [SerializeField] private List<SkillData> commonSkills = new List<SkillData>();

        public List<HeroClassData> AllClasses => allClasses;
        public List<SkillData> CommonSkills => commonSkills;
    }
}
