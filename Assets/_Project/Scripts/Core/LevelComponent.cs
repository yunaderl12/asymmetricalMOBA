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

        public int CurrentLevel => currentLevel;
        public int MaxLevel => maxLevel;
        public bool IsMaxLevel => currentLevel >= maxLevel;

        public event Action<int> OnLevelChanged;

        private void Start()
        {
            StartCoroutine(LevelUpRoutine());
        }

        private IEnumerator LevelUpRoutine()
        {
            while (currentLevel < maxLevel)
            {
                yield return new WaitForSeconds(secondsPerLevel);
                currentLevel++;
                Debug.Log($"[Level] {name} LevelUp -> {currentLevel}");
                OnLevelChanged?.Invoke(currentLevel);
            }
        }
    }
}
