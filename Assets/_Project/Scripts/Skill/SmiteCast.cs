using System.Collections.Generic;
using UnityEngine;
using Game.Combat;

namespace Game.Skill
{
    /// 僧侶の Smite。杖を振り上げて振り下ろすと、照準の先へ天から細い光が突き刺さる。当たった敵は沈黙する。
    public class SmiteCast : PriestCast
    {
        private const float AimRadius = 0.5f;
        private const float BeamRadius = 0.45f;
        private const float BeamHeight = 26f;
        private const float BeamSeconds = 0.42f;
        private const float StrikeSeconds = 0.08f;
        private const float ShakeSeconds = 0.18f;
        private const float ShakeSize = 0.05f;

        private static readonly Vector3 WandDownPosition = new Vector3(0.1f, -0.2f, 0.46f);
        private static readonly Vector3 WandDownEuler = new Vector3(70f, -5f, -5f);

        private readonly List<HealthComponent> single = new List<HealthComponent>(1);
        private Vector3 strikeArmPoint;

        protected override void OnRelease()
        {
            ReleaseFlash(1.6f);
            Vector3 eye = CameraTransform != null ? CameraTransform.position : Chest;
            Vector3 aim = Controller != null ? Controller.AimForward : transform.forward;
            Vector3 point = eye + aim * Skill.range;
            HealthComponent target = null;
            if (FireballProjectile.Probe(eye, aim, AimRadius, Skill.range, gameObject, out var hit))
            {
                point = hit.Point;
                target = hit.Health;
            }
            Vector3 strike = target != null ? target.transform.position : point;
            strikeArmPoint = Chest + (strike - Chest).normalized * 2f;

            LightPillar.Spawn(strike, BeamRadius * SizeScale, BeamHeight, BeamSeconds, Palette.Main * 1.2f, false);
            ShockRing.Spawn(strike, 0.3f, 2.8f * SizeScale, 0.32f, 0.4f, Palette.Core);
            FireFlash.Spawn(strike + Vector3.up * 0.6f, 3f * SizeScale, 0.16f, 0.6f, Palette.Core, Palette.Main, Palette.Light, 3f);
            FireballVfx.SpawnSparks(strike + Vector3.up * 0.1f, Vector3.up, Mathf.RoundToInt(20 * SizeScale), Color.white, Palette.Spark);
            Shake(ShakeSeconds, ShakeSize * SizeScale);

            int hits = 0;
            if (target != null)
            {
                single.Clear();
                single.Add(target);
                hits = HitEnemies(single, strike + Vector3.up * 3f, 1.6f * SizeScale);
            }
            Debug.Log($"[Skill] Smite hit {hits} target(s)");
        }

        protected override bool TickActive(float seconds)
        {
            PoseArms(WandDownPosition, WandDownEuler, BookUpPosition, BookUpEuler);
            if (ModelAnim != null) ModelAnim.SetMeleePose(0.7f, 0f, 1f, strikeArmPoint);
            SetCamera(new Vector3(0f, 0f, 0.04f), 1.5f, 0f, 3f);
            return seconds < StrikeSeconds;
        }
    }
}
