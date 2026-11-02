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

        [Tooltip("Smart monster: targets the hero with the most hate instead of the nearest one")]
        public bool useHate;

        public bool IsUnlockedAt(int dkLevel) => dkLevel >= unlockDkLevel;
        [TextArea(2, 5)] public string description;

        [Header("Model")]
        [Tooltip("Resources path of the model on the Knight skeleton (e.g. Goblin/goblin). Empty = keep the prefab's Body shape.")]
        public string modelPath;
        [Tooltip("Resources path of a material applied to every renderer. Empty = keep the FBX materials.")]
        public string modelMaterialPath;
        public float modelScale = 1f;
    }
}
