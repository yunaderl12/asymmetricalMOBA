using UnityEngine;
using Game.Combat;
using Game.Core;

namespace Game.DemonKing
{
    /// Demon King detect skill: spend MP to reveal every hero on the bird's-eye map for a while.
    /// Numbers come from BalanceConfig (dkDetectMpCost / dkDetectDuration / dkDetectCooldown).
    public class DemonKingDetect : MonoBehaviour
    {
        private ManaComponent mana;
        private float activeUntil;
        private float cooldownUntil;

        public bool IsActive => Time.time < activeUntil;
        public float ActiveRemaining => Mathf.Max(0f, activeUntil - Time.time);
        public float CooldownRemaining => Mathf.Max(0f, cooldownUntil - Time.time);
        public int MpCost => BalanceConfig.Instance.dkDetectMpCost;
        public bool HasEnoughMp => mana == null || mana.CurrentMP >= MpCost;

        private void Awake()
        {
            mana = GetComponent<ManaComponent>();
        }

        public bool TryActivate()
        {
            var cfg = BalanceConfig.Instance;
            if (CooldownRemaining > 0f)
            {
                Debug.Log($"[DKDetect] on cooldown ({CooldownRemaining:F1}s)");
                return false;
            }
            if (mana != null && !mana.TryUseMana(cfg.dkDetectMpCost))
            {
                Debug.Log($"[DKDetect] not enough MP ({mana.CurrentMP}/{cfg.dkDetectMpCost})");
                return false;
            }
            activeUntil = Time.time + cfg.dkDetectDuration;
            cooldownUntil = Time.time + cfg.dkDetectCooldown;
            Debug.Log($"[DKDetect] activated for {cfg.dkDetectDuration}s (MP -{cfg.dkDetectMpCost})");
            return true;
        }
    }
}
