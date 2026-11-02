using UnityEngine;

namespace Game.Hero
{
    /// モデルに含まれる FloatingBook を左手のそばで浮かせる。手の動きへ少し遅れて付いていき、上下にふわふわ揺れる。
    [DefaultExecutionOrder(10)]
    public class FloatingHeldItem : MonoBehaviour
    {
        private const string ItemName = "FloatingBook";
        private const string HandName = "LeftHand";

        // モデルのローカル座標での手からの位置（Unityの-Xがキャラの左）。
        private static readonly Vector3 HandOffset = new Vector3(-0.45f, 0.2f, 0.25f);
        private const float FollowSpeed = 6f;
        private const float BobHeight = 0.07f;
        private const float BobSpeed = 2.2f;
        private const float SwayDegrees = 12f;
        private const float SwaySpeed = 0.9f;
        private const float BaseYaw = -25f; // 表紙をキャラの左前へ向ける

        private Transform item;
        private Transform hand;
        private Quaternion restRotation;
        private Vector3 smoothedLocal;
        private float phase;

        /// モデルに FloatingBook が無ければ何も付けずnullを返す。
        public static FloatingHeldItem AttachIfPresent(GameObject model)
        {
            Transform found = null, foundHand = null;
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == ItemName) found = t;
                else if (t.name == HandName) foundHand = t;
            }
            if (found == null || foundHand == null) return null;

            var floating = model.AddComponent<FloatingHeldItem>();
            floating.item = found;
            floating.restRotation = found.localRotation;
            floating.hand = foundHand;
            floating.phase = Random.value * Mathf.PI * 2f;
            return floating;
        }

        private void LateUpdate()
        {
            Place();
        }

        /// 手の今の位置に合わせて本を置く。アニメーターを通さず姿勢を作るとき（肖像生成）は直接呼ぶ。
        public void Place()
        {
            // モデル基準で追うので、本体が走っても置いていかれない。
            Vector3 target = transform.InverseTransformPoint(hand.position) + HandOffset;
            if (smoothedLocal == Vector3.zero) smoothedLocal = target;
            smoothedLocal = Vector3.Lerp(smoothedLocal, target, 1f - Mathf.Exp(-FollowSpeed * Time.deltaTime));

            float t = Time.time;
            Vector3 local = smoothedLocal + Vector3.up * (Mathf.Sin(t * BobSpeed + phase) * BobHeight);
            item.position = transform.TransformPoint(local);
            float yaw = BaseYaw + Mathf.Sin(t * SwaySpeed + phase) * SwayDegrees;
            float tilt = Mathf.Sin(t * SwaySpeed * 0.7f + phase) * SwayDegrees * 0.4f;
            item.rotation = transform.rotation * Quaternion.Euler(tilt, yaw, 0f) * restRotation;
        }
    }
}
