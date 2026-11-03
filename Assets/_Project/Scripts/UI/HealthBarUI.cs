using UnityEngine;
using UnityEngine.UI;
using Game.Combat;

namespace Game.UI
{
    // カメラの動きが確定したあとで向きを合わせないと、振り向くときにバーが揺れて見える。
    [DefaultExecutionOrder(1000)]
    public class HealthBarUI : MonoBehaviour
    {
        [SerializeField] private HealthComponent target;
        [SerializeField] private Image fillImage;
        [SerializeField] private bool billboard = true;

        private Transform camTransform;
        private HealthBarGraphic graphic;

        private void Awake()
        {
            if (target == null) target = GetComponentInParent<HealthComponent>();
            BuildGraphic();
        }

        // プレハブの画像は色だけ受け継ぎ、描画は HealthBarGraphic に任せる。
        private void BuildGraphic()
        {
            if (fillImage != null) graphic = HealthBarGraphic.Replace(fillImage);
        }

        /// 持ち主の足元からの高さへ移す（モデルの大きさはキャラごとに違う）。
        public void SetHeight(float height)
        {
            var p = transform.localPosition;
            p.y = height;
            transform.localPosition = p;
        }

        private void OnEnable()
        {
            if (target != null) target.OnHPChanged += OnHPChanged;
            UpdateFill(true);
        }

        private void OnDisable()
        {
            if (target != null) target.OnHPChanged -= OnHPChanged;
        }

        private void OnHPChanged(int current, int max)
        {
            UpdateFill();
        }

        private void UpdateFill(bool immediate = false)
        {
            if (target == null || graphic == null) return;
            graphic.SetValue(target.MaxHP > 0 ? (float)target.CurrentHP / target.MaxHP : 0f, immediate);
        }

        private void LateUpdate()
        {
            if (!billboard) return;
            // Re-acquire when the active camera changes (e.g. Demon King TPS <-> overhead).
            if (camTransform == null || !camTransform.gameObject.activeInHierarchy) camTransform = FindCameraTransform();
            if (camTransform == null) return;

            // Match the camera's rotation instead of LookAt: a UI canvas faces -forward,
            // so LookAt(camera) showed the text mirrored.
            transform.rotation = camTransform.rotation;
        }

        private static Transform FindCameraTransform()
        {
            var main = Camera.main;
            if (main != null) return main.transform;
            var all = Camera.allCameras;
            return all.Length > 0 ? all[0].transform : null;
        }
    }
}
