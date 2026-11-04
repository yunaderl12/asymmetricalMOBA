using UnityEngine;

namespace Game.Skill
{
    /// 重戦士の Taunt。斧を掲げて身を反らし、前に踏み込んで吠える。周囲へ赤い衝撃の輪が広がる。
    public class TauntCast : MeleeSkillCast
    {
        private const float RoarSeconds = 0.35f;
        private const float ShakeSeconds = 0.3f;
        private const float ShakeSize = 0.05f;

        private static readonly Color RoarColor = new Color(2f, 0.25f, 0.08f, 1f);
        private static readonly Vector3 AxeUpPosition = new Vector3(0.26f, -0.03f, 0.42f);
        private static readonly Vector3 AxeUpEuler = new Vector3(-12f, -8f, -22f);
        private static readonly Vector3 ShieldOutPosition = new Vector3(-0.4f, -0.14f, 0.34f);

        protected override float RecoverSeconds => 0.25f;
        protected override MeleeFx.Palette Palette => MeleeFx.Ember;

        protected override void OnWindUp(float k)
        {
            float e = EaseOut(k);
            Pose(-0.8f * e, e);
            SetCamera(new Vector3(0f, 0.04f * e, -0.06f * e), -2f * e, 0f, -2f * e);
        }

        protected override void OnRelease()
        {
            FireFlash.Spawn(Chest, 2.8f * SizeScale, 0.18f, 0f, new Color(2.6f, 0.9f, 0.4f), RoarColor, new Color(1f, 0.4f, 0.2f), 2.5f);
            ShockRing.Spawn(transform.position, 0.4f, ScaledRange, 0.45f, 0.6f, RoarColor);
            ShockRing.Spawn(transform.position, 0.3f, ScaledRange * 0.6f, 0.35f, 0.3f, Palette.Main);
            MeleeFx.DustBurst(transform.position, 0.8f, 18, 4f);
            Shake(ShakeSeconds, ShakeSize * SizeScale);
        }

        protected override bool TickActive(float seconds)
        {
            float k = Mathf.Clamp01(seconds / RoarSeconds);
            Pose(Mathf.Lerp(-0.8f, 0.5f, EaseOut(Mathf.Clamp01(k * 4f))), 1f);
            SetCamera(new Vector3(0f, 0f, 0.06f), 1.5f, 0f, 5f * (1f - k));
            return seconds < RoarSeconds;
        }

        protected override void OnRecover(float seconds, float k)
        {
            float settle = (1f - k) * (1f - k);
            Pose(0.5f * settle, settle);
            SetCamera(new Vector3(0f, 0f, 0.06f * settle), 1.5f * settle, 0f, 0f);
        }

        private void Pose(float lean, float arm)
        {
            if (View != null && arm > 0.5f)
            {
                View.PoseRight(AxeUpPosition, AxeUpEuler);
                View.PoseLeft(ShieldOutPosition, Vector3.zero);
            }
            if (ModelAnim == null) return;
            ModelAnim.SetMeleePose(lean, 0f, arm, Chest + Vector3.up * 2f + transform.right * 0.5f);
            ModelAnim.SetGuardArm(arm, Chest + Vector3.up * 0.3f - transform.right * 1.5f);
        }

        protected override void OnPoseEnd()
        {
            if (ModelAnim != null) ModelAnim.SetGuardArm(0f, Vector3.zero);
        }
    }
}
