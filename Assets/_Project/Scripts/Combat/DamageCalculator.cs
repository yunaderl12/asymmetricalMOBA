using UnityEngine;
using Game.Core;

namespace Game.Combat
{
    public enum DamageType { Physical, Magical, True }

    /// Anything that can mitigate incoming damage (heroes, monsters, cores).
    public interface IDefenseSource
    {
        int Df { get; }
        int MDf { get; }
    }

    public static class DamageCalculator
    {
        /// damage = attack * K / (K + defense)
        public static int Calculate(int attack, int defense)
        {
            if (attack <= 0) return 0;
            var cfg = BalanceConfig.Instance;
            float k = Mathf.Max(0.01f, cfg.damageK);
            float raw = attack * k / (k + Mathf.Max(0, defense));
            // Critical hits: undecided, hook in here.
            return Mathf.Max(cfg.minDamage, Mathf.RoundToInt(raw));
        }

        public static int Calculate(int attack, DamageType type, IDefenseSource defender)
        {
            if (type == DamageType.True || defender == null) return Mathf.Max(0, attack);
            int defense = type == DamageType.Physical ? defender.Df : defender.MDf;
            return Calculate(attack, defense);
        }
    }
}
