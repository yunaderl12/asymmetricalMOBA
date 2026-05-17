using UnityEngine;
using UnityEngine.UI;
using Game.Combat;

namespace Game.UI
{
    public class HealthBarUI : MonoBehaviour
    {
        [SerializeField] private HealthComponent target;
        [SerializeField] private Image fillImage;
        [SerializeField] private bool billboard = true;

        private Transform camTransform;

        private void Awake()
        {
            if (target == null) target = GetComponentInParent<HealthComponent>();
        }

        private void OnEnable()
        {
            if (target != null) target.OnHPChanged += OnHPChanged;
            UpdateFill();
        }

        private void OnDisable()
        {
            if (target != null) target.OnHPChanged -= OnHPChanged;
        }

        private void OnHPChanged(int current, int max)
        {
            UpdateFill();
        }

        private void UpdateFill()
        {
            if (target == null || fillImage == null) return;
            fillImage.fillAmount = target.MaxHP > 0 ? (float)target.CurrentHP / target.MaxHP : 0f;
        }

        private void LateUpdate()
        {
            if (!billboard) return;
            if (camTransform == null) camTransform = FindCameraTransform();
            if (camTransform == null) return;

            transform.LookAt(camTransform);
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
