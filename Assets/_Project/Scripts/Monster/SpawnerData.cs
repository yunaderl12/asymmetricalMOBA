using UnityEngine;

namespace Game.Monster
{
    [CreateAssetMenu(menuName = "Game/Spawner Data", fileName = "SpawnerData")]
    public class SpawnerData : ScriptableObject
    {
        public string spawnerName;
        public int hp = 300;
        public int atk = 15;
        public float cool = 2.0f;
        public int exp = 100;
        public int maxCost = 2;
        public int costPerDkLevel = 1;
        public int inv = 3;
        public float spawnInterval = 2.0f;
        public float spawnIntervalMultiplier = 5.0f;
        public float attackRange = 20f;
        public float detectRange = 15f;
    }
}
