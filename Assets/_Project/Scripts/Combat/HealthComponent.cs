using System;
using UnityEngine;

namespace Game.Combat
{
    public class HealthComponent : MonoBehaviour
    {
        [SerializeField] private int maxHP = 100;
        [SerializeField] private int currentHP = 100;

        public int MaxHP => maxHP;
        public int CurrentHP => currentHP;
        public bool IsDead { get; private set; }

        public event Action OnDeath;
        public event Action<int, int> OnHPChanged;

        private void Awake()
        {
            currentHP = maxHP;
            IsDead = false;
        }

        public void TakeDamage(int amount)
        {
            if (IsDead || amount <= 0) return;
            currentHP = Mathf.Max(0, currentHP - amount);
            Debug.Log($"[HealthComponent] {name} took {amount} dmg -> HP={currentHP}/{maxHP}");
            OnHPChanged?.Invoke(currentHP, maxHP);
            if (currentHP <= 0)
            {
                IsDead = true;
                OnDeath?.Invoke();
            }
        }

        public void Heal(int amount)
        {
            if (IsDead || amount <= 0) return;
            currentHP = Mathf.Min(maxHP, currentHP + amount);
            OnHPChanged?.Invoke(currentHP, maxHP);
        }

        public void ResetHealth()
        {
            currentHP = maxHP;
            IsDead = false;
            OnHPChanged?.Invoke(currentHP, maxHP);
        }

        public void SetMaxHP(int value)
        {
            maxHP = Mathf.Max(1, value);
            ResetHealth();
        }
    }
}
