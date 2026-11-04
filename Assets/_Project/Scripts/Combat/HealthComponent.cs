using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat
{
    public class HealthComponent : MonoBehaviour
    {
        [SerializeField] private int maxHP = 100;
        [SerializeField] private int currentHP = 100;
        [SerializeField] private float assistWindow = 10f;

        public int MaxHP => maxHP;
        public int CurrentHP => currentHP;
        public bool IsDead { get; private set; }
        /// 継続ダメージのように、のけぞりを起こさないダメージの間だけ true にする。
        public bool SuppressHitReaction { get; set; }
        public GameObject LastAttacker => recentDamages.Count > 0 ? recentDamages[recentDamages.Count - 1].attacker : null;

        public event Action OnDeath;
        public event Action<int, int> OnHPChanged;
        public event Action<DeathInfo> OnDeathDetailed;

        public static event Action<HealthComponent, int, GameObject> AnyDamage;
        public static event Action<HealthComponent, DeathInfo> AnyDeath;

        public struct DamageRecord
        {
            public GameObject attacker;
            public float time;
            public int amount;
        }

        public struct DeathInfo
        {
            public GameObject lastHitter;
            public GameObject[] assistContributors;
        }

        private readonly List<DamageRecord> recentDamages = new List<DamageRecord>();

        private void Awake()
        {
            currentHP = maxHP;
            IsDead = false;
        }

        public void TakeDamage(int amount)
        {
            TakeDamage(amount, null);
        }

        /// Applies the defense formula: attack value is reduced by this object's Df (Physical) or mDf (Magical).
        public void TakeDamage(int attack, GameObject attacker, DamageType type)
        {
            if (IsDead || attack <= 0) return;
            int dmg = DamageCalculator.Calculate(attack, type, GetComponent<IDefenseSource>());
            TakeDamage(dmg, attacker);
        }

        /// Raw damage, no defense applied (debug commands, HP adjustments).
        public void TakeDamage(int amount, GameObject attacker)
        {
            if (IsDead || amount <= 0) return;
            currentHP = Mathf.Max(0, currentHP - amount);

            float now = Time.time;
            recentDamages.RemoveAll(r => now - r.time > assistWindow);
            recentDamages.Add(new DamageRecord { attacker = attacker, time = now, amount = amount });

            Debug.Log($"[HealthComponent] {name} took {amount} dmg from {(attacker != null ? attacker.name : "null")} -> HP={currentHP}/{maxHP}");
            OnHPChanged?.Invoke(currentHP, maxHP);
            AnyDamage?.Invoke(this, amount, attacker);

            if (currentHP <= 0)
            {
                IsDead = true;
                var info = BuildDeathInfo();
                OnDeathDetailed?.Invoke(info);
                AnyDeath?.Invoke(this, info);
                OnDeath?.Invoke();
            }
        }

        /// 直近の攻撃者のうち、T を持つ者で最後に攻撃した相手（アシスト時間内）。いなければ null。
        public GameObject LastAttackerWith<T>() where T : Component
        {
            for (int i = recentDamages.Count - 1; i >= 0; i--)
            {
                var attacker = recentDamages[i].attacker;
                if (attacker != null && attacker.GetComponentInParent<T>() != null) return attacker;
            }
            return null;
        }

        private DeathInfo BuildDeathInfo()
        {
            GameObject lastHitter = recentDamages.Count > 0 ? recentDamages[recentDamages.Count - 1].attacker : null;
            var assists = new HashSet<GameObject>();
            foreach (var r in recentDamages)
            {
                if (r.attacker == null) continue;
                if (r.attacker == lastHitter) continue;
                assists.Add(r.attacker);
            }
            var arr = new GameObject[assists.Count];
            int i = 0;
            foreach (var g in assists) arr[i++] = g;
            return new DeathInfo { lastHitter = lastHitter, assistContributors = arr };
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
            recentDamages.Clear();
            OnHPChanged?.Invoke(currentHP, maxHP);
        }

        public void SetMaxHP(int value)
        {
            maxHP = Mathf.Max(1, value);
            ResetHealth();
        }
    }
}
