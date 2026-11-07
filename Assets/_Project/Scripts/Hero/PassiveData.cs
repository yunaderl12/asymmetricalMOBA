using System.Collections.Generic;
using UnityEngine;

namespace Game.Hero
{
    public enum PassiveType { QuickDodge, ChargedSlash, DoubleJump, BlockCircle }

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
                default: return cost.TrimEnd('　');
            }
        }
    }
}
