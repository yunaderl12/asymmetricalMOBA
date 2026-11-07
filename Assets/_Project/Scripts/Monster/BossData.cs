using UnityEngine;

namespace Game.Monster
{
    /// 広場に1体だけいる中立のボス（ドラゴン・スコーピオン）の共通の数値。種類ごとの技の数値は派生クラスに置く。
    /// 勇者だけを襲い、魔王や魔物とは戦わない。倒すと大きな経験値が入る。
    public abstract class BossData : ScriptableObject
    {
        public string bossName;
        [Tooltip("Base HP. Scaled by the Demon King level when the boss appears, like monsters.")]
        public int hp = 3000;
        [Tooltip("Base attack. Each attack deals Atk x its damage scale (physical).")]
        public int atk = 40;
        public int df = 10;
        public int mDf = 10;
        [Tooltip("Exp for the last hit. Assists get half, as with monsters.")]
        public int exp = 1500;
        [Tooltip("Seconds from the kill until it appears again. 0 = never comes back.")]
        public float respawnSeconds = 300f;

        [Header("Arena")]
        [Tooltip("Heroes farther than this from the plaza center are ignored (m).")]
        public float aggroRadius = 34f;
        [Tooltip("Seconds between the end of one attack and the start of the next.")]
        public float attackInterval = 2.2f;
        [Tooltip("HP regained per second while no hero is in the arena (share of max HP).")]
        public float idleRegenShare = 0.02f;

        [Header("Model")]
        [Tooltip("Resources path of the rigged model (e.g. Dragon/dragon).")]
        public string modelPath;
        [Tooltip("Resources path of the material for every renderer of the model.")]
        public string modelMaterialPath;
        public float modelScale = 10f;
        [Tooltip("Turn of the model around Y so its head faces the boss's forward (degrees).")]
        public float modelYaw;
        [Tooltip("Size of the hit box around the body (m), before nothing else; centered on the body.")]
        public Vector3 bodySize = new Vector3(6f, 4f, 8f);
        [Tooltip("Height of the body center above the boss origin (m).")]
        public float bodyCenterHeight = 2f;

        public int ScaledHp(float levelMultiplier) => Mathf.Max(1, Mathf.RoundToInt(hp * levelMultiplier));
    }
}
