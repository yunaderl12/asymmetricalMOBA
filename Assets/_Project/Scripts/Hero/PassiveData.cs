using System.Collections.Generic;
using UnityEngine;

namespace Game.Hero
{
    public enum PassiveType { QuickDodge, ChargedSlash, DoubleJump, BlockCircle, HealRipple, Cleanse, LightStep, Unshakable, LastStand, Rage }

    /// ジョブごとの選択肢から1つ選ぶパッシブ。どの動きをするかは type で決まり、使う数値は type に対応する欄だけ。
    [CreateAssetMenu(fileName = "NewPassive", menuName = "Game/Passive Data", order = 6)]
    public class PassiveData : ScriptableObject
    {
        public string passiveName;
        public PassiveType type;
        [Tooltip("操作や発動条件。詳細欄の1行目に出す。")]
        public string howToUse;
        [TextArea] public string description;
        public Color iconColor = new Color(0.5f, 0.8f, 1f, 1f);

        [Header("Cost")]
        [UnityEngine.Serialization.FormerlySerializedAs("spCost")]
        public int mpCost;
        public float cooldown;

        [Header("Quick Dodge")]
        [Tooltip("A / D の二度押しと判定する間隔（秒）。")]
        public float doubleTapSeconds = 0.5f;
        public float dodgeDistance = 4.5f;
        public float dodgeSeconds = 0.2f;

        [Header("Charged Slash")]
        public float holdSeconds = 1f;
        [Tooltip("通常攻撃のダメージに掛ける倍率。")]
        public float slashDamageScale = 3f;
        public float slashSpeed = 30f;
        public float slashRange = 25f;
        public float slashHitRadius = 1.5f;

        [Header("Double Jump")]
        [Tooltip("空中ジャンプの上昇速度。通常のジャンプは 5。")]
        public float airJumpSpeed = 6.5f;

        [Header("Block Circle")]
        [Tooltip("防御計算後のダメージがこれ以下の魔法攻撃を完全に防ぐ。超えると魔法陣は割れ、ダメージはそのまま受ける。")]
        public int blockDamage = 15;
        [Tooltip("発動してから透明になって消えるまでの秒数。消えるまでその1個は次の攻撃に使えない。")]
        public float blockSeconds = 0.5f;
        [Tooltip("同時に出せる魔法陣は1個から始まり、勇者がこのレベルに達するごとに1個増える。")]
        public List<int> extraCircleLevels = new List<int> { 7, 14 };

        [Header("Heal Ripple (Priest)")]
        [Tooltip("回復・バフを当てた味方からこの距離（m）以内の、ほかの味方にも効果を分ける。")]
        public float rippleRadius = 4f;
        [Tooltip("分ける効果の割合。")]
        public float rippleShare = 0.3f;

        [Header("Light Step (Priest)")]
        [Tooltip("回復スキルを使ったときの移動速度の倍率と秒数。")]
        public float stepSpeedMultiplier = 1.2f;
        public float stepSeconds = 2f;

        [Header("Unshakable (Warrior)")]
        [Tooltip("押し飛ばしの距離の倍率（0 で押されない）。")]
        public float knockbackScale = 0f;
        [Tooltip("スタンの時間の倍率。")]
        public float stunScale = 0.5f;

        [Header("Last Stand (Warrior)")]
        [Tooltip("HP がこの割合を切ったら発動する（クールタイムは cooldown）。")]
        public float lastStandHpRatio = 0.3f;
        [Tooltip("Df と mDf の倍率。")]
        public float lastStandDefenseMultiplier = 1.5f;
        public float lastStandSeconds = 5f;

        [Header("Rage (Warrior)")]
        [Tooltip("最大HPのこの割合のダメージを受けると怒りが満タンになる。")]
        public float rageFullDamageRatio = 0.35f;
        [Tooltip("満タンで出す次の通常攻撃のダメージ倍率と、押し飛ばす距離（m）。")]
        public float rageDamageScale = 2f;
        public float rageKnockback = 2.5f;

        /// 詳細欄に出す数値の行。
        public string StatsLine()
        {
            string cost = mpCost > 0 ? $"消費MP {mpCost}　　" : "";
            if (cooldown > 0f) cost += $"CT {cooldown:0.#}秒　　";
            switch (type)
            {
                case PassiveType.QuickDodge: return $"{cost}距離 {dodgeDistance:0.#}m";
                case PassiveType.ChargedSlash: return $"{cost}溜め {holdSeconds:0.#}秒　　通常攻撃の{slashDamageScale:0.#}倍　　射程 {slashRange:0.#}m";
                case PassiveType.DoubleJump: return mpCost > 0 ? $"消費MP {mpCost}" : "消費MPなし";
                case PassiveType.BlockCircle: return $"防げるダメージ {blockDamage}以下　　同時に最大{1 + extraCircleLevels.Count}個（Lv{string.Join("・", extraCircleLevels)}で増加）";
                case PassiveType.HealRipple: return $"半径 {rippleRadius:0.#}m　　効果の{rippleShare * 100f:0}%";
                case PassiveType.Cleanse: return "解く: スロー・継続ダメージ・沈黙";
                case PassiveType.LightStep: return $"移動速度 +{(stepSpeedMultiplier - 1f) * 100f:0}%　　{stepSeconds:0.#}秒";
                case PassiveType.Unshakable: return $"押し飛ばし {(knockbackScale <= 0f ? "無効" : $"×{knockbackScale:0.##}")}　　スタン時間 ×{stunScale:0.##}";
                case PassiveType.LastStand: return $"{cost}HP {lastStandHpRatio * 100f:0}%未満で Df・mDf +{(lastStandDefenseMultiplier - 1f) * 100f:0}%　　{lastStandSeconds:0.#}秒";
                case PassiveType.Rage: return $"最大HPの{rageFullDamageRatio * 100f:0}%のダメージで満タン　　通常攻撃の{rageDamageScale:0.#}倍";
                default: return cost.TrimEnd('　');
            }
        }
    }
}
