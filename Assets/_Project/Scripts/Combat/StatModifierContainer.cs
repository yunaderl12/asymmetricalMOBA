using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat
{
    public class StatModifierContainer : MonoBehaviour
    {
        [SerializeField] private List<StatModifier> activeModifiers = new List<StatModifier>();

        public float TotalAtkMultiplier { get; private set; } = 1f;
        public float TotalSpdMultiplier { get; private set; } = 1f;
        public float TotalDfMultiplier { get; private set; } = 1f;

        public event Action OnModifiersChanged;

        public IReadOnlyList<StatModifier> ActiveModifiers => activeModifiers;

        private void Awake()
        {
            Recalculate();
        }

        public void AddOrReplaceModifier(StatModifier mod)
        {
            if (mod == null || string.IsNullOrEmpty(mod.sourceId)) return;
            int idx = activeModifiers.FindIndex(m => m.sourceId == mod.sourceId);
            if (idx >= 0) activeModifiers[idx] = mod;
            else activeModifiers.Add(mod);
            Recalculate();
            OnModifiersChanged?.Invoke();
        }

        public bool RemoveModifier(string sourceId)
        {
            int idx = activeModifiers.FindIndex(m => m.sourceId == sourceId);
            if (idx < 0) return false;
            activeModifiers.RemoveAt(idx);
            Recalculate();
            OnModifiersChanged?.Invoke();
            return true;
        }

        private void Recalculate()
        {
            float a = 1f, s = 1f, d = 1f;
            foreach (var m in activeModifiers)
            {
                a *= m.atkMultiplier;
                s *= m.spdMultiplier;
                d *= m.dfMultiplier;
            }
            TotalAtkMultiplier = a;
            TotalSpdMultiplier = s;
            TotalDfMultiplier = d;
        }
    }
}
