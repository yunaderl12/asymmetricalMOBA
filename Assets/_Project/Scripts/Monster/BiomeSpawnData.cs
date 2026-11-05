using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;

namespace Game.Monster
{
    /// バイオームごとに自然湧きする魔物の表。1バイオームの最大数は魔物ごとの MonsterData.maxSpwn、湧き間隔は spwnCool。
    [CreateAssetMenu(menuName = "Game/Biome Spawn Data", fileName = "BiomeSpawnTable")]
    public class BiomeSpawnData : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public StageMap.Biome biome;
            public List<MonsterData> monsters = new List<MonsterData>();
        }

        public List<Entry> entries = new List<Entry>();
    }
}
