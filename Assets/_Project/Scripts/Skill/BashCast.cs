using System.Collections.Generic;
using UnityEngine;
using Game.Combat;

namespace Game.Skill
{
    /// 重戦士の Bash。盾を前に構えて前方へ高速で突っ込む。止まらずに進み、触れた敵すべてを1回ずつ前へ大きく突き飛ばす。
    public class BashCast : MeleeSkillCast
    {
        private const float ProbeRadius = 1.8f;
        private const float ProbeCone = 130f;
        private const float StuckRatio = 0.25f;
        private const float GhostInterval = 0.04f;
        private const float GhostSeconds = 0.26f;
        private const float ShakeSeconds = 0.24f;
        private const float ShakeSize = 0.1f;
        private const float DustInterval = 0.05f;

        private static readonly Vector3 AxeBackPosition = new Vector3(0.36f, -0.22f, 0.22f);
        private static readonly Vector3 AxeBackEuler = new Vector3(15f, 0f, -35f);
        private static readonly Vector3 ShieldFrontPosition = new Vector3(-0.08f, -0.16f, 0.36f);
        private static readonly Vector3 ShieldFrontEuler = new Vector3(0f, -15f, 0f);
        private static readonly Vector3 ShieldReadyPosition = new Vector3(-0.2f, -0.2f, 0.32f);

        private readonly HashSet<HealthComponent> alreadyHit = new HashSet<HealthComponent>();
        private readonly List<HealthComponent> single = new List<HealthComponent>(1);
        private CharacterController characterController;
        private Vector3 direction;
        private float traveled;
        private float nextGhost;
        private float nextDust;
        private ParticleSystem speedLines;

        protected override float RecoverSeconds => 0.3f;
        protected override MeleeFx.Palette Palette => MeleeFx.Ember;

        protected override void Awake()
        {
            base.Awake();
            characterController = GetComponent<CharacterController>();
        }

        protected override void OnWindUp(float k)
        {
            float e = EaseOut(k);
            if (View != null)
            {
                View.PoseRight(AxeBackPosition, AxeBackEuler);
                View.PoseLeft(Vector3.Lerp(View.LeftIdlePosition, ShieldReadyPosition, e), ShieldFrontEuler);
            }
            PoseBody(0.9f * e, e);
            SetCamera(new Vector3(0f, -0.22f * e, -0.12f * e), 1.5f * e, 0f, -4f * e);
        }

        protected override void OnRelease()
        {
            direction = transform.forward;
            traveled = 0f;
            nextGhost = nextDust = 0f;
            alreadyHit.Clear();
            MeleeFx.DustBurst(transform.position, 0.5f, 18, 4f);
            ShockRing.Spawn(transform.position, 0.3f, 2.6f * SizeScale, 0.28f, 0.3f, Palette.Main);
            if (IsLocalPlayer && CameraTransform != null) speedLines = MeleeFx.SpawnSpeedLines(CameraTransform, Palette.Light);
        }

        protected override bool TickActive(float seconds)
        {
            if (View != null)
            {
                View.PoseRight(AxeBackPosition, AxeBackEuler);
                View.PoseLeft(ShieldFrontPosition, ShieldFrontEuler);
            }
            PoseBody(1f, 1f);
            SetCamera(new Vector3(0f, -0.12f, 0.12f), 1f, 0f, 13f);

            if (seconds >= nextGhost && ModelAnim != null)
            {
                AfterImage.Spawn(ModelAnim.ModelRoot, Palette.Main, GhostSeconds);
                nextGhost = seconds + GhostInterval;
            }
            if (seconds >= nextDust)
            {
                MeleeFx.DustBurst(transform.position, 0.4f, 5, 2.5f);
                nextDust = seconds + DustInterval;
            }

            HitNewTargets();

            float step = Mathf.Min(Skill.dashSpeed * Time.deltaTime, Skill.range - traveled);
            Vector3 before = transform.position;
            characterController.Move(direction * step);
            float moved = Vector3.ProjectOnPlane(transform.position - before, Vector3.up).magnitude;
            traveled += moved;
            if (traveled >= Skill.range - 0.01f) return false;
            if (step > 0f && moved < step * StuckRatio)
            {
                // 壁や、押せない敵にぶつかった。
                MeleeFx.Impact(Chest + direction * 0.8f, -direction, 1.8f * SizeScale, Palette);
                Shake(ShakeSeconds, ShakeSize * 0.6f);
                return false;
            }
            return true;
        }

        private void HitNewTargets()
        {
            var found = FindEnemies(Chest, ProbeRadius, direction, ProbeCone);
            for (int i = 0; i < found.Count; i++)
            {
                var hp = found[i];
                if (!alreadyHit.Add(hp)) continue;
                single.Clear();
                single.Add(hp);
                HitEnemies(single, Chest, 2.6f * SizeScale);
                if (hp != null) SkillKnockback.Play(hp.gameObject, direction, Skill.knockbackDistance * SizeScale);
                ShockRing.Spawn(hp.transform.position, 0.4f, 3.2f * SizeScale, 0.3f, 0.35f, Palette.Core);
                FireballVfx.SpawnSparks(Chest + direction * 0.8f, direction, 20, Color.white, Palette.Spark);
                Shake(ShakeSeconds, ShakeSize * SizeScale);
                Debug.Log($"[Skill] Bash hit {hp.name}");
            }
        }

        private void PoseBody(float lean, float arm)
        {
            if (ModelAnim == null) return;
            ModelAnim.SetMeleePose(lean, 0f, arm * 0.6f, Chest - transform.forward * 0.5f + transform.right * 0.6f);
            ModelAnim.SetGuardArm(arm, Chest + transform.forward * 1.2f);
        }

        protected override void OnRecover(float seconds, float k)
        {
            StopLines();
            float settle = (1f - k) * (1f - k);
            SetCamera(new Vector3(0f, -0.12f * settle, 0.12f * settle), settle, 0f, 13f * settle);
            PoseBody(settle, settle);
        }

        protected override void OnPoseEnd()
        {
            StopLines();
            if (ModelAnim != null) ModelAnim.SetGuardArm(0f, Vector3.zero);
        }

        private void StopLines()
        {
            if (speedLines == null) return;
            speedLines.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            speedLines = null;
        }
    }
}
