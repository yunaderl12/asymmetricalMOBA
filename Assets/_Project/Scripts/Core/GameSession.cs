using System.Collections.Generic;
using UnityEngine;
using Game.Hero;
using Game.Skill;
using Game.Monster;

namespace Game.Core
{
    public class GameSession : MonoBehaviour
    {
        public enum PlayerRole { None, Hero, DemonKing }

        public static GameSession Instance { get; private set; }

        public const int SPAWNER_COUNT = 5;
        public const int DECK_MAX = 20;
        public const int BRIDGE_COUNT = 3;

        public PlayerRole CurrentRole = PlayerRole.None;
        public HeroClassData SelectedHeroClass;
        /// 1〜4枠に選んだジョブのスキル（SelectedHeroClass のもの）。空ならジョブの標準。
        public List<SkillData> SelectedClassSkills = new List<SkillData>();
        public SkillData SelectedCommonSkill;
        public PassiveData SelectedPassive;
        public List<MonsterData> DemonDeck = new List<MonsterData>();
        public List<MonsterData> SpawnerInitialMobs = new List<MonsterData>();
        /// 橋ごとの番人（山・砂漠・森の順）。未設定の橋には標準の番人が立つ。
        public MonsterData[] BridgeGuardians = new MonsterData[BRIDGE_COUNT];
        /// 橋ごとに番人を置くか（魔王ロビーの地図で切り替える）。
        public bool[] BridgeGuardianOn = { true, true, true };

        // ---- 魔王ロビーの地図の配置（2026-11-08） ----
        public const int BIOME_COUNT = 3;
        public const int BIOME_MONSTER_SLOTS = 3;
        public const int FIELD_SPELL_SLOTS = 5;
        /// ロビーの並び（山・森・砂漠）。
        public static readonly StageMap.Biome[] PlacementBiomes = { StageMap.Biome.Mountain, StageMap.Biome.Forest, StageMap.Biome.Desert };
        /// 魔王ロビーで配置を決めたか。false なら自然湧きは BiomeSpawnTable のまま。
        public bool PlacementChosen;
        /// バイオームごとの通常魔物（PlacementBiomes の順に3枠ずつ）。同じ魔物を2枠に入れると、その分だけ多く湧く。
        public MonsterData[] BiomeMonsters = new MonsterData[BIOME_COUNT * BIOME_MONSTER_SLOTS];
        /// バイオームごとに中ボスを出すか（PlacementBiomes の順）。
        public bool[] BiomeBossOn = { true, true, true };
        /// 装備したフィールド魔法（1〜5キー）。
        public Game.DemonKing.FieldSpellData[] FieldSpells = new Game.DemonKing.FieldSpellData[FIELD_SPELL_SLOTS];

        public static int PlacementIndex(StageMap.Biome biome) => System.Array.IndexOf(PlacementBiomes, biome);

        /// 塔（すべて砂漠に立つ）で出す魔物。デッキにし、5本の塔の最初の魔物に順に割り振る。空なら砂漠の表の魔物。
        public void SetTowerMonsters(List<MonsterData> monsters)
        {
            var list = new List<MonsterData>();
            if (monsters != null) foreach (var m in monsters) if (m != null && !list.Contains(m)) list.Add(m);
            if (list.Count == 0)
            {
                var entry = Game.Monster.BiomeSpawnData.Load() != null ? Game.Monster.BiomeSpawnData.Load().For(StageMap.Biome.Desert) : null;
                if (entry != null) foreach (var m in entry.monsters) if (m != null && !list.Contains(m)) list.Add(m);
            }
            DemonDeck = list;
            SpawnerInitialMobs = new List<MonsterData>();
            for (int i = 0; i < SPAWNER_COUNT; i++) SpawnerInitialMobs.Add(list.Count > 0 ? list[i % list.Count] : null);
        }

        /// 魔王ロビーを通らない試合（勇者でプレイして魔王が BOT のときなど）の配置。
        /// 自然湧きは表のまま、中ボスと番人は全部置き、塔は砂漠の魔物（今はイフリートとミイラ）。
        public void UseDefaultDemonPlacement()
        {
            PlacementChosen = false;
            for (int i = 0; i < BiomeBossOn.Length; i++) BiomeBossOn[i] = true;
            for (int i = 0; i < BridgeGuardianOn.Length; i++) BridgeGuardianOn[i] = true;
            for (int i = 0; i < BridgeGuardians.Length; i++) BridgeGuardians[i] = null;
            for (int i = 0; i < FieldSpells.Length; i++) FieldSpells[i] = null;
            SetTowerMonsters(null);
        }
        /// 試合をするワールド（WorldCatalog.All の番号）。
        public int WorldIndex;
        public MatchStats LastMatchStats;
        public MatchManager.Winner LastWinner = MatchManager.Winner.None;
        public MatchManager.EndCause LastEndCause = MatchManager.EndCause.TimeUp;
        public bool SkipPrepPhase = false;
        public List<BotData> CurrentMatchBots = new List<BotData>();

        public static GameSession GetOrCreate()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("GameSession");
            return go.AddComponent<GameSession>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (SpawnerInitialMobs == null) SpawnerInitialMobs = new List<MonsterData>();
            while (SpawnerInitialMobs.Count < SPAWNER_COUNT) SpawnerInitialMobs.Add(null);
            ApplyDefaultsIfNeeded();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void ApplyDefaultsIfNeeded()
        {
#if UNITY_EDITOR
            if (SelectedHeroClass == null)
                SelectedHeroClass = UnityEditor.AssetDatabase.LoadAssetAtPath<HeroClassData>("Assets/_Project/ScriptableObjects/Classes/Swordsman.asset");
            if (SelectedCommonSkill == null)
                SelectedCommonSkill = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/Common/Sprint.asset");
#endif
            // 塔の魔物の既定は砂漠の魔物（ゴブリンは森だけに湧く）。
            if (DemonDeck == null || DemonDeck.Count == 0) UseDefaultDemonPlacement();
        }
    }
}
