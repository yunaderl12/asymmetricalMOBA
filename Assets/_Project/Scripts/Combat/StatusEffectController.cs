using System;
using UnityEngine;
using Game.Core;

namespace Game.Combat
{
    public enum StatusEffectType { None, Stun, Slow, DamageOverTime, Silence }

    /// Stun / Slow / DamageOverTime / Silence on one character.
    /// Re-applying the same type overwrites it, keeping the longer remaining time and the stronger power.
    /// After a stun ends, further stuns are ignored for BalanceConfig.stunImmunitySeconds.
    /// Added on demand by Apply(), so prefabs don't need it.
    public class StatusEffectController : MonoBehaviour
    {
        private const string SlowSourceId = "Status_Slow";
        private static readonly int TypeCount = Enum.GetValues(typeof(StatusEffectType)).Length;

        private struct ActiveEffect
        {
            public float remaining;
            public float power;
            public GameObject source;
        }

        private readonly ActiveEffect[] effects = new ActiveEffect[TypeCount];
        private HealthComponent health;
        private StatModifierContainer mods;
        private float stunImmuneUntil;
        private float dotTickTimer;
        private float appliedSlow;

        public bool IsStunned => IsActive(StatusEffectType.Stun);
        public bool IsSilenced => IsActive(StatusEffectType.Silence);

        public event Action OnEffectsChanged;

        public bool IsActive(StatusEffectType type) => type != StatusEffectType.None && effects[(int)type].remaining > 0f;
        public float Remaining(StatusEffectType type) => type != StatusEffectType.None ? Mathf.Max(0f, effects[(int)type].remaining) : 0f;

        // --- static helpers for callers that only have a GameObject ---

        public static StatusEffectController Apply(GameObject target, StatusEffectType type, float duration, float power, GameObject source)
        {
            if (target == null || type == StatusEffectType.None || duration <= 0f) return null;
            var sec = target.GetComponent<StatusEffectController>();
            if (sec == null) sec = target.AddComponent<StatusEffectController>();
            sec.Apply(type, duration, power, source);
            return sec;
        }

        public static bool IsStunnedObj(GameObject go)
        {
            return go != null && go.TryGetComponent<StatusEffectController>(out var sec) && sec.IsStunned;
        }

        /// Stunned or silenced characters can't use skills.
        public static bool CanUseSkills(GameObject go)
        {
            if (go == null || !go.TryGetComponent<StatusEffectController>(out var sec)) return true;
            return !sec.IsStunned && !sec.IsSilenced;
        }

        private void Awake()
        {
            health = GetComponent<HealthComponent>();
            mods = GetComponent<StatModifierContainer>();
            if (health != null) health.OnDeath += ClearAll;
        }

        private void OnDestroy()
        {
            if (health != null) health.OnDeath -= ClearAll;
        }

        public void Apply(StatusEffectType type, float duration, float power, GameObject source)
        {
            if (type == StatusEffectType.None || duration <= 0f) return;
            if (health != null && health.IsDead) return;
            if (type == StatusEffectType.Stun && Time.time < stunImmuneUntil)
            {
                Debug.Log($"[Status] {name} stun ignored (immune {stunImmuneUntil - Time.time:F1}s)");
                return;
            }

            ref var e = ref effects[(int)type];
            bool wasActive = e.remaining > 0f;
            e.remaining = Mathf.Max(e.remaining, duration);
            e.power = wasActive ? Mathf.Max(e.power, power) : power;
            e.source = source;
            if (type == StatusEffectType.DamageOverTime && !wasActive) dotTickTimer = BalanceConfig.Instance.dotTickInterval;

            Debug.Log($"[Status] {name} {type} {e.remaining:F1}s power={e.power} from {(source != null ? source.name : "null")}");
            RefreshSlow();
            OnEffectsChanged?.Invoke();
        }

        public void ClearAll()
        {
            for (int i = 0; i < effects.Length; i++) effects[i] = default;
            RefreshSlow();
            OnEffectsChanged?.Invoke();
        }

        private void Update()
        {
            bool changed = false;
            for (int i = 1; i < effects.Length; i++)
            {
                if (effects[i].remaining <= 0f) continue;
                if ((StatusEffectType)i == StatusEffectType.DamageOverTime) TickDamageOverTime();
                effects[i].remaining -= Time.deltaTime;
                if (effects[i].remaining <= 0f)
                {
                    effects[i] = default;
                    changed = true;
                    if ((StatusEffectType)i == StatusEffectType.Stun)
                        stunImmuneUntil = Time.time + BalanceConfig.Instance.stunImmunitySeconds;
                    Debug.Log($"[Status] {name} {(StatusEffectType)i} ended");
                }
            }
            if (changed)
            {
                RefreshSlow();
                OnEffectsChanged?.Invoke();
            }
        }

        private void TickDamageOverTime()
        {
            dotTickTimer -= Time.deltaTime;
            if (dotTickTimer > 0f) return;
            dotTickTimer += BalanceConfig.Instance.dotTickInterval;
            var e = effects[(int)StatusEffectType.DamageOverTime];
            if (health == null) return;
            health.SuppressHitReaction = true;
            health.TakeDamage(Mathf.RoundToInt(e.power), e.source, DamageType.True);
            health.SuppressHitReaction = false;
        }

        private void RefreshSlow()
        {
            if (mods == null) return;
            float slow = IsActive(StatusEffectType.Slow) ? Mathf.Clamp01(effects[(int)StatusEffectType.Slow].power) : 0f;
            if (Mathf.Approximately(slow, appliedSlow)) return;
            appliedSlow = slow;
            if (slow <= 0f) mods.RemoveModifier(SlowSourceId);
            else mods.AddOrReplaceModifier(new StatModifier(SlowSourceId, spd: 1f - slow));
        }
    }
}
