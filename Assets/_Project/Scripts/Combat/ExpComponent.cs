using System;
using UnityEngine;
using Game.Core;

namespace Game.Combat
{
    [RequireComponent(typeof(LevelComponent))]
    public class ExpComponent : MonoBehaviour
    {
        [SerializeField] private int currentExp = 0;

        public int CurrentExp => currentExp;
        public LevelComponent Level { get; private set; }

        public event Action<int, int> OnExpChanged;

        public int RequiredExp => Level != null ? Level.CurrentLevel * 100 : int.MaxValue;

        private void Awake()
        {
            Level = GetComponent<LevelComponent>();
        }

        public void AddExp(int amount)
        {
            if (amount <= 0) return;
            currentExp += amount;
            Debug.Log($"[Exp] {name} gained {amount} exp -> {currentExp}/{RequiredExp}");
            while (Level != null && !Level.IsMaxLevel && currentExp >= RequiredExp)
            {
                int req = RequiredExp;
                currentExp -= req;
                Level.LevelUp();
            }
            if (Level != null && Level.IsMaxLevel) currentExp = 0;
            OnExpChanged?.Invoke(currentExp, RequiredExp);
        }
    }
}
