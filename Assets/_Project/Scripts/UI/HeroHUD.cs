using UnityEngine;
using UnityEngine.UI;
using Game.Combat;
using Game.Core;

namespace Game.UI
{
    public class HeroHUD : MonoBehaviour
    {
        [SerializeField] private string heroTag = "Player";
        [SerializeField] private Image hpFillImage;
        [SerializeField] private Text hpText;
        [SerializeField] private Image expFillImage;
        [SerializeField] private Text expText;
        [SerializeField] private Text lvText;

        private HealthComponent health;
        private LevelComponent level;
        private ExpComponent exp;

        private void Start()
        {
            var hero = GameObject.FindWithTag(heroTag);
            if (hero == null)
            {
                Debug.LogWarning("[HeroHUD] Hero not found by tag '" + heroTag + "'");
                return;
            }
            health = hero.GetComponent<HealthComponent>();
            level = hero.GetComponent<LevelComponent>();
            exp = hero.GetComponent<ExpComponent>();
            if (health != null) health.OnHPChanged += OnHPChanged;
            if (level != null) level.OnLevelChanged += OnLevelChanged;
            if (exp != null) exp.OnExpChanged += OnExpChanged;
            UpdateAll();
        }

        private void OnDestroy()
        {
            if (health != null) health.OnHPChanged -= OnHPChanged;
            if (level != null) level.OnLevelChanged -= OnLevelChanged;
            if (exp != null) exp.OnExpChanged -= OnExpChanged;
        }

        private void OnHPChanged(int cur, int max) { UpdateAll(); }
        private void OnLevelChanged(int lv) { UpdateAll(); }
        private void OnExpChanged(int cur, int req) { UpdateAll(); }

        private void UpdateAll()
        {
            if (health != null)
            {
                if (hpFillImage != null)
                    hpFillImage.fillAmount = health.MaxHP > 0 ? (float)health.CurrentHP / health.MaxHP : 0f;
                if (hpText != null)
                    hpText.text = $"HP {health.CurrentHP} / {health.MaxHP}";
            }
            if (level != null && lvText != null)
                lvText.text = $"Lv {level.CurrentLevel} / {level.MaxLevel}";
            if (exp != null)
            {
                int cur = exp.CurrentExp;
                int req = exp.RequiredExp;
                bool maxed = level != null && level.IsMaxLevel;
                if (expFillImage != null)
                    expFillImage.fillAmount = maxed ? 1f : (req > 0 ? (float)cur / req : 0f);
                if (expText != null)
                {
                    if (maxed) expText.text = "Exp MAX";
                    else expText.text = $"Exp {cur} / {req}  (next: {Mathf.Max(0, req - cur)})";
                }
            }
        }
    }
}
