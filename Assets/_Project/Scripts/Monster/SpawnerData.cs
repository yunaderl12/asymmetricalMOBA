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
        [Tooltip("Seconds the crystal charges before the laser fires. Counted inside cool, so the fire rate stays the same.")]
        public float attackChargeSeconds = 0.75f;
        public int exp = 100;
        public int maxCost = 2;
        public int costPerDkLevel = 1;
        public int inv = 3;
        public float attackRange = 20f;
        public float detectRange = 15f;
    }
}
