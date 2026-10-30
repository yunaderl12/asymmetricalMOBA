using UnityEngine;

namespace Game.Monster
{
    [CreateAssetMenu(menuName = "Game/Monster Data", fileName = "MonsterData")]
    public class MonsterData : ScriptableObject
    {
        public string monsterName;
        public int hp;
        public int atk;
        public int df;
        public int mDf;
        public float spd;
        public int cost;
        public float spwnCool;
        public int maxSpwn;
        public int exp;
        [Tooltip("Demon King level required to place this monster in a spawner")]
        public int unlockDkLevel = 1;

        public bool IsUnlockedAt(int dkLevel) => dkLevel >= unlockDkLevel;
        [TextArea(2, 5)] public string description;
    }
}
