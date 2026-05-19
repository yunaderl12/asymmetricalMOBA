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

        public PlayerRole CurrentRole = PlayerRole.None;
        public HeroClassData SelectedHeroClass;
        public SkillData SelectedCommonSkill;
        public List<MonsterData> DemonDeck = new List<MonsterData>();
        public MatchStats LastMatchStats;
        public MatchManager.Winner LastWinner = MatchManager.Winner.None;

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
            if (DemonDeck == null || DemonDeck.Count == 0)
            {
                if (DemonDeck == null) DemonDeck = new List<MonsterData>();
                var basic = UnityEditor.AssetDatabase.LoadAssetAtPath<MonsterData>("Assets/_Project/ScriptableObjects/Monsters/BasicMob.asset");
                if (basic != null) DemonDeck.Add(basic);
            }
#endif
        }
    }
}
