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

        public const int DECK_MAX = 20;
        public const int BRIDGE_COUNT = 3;

        public PlayerRole CurrentRole = PlayerRole.None;
        public HeroClassData SelectedHeroClass;
        /// 1〜4枠に選んだジョブのスキル（SelectedHeroClass のもの）。空ならジョブの標準。
        public List<SkillData> SelectedClassSkills = new List<SkillData>();
        public SkillData SelectedCommonSkill;
        public PassiveData SelectedPassive;
        public List<MonsterData> DemonDeck = new List<MonsterData>();
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

        /// 塔で出せる魔物（PlacementBiomes の順）。塔が立つバイオームの通常魔物で、塔の最初の魔物と試合中の編成に使う。
        public List<MonsterData>[] TowerMonsters = { new List<MonsterData>(), new List<MonsterData>(), new List<MonsterData>() };

        /// そのバイオームの塔で出せる魔物。バイオームの外（旧ステージなど）は全バイオームの魔物（DemonDeck）。
        public List<MonsterData> TowerMonstersFor(StageMap.Biome biome)
        {
            int i = PlacementIndex(biome);
            return i >= 0 && TowerMonsters != null && i < TowerMonsters.Length && TowerMonsters[i] != null ? TowerMonsters[i] : DemonDeck;
        }

        /// 塔で出せる魔物をバイオームごとに決める。地図で置いた通常魔物（PlacementChosen のとき）、なければ BiomeSpawnTable の魔物。
        /// DemonDeck は全バイオームの分をまとめたもの。
        public void SetTowerMonsters()
        {
            var table = Game.Monster.BiomeSpawnData.Load();
            DemonDeck = new List<MonsterData>();
            TowerMonsters = new List<MonsterData>[BIOME_COUNT];
            for (int b = 0; b < BIOME_COUNT; b++)
            {
                var list = new List<MonsterData>();
                if (PlacementChosen)
                    for (int i = 0; i < BIOME_MONSTER_SLOTS; i++)
                    {
                        var m = BiomeMonsters[b * BIOME_MONSTER_SLOTS + i];
                        if (m != null && !list.Contains(m)) list.Add(m);
                    }
                var entry = list.Count == 0 && table != null ? table.For(PlacementBiomes[b]) : null;
                if (entry != null) foreach (var m in entry.monsters) if (m != null && !list.Contains(m)) list.Add(m);
                TowerMonsters[b] = list;
                foreach (var m in list) if (!DemonDeck.Contains(m)) DemonDeck.Add(m);
            }
        }

        /// 魔王ロビーを通らない試合（勇者でプレイして魔王が BOT のときなど）の配置。
        /// 自然湧きは表のまま、中ボスと番人は全部置き、塔はそのバイオームの表の魔物。
        public void UseDefaultDemonPlacement()
        {
            PlacementChosen = false;
            for (int i = 0; i < BiomeBossOn.Length; i++) BiomeBossOn[i] = true;
            for (int i = 0; i < BridgeGuardianOn.Length; i++) BridgeGuardianOn[i] = true;
            for (int i = 0; i < BridgeGuardians.Length; i++) BridgeGuardians[i] = null;
            for (int i = 0; i < FieldSpells.Length; i++) FieldSpells[i] = null;
            SetTowerMonsters();
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
            // 塔の魔物の既定は、塔が立つバイオームの表の魔物。
            if (DemonDeck == null || DemonDeck.Count == 0) UseDefaultDemonPlacement();
        }
    }
}
