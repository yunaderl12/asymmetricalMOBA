using UnityEngine;

namespace Game.Skill
{
    /// 剣士の Whirlwind。剣を左へ引いて力をため、体ごと2回転して周囲を斬る。カメラは回さず、腕と円形の斬撃で見せる。
    public class WhirlwindCast : MeleeSkillCast
    {
        private const float SpinSeconds = 0.42f;
        private const float SpinDegrees = 720f;
        private const float CoilDegrees = -50f;
        private const float ArcWidth = 1.2f;
        private const float ArcLift = 1.3f;
        private const float ArcTailSeconds = 0.18f;
        private const float ShakeSeconds = 0.3f;
        private const float ShakeSize = 0.06f;
        private const float ViewSweeps = 2f;

        private static readonly Vector3 CoiledPosition = new Vector3(-0.16f, -0.13f, 0.36f);
        private static readonly Vector3 CoiledEuler = new Vector3(55f, 55f, 15f);
        private static readonly Vector3 ShieldAsidePosition = new Vector3(-0.42f, -0.42f, 0.25f);

        protected override float RecoverSeconds => 0.28f;

        protected override void OnWindUp(float k)
        {
            float e = EaseOut(k);
            if (View != null)
            {
                View.PoseRight(CoiledPosition, CoiledEuler);
                View.PoseLeft(ShieldAsidePosition, Vector3.zero);
            }
            if (ModelAnim != null) ModelAnim.SetMeleePose(0.3f * e, CoilDegrees * e, e, Chest + transform.right * 1.2f);
            SetCamera(new Vector3(0f, -0.08f * e, -0.05f * e), 0f, -3f * e, -3f * e);
            if (k > 0f && k - Time.deltaTime / Mathf.Max(Skill.castTime, 0.01f) <= 0f)
                ShockRing.Spawn(transform.position, Skill.range, 0.4f, Skill.castTime, 0.2f, MeleeFx.SteelBlue);
        }

        protected override void OnRelease()
        {
            Quaternion rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            SwordArc.Spawn(transform.position + Vector3.up * 0.2f, rotation, Skill.range, ArcWidth, SpinDegrees, SpinSeconds, ArcTailSeconds, true, MeleeFx.SteelCore, MeleeFx.SteelBlue, ArcLift);
            ShockRing.Spawn(transform.position, 0.5f, Skill.range, 0.4f, 0.5f, MeleeFx.SteelLight);
            MeleeFx.DustBurst(transform.position, 1.2f, 26, 4f);

            var targets = FindEnemies(Chest, Skill.range, transform.forward, -1f);
            int hits = HitEnemies(targets, Chest, 2.2f);
            Debug.Log($"[Skill] Whirlwind hit {hits} target(s)");
            Shake(ShakeSeconds, hits > 0 ? ShakeSize : ShakeSize * 0.4f);
        }

        protected override bool TickActive(float seconds)
        {
            float t = Mathf.Clamp01(seconds / SpinSeconds);
            float spin = Mathf.Lerp(CoilDegrees, SpinDegrees, EaseOut(t));
            if (ModelAnim != null) ModelAnim.SetMeleePose(0.3f, spin, 1f, Chest + transform.right * 1.2f);

            // 自分には、剣が視界を左右に横切る動きとして見せる。
            float swing = Mathf.Cos(t * ViewSweeps * Mathf.PI * 2f);
            if (View != null)
            {
                View.PoseRight(new Vector3(swing * 0.36f, -0.13f, 0.4f), new Vector3(68f, -swing * 60f, swing * 20f));
                View.PoseLeft(ShieldAsidePosition, Vector3.zero);
            }
            SetCamera(new Vector3(0f, -0.06f, 0f), 0f, swing * 3f, 6f);
            return seconds < SpinSeconds;
        }

        protected override void OnRecover(float seconds, float k)
        {
            float settle = (1f - k) * (1f - k);
            SetCamera(new Vector3(0f, -0.06f * settle, 0f), 0f, 0f, 6f * settle);
            if (ModelAnim != null) ModelAnim.SetMeleePose(0.3f * settle, 0f, settle, Chest + transform.right * 1.2f);
        }
    }
}
