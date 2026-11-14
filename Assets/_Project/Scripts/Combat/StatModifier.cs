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
        public float damageTakenMultiplier = 1f;
        /// 魔法防御 mDf への倍率（勇者だけが使う）。
        public float mdfMultiplier = 1f;

        public StatModifier() { }

        public StatModifier(string sourceId, float atk = 1f, float spd = 1f, float df = 1f, float damageTaken = 1f)
        {
            this.sourceId = sourceId;
            atkMultiplier = atk;
            spdMultiplier = spd;
            dfMultiplier = df;
            damageTakenMultiplier = damageTaken;
        }
    }
}
