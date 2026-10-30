using System;
using System.Collections;
using UnityEngine;

namespace Game.Core
{
    public class LevelComponent : MonoBehaviour
    {
        [SerializeField] private int currentLevel = 1;
        [SerializeField] private int maxLevel = 15;
        [SerializeField] private float secondsPerLevel = 10f;
        [SerializeField] private bool autoLevelUpByTime = true;

        public int CurrentLevel => currentLevel;
        public int MaxLevel => maxLevel;
        public bool IsMaxLevel => currentLevel >= maxLevel;

        public event Action<int> OnLevelChanged;

        private void Start()
        {
            if (autoLevelUpByTime) StartCoroutine(LevelUpRoutine());
        }

        public void LevelUp()
        {
            if (currentLevel >= maxLevel) return;
            currentLevel++;
            Debug.Log($"[Level] {name} LevelUp -> {currentLevel}");
            OnLevelChanged?.Invoke(currentLevel);
        }

        private IEnumerator LevelUpRoutine()
        {
            var match = MatchManager.Instance;
            if (match == null)
            {
                // Test scenes without a MatchManager: count from scene start.
                while (currentLevel < maxLevel)
                {
                    yield return new WaitForSeconds(secondsPerLevel);
                    LevelUp();
                }
                yield break;
            }

            // Count from InMatch start (prep phase excluded), derived from ElapsedInMatch so it never drifts.
            while (match.CurrentState != MatchManager.MatchState.InMatch)
            {
                if (match.CurrentState == MatchManager.MatchState.Finished) yield break;
                yield return null;
            }
            while (currentLevel < maxLevel && match.CurrentState == MatchManager.MatchState.InMatch)
            {
                int target = 1 + Mathf.FloorToInt(match.ElapsedInMatch / secondsPerLevel);
                while (currentLevel < target && currentLevel < maxLevel) LevelUp();
                yield return null;
            }
        }
    }
}
