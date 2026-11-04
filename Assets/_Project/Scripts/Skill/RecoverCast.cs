using UnityEngine;
using Game.Combat;

namespace Game.Skill
{
    /// 共通の Recover。胸に手を当てて息を整えると、緑の光に包まれて回復する。
    public class RecoverCast : MeleeSkillCast
    {
        private const float PillarRadius = 0.9f;
        private const float PillarHeight = 3f;
        private const float PillarSeconds = 0.8f;

        private static readonly Vector3 HandChestPosition = new Vector3(-0.14f, -0.24f, 0.36f);
        private static readonly Vector3 HandChestEuler = new Vector3(5f, 20f, 0f);

        protected override float RecoverSeconds => 0.3f;
        protected override MeleeFx.Palette Palette => MeleeFx.Verdant;

        protected override void OnWindUp(float k)
        {
            float e = EaseOut(k);
            Pose(e, 0.35f * e);
            SetCamera(new Vector3(0f, -0.04f * e, 0f), 1f * e, 0f, -1f * e);
        }

        protected override void OnRelease()
        {
            var self = GetComponent<HealthComponent>();
            if (self != null) ApplyHit(self);

            int hidden = ModelAnim != null && ModelAnim.ModelRoot != null ? ModelAnim.ModelRoot.gameObject.layer : -1;
            LightPillar.Spawn(transform.position, PillarRadius * SizeScale, PillarHeight * SizeScale, PillarSeconds, Palette.Main * 1.4f, true, hidden);
            ShockRing.Spawn(transform.position, 0.3f, 2.2f * SizeScale, 0.45f, 0.3f, Palette.Light);
            Vector3 sparks = CameraTransform != null && IsLocalPlayer ? CameraTransform.position + CameraTransform.forward * 1.2f - CameraTransform.up * 0.7f : Chest;
            FireballVfx.SpawnSparks(sparks, Vector3.up, Mathf.RoundToInt(22 * SizeScale), Color.white, Palette.Spark);
        }

        protected override void OnRecover(float seconds, float k)
        {
            float settle = (1f - k) * (1f - k);
            Pose(settle, 0.35f * settle);
            SetCamera(new Vector3(0f, -0.04f * settle, 0f), settle, 0f, -settle);
        }

        protected override void OnPoseEnd()
        {
            if (ModelAnim != null) ModelAnim.SetGuardArm(0f, Vector3.zero);
        }

        private void Pose(float arm, float lean)
        {
            if (View != null && arm > 0.2f) View.PoseLeft(HandChestPosition, HandChestEuler);
            if (ModelAnim == null) return;
            ModelAnim.SetMeleePose(lean, 0f, 0f, Vector3.zero);
            ModelAnim.SetGuardArm(arm, Chest + transform.forward * 0.3f);
        }
    }
}
