using System.Collections.Generic;
using UnityEngine;
using Game.Combat;

namespace Game.Skill
{
    /// 重戦士の Stomp。身を沈めて跳び上がり、斧を振り上げて勢いよく急降下する。
    /// 着地点の真下の敵には大ダメージ、周囲には衝撃波でそれより弱いダメージを与え、地面にヒビを残す。
    public class StompCast : MeleeSkillCast
    {
        private const float RiseSeconds = 0.36f;
        private const float HangSeconds = 0.06f;
        private const float ForwardDrift = 2f;
        private const float MaxFallSeconds = 3f;
        private const float DirectRadius = 1.5f;
        private const float ShakeSeconds = 0.42f;
        private const float ShakeSize = 0.2f;
        private const float DiveGhostInterval = 0.03f;
        private const float DiveGhostSeconds = 0.22f;
        private const float CrackHoldSeconds = 6f;

        private static readonly Vector3 AxeLowPosition = new Vector3(0.3f, -0.3f, 0.3f);
        private static readonly Vector3 AxeLowEuler = new Vector3(30f, -5f, -25f);
        private static readonly Vector3 AxeHighPosition = new Vector3(0.24f, -0.06f, 0.42f);
        private static readonly Vector3 AxeHighEuler = new Vector3(-8f, -12f, -18f);
        private static readonly Vector3 AxeSlamPosition = new Vector3(0.04f, -0.24f, 0.46f);
        private static readonly Vector3 AxeSlamEuler = new Vector3(85f, 0f, 0f);
        private static readonly Vector3 ShieldAsidePosition = new Vector3(-0.42f, -0.38f, 0.26f);

        private readonly List<HealthComponent> direct = new List<HealthComponent>();
        private readonly List<HealthComponent> wave = new List<HealthComponent>();
        private CharacterController characterController;
        private Vector3 drift;
        private float height;
        private float risen;
        private float fallTimer;
        private float nextGhost;
        private bool diving;

        protected override float RecoverSeconds => 0.45f;
        protected override MeleeFx.Palette Palette => MeleeFx.Ember;

        protected override void Awake()
        {
            base.Awake();
            characterController = GetComponent<CharacterController>();
        }

        protected override void OnWindUp(float k)
        {
            float e = EaseOut(k);
            PoseArms(AxeLowPosition, AxeLowEuler);
            if (ModelAnim != null) ModelAnim.SetMeleePose(0.8f * e, 0f, e, Chest + transform.right * 0.6f - transform.forward * 0.3f);
            SetCamera(new Vector3(0f, -0.3f * e, 0f), 2f * e, 0f, -3f * e);
        }

        protected override void OnRelease()
        {
            height = Skill.leapHeight * SizeScale;
            risen = 0f;
            fallTimer = 0f;
            diving = false;
            nextGhost = 0f;
            drift = transform.forward * (ForwardDrift / (RiseSeconds + HangSeconds + DiveEstimate()));
            if (Controller != null) Controller.SkillMotion = true;
            MeleeFx.DustBurst(transform.position, 0.6f, 16, 3.5f);
            ShockRing.Spawn(transform.position, 0.3f, 2f, 0.24f, 0.25f, Palette.Light);
        }

        private float DiveEstimate()
        {
            return height / Mathf.Max(DiveSpeed, 1f);
        }

        private float DiveSpeed => Skill.diveSpeed * SizeScale;

        protected override bool TickActive(float seconds)
        {
            if (seconds < RiseSeconds + HangSeconds) return Rise(seconds);
            return Dive();
        }

        private bool Rise(float seconds)
        {
            float target = height * EaseOut(Mathf.Clamp01(seconds / RiseSeconds));
            characterController.Move(Vector3.up * (target - risen) + drift * Time.deltaTime);
            risen = target;

            float k = Mathf.Clamp01(seconds / RiseSeconds);
            PoseArms(Vector3.Lerp(AxeLowPosition, AxeHighPosition, EaseOut(k)), Vector3.Lerp(AxeLowEuler, AxeHighEuler, EaseOut(k)));
            if (ModelAnim != null) ModelAnim.SetMeleePose(-0.6f * k, 0f, 1f, Chest + Vector3.up * 2f + transform.forward * 0.2f);
            SetCamera(Vector3.zero, -2f * k, 0f, 3f * k);
            return true;
        }

        private bool Dive()
        {
            if (!diving)
            {
                diving = true;
                nextGhost = 0f;
            }
            fallTimer += Time.deltaTime;
            characterController.Move(Vector3.down * (DiveSpeed * Time.deltaTime) + drift * Time.deltaTime);

            PoseArms(AxeSlamPosition, AxeSlamEuler);
            if (ModelAnim != null) ModelAnim.SetMeleePose(1f, 0f, 1f, Chest + transform.forward * 1.2f + Vector3.down * 1.4f);
            SetCamera(new Vector3(0f, 0f, 0.1f), 3f, 0f, 14f);

            if (fallTimer >= nextGhost && ModelAnim != null)
            {
                AfterImage.Spawn(ModelAnim.ModelRoot, Palette.Main, DiveGhostSeconds);
                nextGhost = fallTimer + DiveGhostInterval;
            }

            bool landed = (characterController.collisionFlags & CollisionFlags.Below) != 0;
            if (!landed && fallTimer < MaxFallSeconds) return true;
            Land();
            return false;
        }

        private void Land()
        {
            Vector3 feet = transform.position;
            Vector3 low = feet + Vector3.up * 0.6f;

            direct.Clear();
            direct.AddRange(FindEnemies(low, DirectRadius, transform.forward, -1f));
            wave.Clear();
            foreach (var hp in FindEnemies(low, ScaledRange, transform.forward, -1f))
                if (!direct.Contains(hp)) wave.Add(hp);

            int directHits = HitEnemies(direct, feet, 3f * SizeScale, Skill.directHitMultiplier);
            int waveHits = HitEnemies(wave, feet, 1.8f * SizeScale);
            Debug.Log($"[Skill] Stomp landed: direct {directHits}, wave {waveHits} (height {height:F2}m, dive {DiveSpeed:F1}m/s)");

            FireFlash.Spawn(feet + Vector3.up * 0.4f, 5f * SizeScale, 0.2f, 0.8f, Palette.Core, Palette.Main, Palette.Light, 4f);
            ShockRing.Spawn(feet, 0.5f, ScaledRange, 0.36f, 0.9f, Palette.Core);
            ShockRing.Spawn(feet, 0.3f, ScaledRange * 0.6f, 0.5f, 0.5f, Palette.Main);
            MeleeFx.DustBurst(feet, ScaledRange * 0.35f, 42, 7f * SizeScale);
            FireballVfx.SpawnSparks(feet + Vector3.up * 0.2f, Vector3.up, 34, Color.white, Palette.Spark);
            GroundCracks.Spawn(feet, ScaledRange * 0.75f, CrackHoldSeconds);
            Shake(ShakeSeconds, ShakeSize * SizeScale);
            if (Controller != null) Controller.SkillMotion = false;
        }

        protected override void OnRecover(float seconds, float k)
        {
            if (Controller != null) Controller.SkillMotion = false;
            float settle = (1f - k) * (1f - k);
            float rise = Mathf.Clamp01(k * 2f);
            PoseArms(Vector3.Lerp(AxeSlamPosition, View != null ? View.RightIdlePosition : AxeSlamPosition, rise), Vector3.Lerp(AxeSlamEuler, AxeLowEuler, rise));
            if (ModelAnim != null) ModelAnim.SetMeleePose(settle, 0f, settle, Chest + transform.forward * 1.2f + Vector3.down * 1.4f);
            SetCamera(new Vector3(0f, -0.4f * settle, 0f), 5f * settle, 0f, -2f * settle);
        }

        protected override void OnPoseEnd()
        {
            if (Controller != null) Controller.SkillMotion = false;
        }

        private void PoseArms(Vector3 axePosition, Vector3 axeEuler)
        {
            if (View == null) return;
            View.PoseRight(axePosition, axeEuler);
            View.PoseLeft(ShieldAsidePosition, Vector3.zero);
        }
    }
}
