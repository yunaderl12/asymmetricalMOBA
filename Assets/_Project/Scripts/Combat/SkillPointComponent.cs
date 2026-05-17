using System;
using UnityEngine;

namespace Game.Combat
{
    public class SkillPointComponent : MonoBehaviour
    {
        [SerializeField] private int maxSP = 50;
        [SerializeField] private int currentSP = 50;

        public int MaxSP => maxSP;
        public int CurrentSP => currentSP;

        public event Action<int, int> OnSPChanged;

        private void Awake()
        {
            currentSP = maxSP;
            OnSPChanged?.Invoke(currentSP, maxSP);
        }

        public bool TrySpendSP(int amount)
        {
            if (amount <= 0) return true;
            if (currentSP < amount) return false;
            currentSP -= amount;
            OnSPChanged?.Invoke(currentSP, maxSP);
            return true;
        }

        public void RestoreSP(int amount)
        {
            if (amount <= 0) return;
            currentSP = Mathf.Min(maxSP, currentSP + amount);
            OnSPChanged?.Invoke(currentSP, maxSP);
        }

        public void SetMaxSP(int value)
        {
            maxSP = Mathf.Max(1, value);
            currentSP = maxSP;
            OnSPChanged?.Invoke(currentSP, maxSP);
        }
    }
}
