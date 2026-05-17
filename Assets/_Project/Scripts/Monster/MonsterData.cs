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
        public float spd;
        public int cost;
        public float spwnCool;
        public int maxSpwn;
        public int exp;
    }
}
