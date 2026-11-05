using System;
using UnityEngine;

namespace Game.Combat
{
    public class ManaComponent : MonoBehaviour
    {
        [SerializeField] private int maxMP = 100;
        [SerializeField] private int currentMP = 100;

        public int MaxMP => maxMP;
        public int CurrentMP => currentMP;

        public event Action<int, int> OnMPChanged;

        private void Awake()
        {
            currentMP = maxMP;
            OnMPChanged?.Invoke(currentMP, maxMP);
        }

        public bool TryUseMana(int amount)
        {
            if (amount <= 0) return true;
            if (currentMP < amount) return false;
            currentMP -= amount;
            OnMPChanged?.Invoke(currentMP, maxMP);
            return true;
        }

        /// 1未満の回復をためておき、1になったら回復する。微量の自然回復に使う。
        public void RestoreFraction(float amount)
        {
            fractionalRestore += amount;
            int whole = Mathf.FloorToInt(fractionalRestore);
            if (whole <= 0) return;
            fractionalRestore -= whole;
            RestoreMana(whole);
        }
        private float fractionalRestore;

        public void RestoreMana(int amount)
        {
            if (amount <= 0) return;
            currentMP = Mathf.Min(maxMP, currentMP + amount);
            OnMPChanged?.Invoke(currentMP, maxMP);
        }

        public void SetMaxMP(int value)
        {
            maxMP = Mathf.Max(1, value);
            currentMP = maxMP;
            OnMPChanged?.Invoke(currentMP, maxMP);
        }
    }
}
