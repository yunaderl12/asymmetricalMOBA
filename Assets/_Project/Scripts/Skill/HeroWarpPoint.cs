using System;
using System.Collections;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Hero;

namespace Game.Skill
{
    /// Warp point skill state for one hero.
    /// No point set: using the skill sets one at the current position (allowed area only, no SP / cooldown).
    /// Point set: using the skill starts a cast; on completion the hero moves there and the point is consumed.
    /// Allowed area: within warpTownRadius of StartTown, or not farther toward the Demon Castle than the
    /// farthest destroyed spawner (measured along the StartTown -> DemonCastle axis).
    public class HeroWarpPoint : MonoBehaviour
    {
        private const string StartTownTag = "StartTown";
        private const string DemonCastleTag = "DemonCastle";

        private HealthComponent health;
        private CharacterController cc;
        private Coroutine castRoutine;
        private bool castInterrupted;

        public bool HasPoint { get; private set; }
        public Vector3 Point { get; private set; }
        public bool IsCasting => castRoutine != null;
        public float CastRemaining { get; private set; }

        private void Awake()
        {
            health = GetComponent<HealthComponent>();
            cc = GetComponent<CharacterController>();
        }

        private void OnEnable()
        {
            if (health != null) health.OnHPChanged += OnHPChanged;
        }

        private void OnDisable()
        {
            if (health != null) health.OnHPChanged -= OnHPChanged;
            if (castRoutine != null) CancelCast("disabled");
        }

        private int lastHp = -1;
        private void OnHPChanged(int current, int max)
        {
            if (IsCasting && lastHp >= 0 && current < lastHp) castInterrupted = true;
            lastHp = current;
        }

        public bool TrySetPoint(bool ignoreArea = false)
        {
            Vector3 pos = transform.position;
            if (!ignoreArea && !IsInWarpableArea(pos))
            {
                Notify("ここにはワープ地点を設定できません（破壊済みスポナーより自陣側のみ）");
                return false;
            }
            Point = pos;
            HasPoint = true;
            Notify("ワープ地点を設定しました");
            Debug.Log($"[Warp] {name} set warp point at {pos}");
            return true;
        }

        public void ClearPoint()
        {
            HasPoint = false;
        }

        /// Starts the cast toward the current point. onComplete runs only when the hero actually warps.
        public bool BeginCast(Action onComplete)
        {
            if (!HasPoint || IsCasting) return false;
            castRoutine = StartCoroutine(CastRoutine(onComplete));
            return true;
        }

        public void CancelCast(string reason)
        {
            if (castRoutine != null) StopCoroutine(castRoutine);
            castRoutine = null;
            CastRemaining = 0f;
            Notify("ワープを中断しました");
            Debug.Log($"[Warp] {name} cast cancelled ({reason})");
        }

        private IEnumerator CastRoutine(Action onComplete)
        {
            var cfg = BalanceConfig.Instance;
            Vector3 startPos = transform.position;
            castInterrupted = false;
            lastHp = health != null ? health.CurrentHP : -1;
            CastRemaining = cfg.warpCastSeconds;
            int shownSec = -1;
            Debug.Log($"[Warp] {name} casting ({cfg.warpCastSeconds}s)");

            while (CastRemaining > 0f)
            {
                int sec = Mathf.CeilToInt(CastRemaining);
                if (sec != shownSec)
                {
                    shownSec = sec;
                    Notify($"ワープ詠唱中… {sec}", 1.1f);
                }

                Vector3 moved = transform.position - startPos;
                moved.y = 0f;
                string reason = null;
                if (health != null && health.IsDead) reason = "dead";
                else if (castInterrupted) reason = "damaged";
                else if (!StatusEffectController.CanUseSkills(gameObject)) reason = "stunned / silenced";
                else if (moved.magnitude > cfg.warpCancelMoveDistance) reason = "moved";
                if (reason != null)
                {
                    CancelCast(reason);
                    yield break;
                }

                yield return null;
                CastRemaining -= Time.deltaTime;
            }

            castRoutine = null;
            CastRemaining = 0f;
            WarpNow();
            onComplete?.Invoke();
        }

        /// Moves the hero to the point immediately and consumes it.
        public bool WarpNow()
        {
            if (!HasPoint) return false;
            Vector3 target = Point;
            bool ccWasEnabled = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false;
            transform.position = target;
            if (cc != null) cc.enabled = ccWasEnabled;
            HasPoint = false;
            Notify("ワープしました");
            Debug.Log($"[Warp] {name} warped to {target}");
            return true;
        }

        private void Notify(string msg, float duration = -1f)
        {
            // Only the local human hero gets on-screen messages; bots just log.
            if (GetComponent<HumanHeroMarker>() == null) return;
            if (Game.UI.PowerItemNotifierUI.Instance != null) Game.UI.PowerItemNotifierUI.Instance.ShowNotification(msg, duration);
        }

        /// Whether a warp point may be set at this position.
        public static bool IsInWarpableArea(Vector3 pos)
        {
            return IsInWarpableArea(pos, out _, out _);
        }

        /// progress: position along StartTown -> DemonCastle (m). limit: farthest destroyed spawner's progress (-Infinity if none).
        public static bool IsInWarpableArea(Vector3 pos, out float progress, out float limit)
        {
            progress = 0f;
            limit = float.NegativeInfinity;
            var town = GameObject.FindWithTag(StartTownTag);
            if (town == null) return true; // test scenes without a town: no restriction

            Vector3 townPos = town.transform.position;
            Vector3 toPos = pos - townPos;
            toPos.y = 0f;
            bool nearTown = toPos.magnitude <= BalanceConfig.Instance.warpTownRadius;

            var castle = GameObject.FindWithTag(DemonCastleTag);
            if (castle == null) return nearTown;
            Vector3 axis = castle.transform.position - townPos;
            axis.y = 0f;
            if (axis.sqrMagnitude < 0.01f) return nearTown;
            axis.Normalize();

            progress = Vector3.Dot(toPos, axis);
            foreach (var s in FindObjectsByType<Game.Monster.Spawner>(FindObjectsSortMode.None))
            {
                if (s == null || s.IsFunctional) continue;
                Vector3 toSpawner = s.transform.position - townPos;
                toSpawner.y = 0f;
                limit = Mathf.Max(limit, Vector3.Dot(toSpawner, axis));
            }
            return nearTown || progress <= limit;
        }
    }
}
