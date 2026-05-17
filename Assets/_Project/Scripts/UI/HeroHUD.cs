using UnityEngine;
using UnityEngine.UI;
using Game.Combat;

namespace Game.UI
{
    public class HeroHUD : MonoBehaviour
    {
        [SerializeField] private string heroTag = "Player";
        [SerializeField] private Image hpFillImage;
        [SerializeField] private Text hpText;

        private HealthComponent target;

        private void Start()
        {
            var hero = GameObject.FindWithTag(heroTag);
            if (hero == null)
            {
                Debug.LogWarning("[HeroHUD] Hero not found by tag '" + heroTag + "'");
                return;
            }
            target = hero.GetComponent<HealthComponent>();
            if (target != null)
            {
                target.OnHPChanged += OnHPChanged;
                UpdateUI();
            }
        }

        private void OnDestroy()
        {
            if (target != null) target.OnHPChanged -= OnHPChanged;
        }

        private void OnHPChanged(int current, int max)
        {
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (target == null) return;
            if (hpFillImage != null)
                hpFillImage.fillAmount = target.MaxHP > 0 ? (float)target.CurrentHP / target.MaxHP : 0f;
            if (hpText != null)
                hpText.text = "HP " + target.CurrentHP + " / " + target.MaxHP;
        }
    }
}
