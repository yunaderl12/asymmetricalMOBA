using System;
using UnityEngine;

namespace Game.Combat
{
    [Serializable]
    public class StatModifier
    {
        public string sourceId;
        public float atkMultiplier = 1f;
        public float spdMultiplier = 1f;
        public float dfMultiplier = 1f;

        public StatModifier() { }

        public StatModifier(string sourceId, float atk = 1f, float spd = 1f, float df = 1f)
        {
            this.sourceId = sourceId;
            atkMultiplier = atk;
            spdMultiplier = spd;
            dfMultiplier = df;
        }
    }
}
