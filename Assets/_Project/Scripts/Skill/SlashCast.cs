using UnityEngine;

namespace Game.Skill
{
    /// 剣士の Slash。右上へ振りかぶり、前方の扇形へ三日月形の斬撃を走らせる。
    public class SlashCast : MeleeSkillCast
    {
        private const float SwingSeconds = 0.09f;
        private const float ArcRadius = 2.1f;
        private const float ArcWidth = 0.75f;
        private const float ArcDegrees = 125f;
        private const float ArcSweepSeconds = 0.1f;
        private const float ArcTailSeconds = 0.12f;
        private const float ArcDistance = 2f;      // 視線の先のどこに描くか
        private const float ArcTiltBack = 22f;     // 他の人から横に見ても線にならないよう、面を少し寝かせる
        private const float ArcDiagonal = -35f;    // 右上から左下へ斬り下ろす傾き
        private const float ShakeSeconds = 0.14f;
        private const float ShakeSize = 0.035f;

        private static readonly Vector3 RaisedPosition = new Vector3(0.34f, 0.02f, 0.34f);
        private static readonly Vector3 RaisedEuler = new Vector3(-30f, 30f, -35f);
        private static readonly Vector3 SwungPosition = new Vector3(-0.22f, -0.26f, 0.44f);
        private static readonly Vector3 SwungEuler = new Vector3(85f, -35f, 50f);

        protected override float RecoverSeconds => 0.26f;

        protected override void OnWindUp(float k)
        {
            float e = EaseOut(k);
            if (View != null) View.PoseRight(RaisedPosition, RaisedEuler);
            if (ModelAnim != null) ModelAnim.SetMeleePose(-0.5f * e, 0f, e, transform.position + transform.up * 2f + transform.right * 0.7f - transform.forward * 0.2f);
            SetCamera(Vector3.zero, -1.2f * e, 2f * e, 0f);
        }

        protected override void OnRelease()
        {
            Vector3 aim = Controller != null ? Controller.AimForward : transform.forward;
            Vector3 eye = CameraTransform != null ? CameraTransform.position : Chest;
            Vector3 up = CameraTransform != null ? CameraTransform.up : Vector3.up;
            SpawnArc(eye, aim, up);

            var targets = FindEnemies(Chest, ScaledRange, aim, Skill.coneAngle);
            int hits = HitEnemies(targets, eye, 1.6f);
            Debug.Log($"[Skill] Slash hit {hits} target(s)");
            if (hits > 0) Shake(ShakeSeconds, ShakeSize);
            if (ModelAnim != null) ModelAnim.PlayAttack();
        }

        // 視線にほぼ正対する面に描く。弧の中心を左上に置くと、画面の中央を右上から左下へ抜ける三日月になる。
        private void SpawnArc(Vector3 eye, Vector3 aim, Vector3 up)
        {
            Vector3 side = Vector3.Cross(up, aim).normalized;
            Vector3 bulge = Quaternion.AngleAxis(ArcDiagonal, aim) * side;
            Vector3 normal = Quaternion.AngleAxis(ArcTiltBack, bulge) * -aim;
            float radius = ArcRadius * SizeScale;
            float width = ArcWidth * SizeScale;
            Vector3 center = eye + aim * ArcDistance - bulge * (radius - width * 0.5f);
            SwordArc.Spawn(center, Quaternion.LookRotation(bulge, normal), radius, width, ArcDegrees, ArcSweepSeconds, ArcTailSeconds, true, MeleeFx.SteelCore, MeleeFx.SteelBlue);
        }

        protected override void OnRecover(float seconds, float k)
        {
            if (seconds < SwingSeconds)
            {
                if (View != null) View.PoseRight(SwungPosition, SwungEuler);
                SetCamera(new Vector3(0f, 0f, 0.05f), 1.5f, -2.5f, 2f);
                if (ModelAnim != null) ModelAnim.SetMeleePose(0.5f, 0f, 0f, Vector3.zero);
                return;
            }
            float settle = (1f - k) * (1f - k);
            SetCamera(new Vector3(0f, 0f, 0.05f * settle), 1.5f * settle, -2.5f * settle, 2f * settle);
            if (ModelAnim != null) ModelAnim.SetMeleePose(0.5f * settle, 0f, 0f, Vector3.zero);
        }
    }
}
