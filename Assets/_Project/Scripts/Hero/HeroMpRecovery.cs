using UnityEngine;
using Game.Combat;
using Game.Core;

namespace Game.Hero
{
    /// MPの回復。自然回復は微量で、魔物に通常攻撃を当てると回復し、はじまりのまちに戻ると全回復する。
    public static class HeroMpRecovery
    {
        private static Transform town;

        public static void OnBasicHit(GameObject attacker, HealthComponent target)
        {
            if (attacker == null || target == null) return;
            if (target.GetComponentInParent<Game.Monster.Monster>() == null) return;
            var mana = attacker.GetComponent<ManaComponent>();
            if (mana != null) mana.RestoreMana(Mathf.CeilToInt(mana.MaxMP * BalanceConfig.Instance.mpPerBasicHit));
        }

        public static void RefillInTown(GameObject hero, ManaComponent mana)
        {
            if (mana == null || mana.CurrentMP >= mana.MaxMP) return;
            if (town == null)
            {
                var go = GameObject.FindWithTag("StartTown");
                if (go == null) return;
                town = go.transform;
            }
            Vector3 offset = hero.transform.position - town.position;
            offset.y = 0f;
            float radius = BalanceConfig.Instance.townRefillRadius;
            if (offset.sqrMagnitude <= radius * radius) mana.RestoreMana(mana.MaxMP);
        }
    }
}
