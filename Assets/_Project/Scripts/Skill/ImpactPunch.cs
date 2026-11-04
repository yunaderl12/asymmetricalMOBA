using UnityEngine;
using Game.Hero;

namespace Game.Skill
{
    /// 被弾時の「ドムッ」。見た目のモデルだけをつぶして押し込み、弾むように戻す（当たり判定と位置は動かさない）。
    public class ImpactPunch : MonoBehaviour
    {
        private const float Duration = 0.28f;
        private const float Squash = 0.3f;
        private const float Widen = 0.24f;
        private const float Recoil = 0.32f;
        private const float TiltDegrees = 16f;

        private Transform visual;
        private Vector3 baseLocalPosition;
        private Vector3 baseLocalScale;
        private Quaternion baseLocalRotation;
        private Vector3 direction;
        private float recoil = 1f;
        private float startTime;

        /// recoilScale: 押し込みと傾きの強さ。0 ならつぶれるだけ。
        public static void Play(GameObject target, Vector3 direction, float recoilScale = 1f)
        {
            var model = target.GetComponent<CharacterModelAnimator>();
            if (model != null && model.IsBlasting) return;
            var punch = target.GetComponent<ImpactPunch>();
            if (punch == null)
            {
                var v = FindVisual(target);
                if (v == null) return;
                punch = target.AddComponent<ImpactPunch>();
                punch.visual = v;
                punch.baseLocalPosition = v.localPosition;
                punch.baseLocalScale = v.localScale;
                punch.baseLocalRotation = v.localRotation;
            }
            direction.y = 0f;
            punch.direction = direction.sqrMagnitude > 0.001f ? direction.normalized : target.transform.forward;
            punch.recoil = recoilScale;
            punch.startTime = Time.unscaledTime;
            punch.enabled = true;
        }

        /// 再生を止めて、モデルを元の形へ戻す。
        public void Stop()
        {
            if (visual != null)
            {
                visual.localPosition = baseLocalPosition;
                visual.localScale = baseLocalScale;
                visual.localRotation = baseLocalRotation;
            }
            enabled = false;
        }

        private static Transform FindVisual(GameObject target)
        {
            var anim = target.GetComponent<CharacterModelAnimator>();
            if (anim != null && anim.ModelRoot != null) return anim.ModelRoot;
            var body = target.transform.Find("Body");
            // 当たり判定付きの見た目（スポナーなど）は伸縮させない。
            if (body == null || !body.gameObject.activeSelf || body.GetComponent<Collider>() != null) return null;
            return body;
        }

        private void LateUpdate()
        {
            if (visual == null)
            {
                enabled = false;
                return;
            }
            float t = (Time.unscaledTime - startTime) / Duration;
            if (t >= 1f)
            {
                visual.localPosition = baseLocalPosition;
                visual.localScale = baseLocalScale;
                visual.localRotation = baseLocalRotation;
                enabled = false;
                return;
            }

            // 一気につぶれ、減衰しながら揺り戻す。
            float k = Mathf.Exp(-t * 4.5f) * Mathf.Cos(t * Mathf.PI * 2.6f);
            float push = Mathf.Exp(-t * 5f);
            visual.localScale = Vector3.Scale(baseLocalScale, new Vector3(1f + Widen * k, 1f - Squash * k, 1f + Widen * k));
            Vector3 offset = direction * (Recoil * push * recoil);
            if (visual.parent != null) offset = visual.parent.InverseTransformVector(offset);
            visual.localPosition = baseLocalPosition + offset;
            Vector3 axis = Vector3.Cross(Vector3.up, direction);
            Vector3 localAxis = visual.parent != null ? visual.parent.InverseTransformDirection(axis) : axis;
            visual.localRotation = Quaternion.AngleAxis(TiltDegrees * k * recoil, localAxis) * baseLocalRotation;
        }
    }
}
