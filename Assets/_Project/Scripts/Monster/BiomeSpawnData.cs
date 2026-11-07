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
            /// 魔王ロビーで置ける通常魔物の候補（ロビーを通らないときはこのまま湧く）。
            public List<MonsterData> monsters = new List<MonsterData>();
            /// このバイオームの広場に出る中ボス（ロビーで出す・出さないを選ぶ）。
            public BossData boss;
        }

        /// ロビーと試合の両方で読むので Resources に置く。
        public static BiomeSpawnData Load() => Resources.Load<BiomeSpawnData>("BiomeSpawnTable");

        public Entry For(StageMap.Biome biome) => entries.Find(e => e.biome == biome);

        public List<Entry> entries = new List<Entry>();
    }
}
