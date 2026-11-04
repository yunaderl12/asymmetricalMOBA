using UnityEngine;
using Game.Combat;

namespace Game.Skill
{
    /// 共通の Detect。手を耳元へ上げて気配を探ると、足元から水色の輪が広がる。範囲内の敵に壁越しでも見える印が付く。
    public class DetectCast : MeleeSkillCast
    {
        private static readonly Color ScanColor = new Color(0.4f, 1.3f, 1.8f, 1f);
        private static readonly Vector3 HandUpPosition = new Vector3(-0.22f, -0.1f, 0.38f);
        private static readonly Vector3 HandUpEuler = new Vector3(-10f, 25f, 0f);

        protected override float RecoverSeconds => 0.25f;

        protected override void OnWindUp(float k)
        {
            Pose(EaseOut(k));
        }

        protected override void OnRelease()
        {
            ShockRing.Spawn(transform.position, 0.5f, ScaledRange, 0.8f, 0.6f, ScanColor);
            ShockRing.Spawn(transform.position, 0.3f, ScaledRange * 0.5f, 0.6f, 0.3f, ScanColor);
            int found = 0;
            if (IsLocalPlayer && CameraTransform != null)
            {
                foreach (var col in Physics.OverlapSphere(transform.position, ScaledRange, ~0, QueryTriggerInteraction.Collide))
                {
                    var hp = col.GetComponentInParent<HealthComponent>();
                    if (hp == null || hp.IsDead || !(hp.CompareTag("Monster") || hp.CompareTag("DemonKing"))) continue;
                    if (hp.GetComponent<RevealTag>() != null) continue;
                    hp.gameObject.AddComponent<RevealTag>().Seconds = Skill.duration;
                    RevealMark.Attach(hp, CameraTransform, Skill.duration);
                    found++;
                }
            }
            Debug.Log($"[Skill] Detect revealed {found} enemy(s) within {ScaledRange:F0}m");
        }

        protected override void OnRecover(float seconds, float k)
        {
            Pose((1f - k) * (1f - k));
        }

        protected override void OnPoseEnd()
        {
            if (ModelAnim != null) ModelAnim.SetGuardArm(0f, Vector3.zero);
        }

        private void Pose(float e)
        {
            if (View != null && e > 0.2f) View.PoseLeft(HandUpPosition, HandUpEuler);
            if (ModelAnim != null) ModelAnim.SetGuardArm(e, Chest + Vector3.up * 0.8f - transform.right * 0.5f);
        }

        // 同じ敵に印を重ねないための目印。時間が来たら消える。
        private class RevealTag : MonoBehaviour
        {
            public float Seconds;

            private void Update()
            {
                Seconds -= Time.deltaTime;
                if (Seconds <= 0f) Destroy(this);
            }
        }
    }
}
