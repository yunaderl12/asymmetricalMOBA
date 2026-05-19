using Game.Hero;
using Game.Skill;

namespace Game.Core
{
    [System.Serializable]
    public class BotData
    {
        public string botName;
        public GameSession.PlayerRole role;
        public HeroClassData heroClass;
        public SkillData freeSlotSkill;
        public bool isReady = true;
    }
}
