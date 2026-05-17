using UnityEngine;
using UnityEngine.UI;
using Game.Combat;
using Game.Core;

namespace Game.UI
{
    public class DemonKingHUD : MonoBehaviour
    {
        [SerializeField] private string targetTag = "DemonKing";
        [SerializeField] private Image hpFillImage;
        [SerializeField] private Text hpText;
        [SerializeField] private Image mpFillImage;
        [SerializeField] private Text mpText;
        [SerializeField] private Text lvText;

        private HealthComponent health;
        private ManaComponent mana;
        private LevelComponent level;

        private void Start()
        {
            var go = GameObject.FindWithTag(targetTag);
            if (go == null)
            {
                Debug.LogWarning($"[DemonKingHUD] no GO with tag '{targetTag}'");
                return;
            }
            health = go.GetComponent<HealthComponent>();
            mana = go.GetComponent<ManaComponent>();
            level = go.GetComponent<LevelComponent>();
            if (health != null) health.OnHPChanged += OnHPChanged;
            if (mana != null) mana.OnMPChanged += OnMPChanged;
            if (level != null) level.OnLevelChanged += OnLevelChanged;
            UpdateAll();
        }

        private void OnDestroy()
        {
            if (health != null) health.OnHPChanged -= OnHPChanged;
            if (mana != null) mana.OnMPChanged -= OnMPChanged;
            if (level != null) level.OnLevelChanged -= OnLevelChanged;
        }

        private void OnHPChanged(int cur, int max) { UpdateAll(); }
        private void OnMPChanged(int cur, int max) { UpdateAll(); }
        private void OnLevelChanged(int lv) { UpdateAll(); }

        private void UpdateAll()
        {
            if (health != null)
            {
                if (hpFillImage != null) hpFillImage.fillAmount = health.MaxHP > 0 ? (float)health.CurrentHP / health.MaxHP : 0f;
                if (hpText != null) hpText.text = $"HP {health.CurrentHP} / {health.MaxHP}";
            }
            if (mana != null)
            {
                if (mpFillImage != null) mpFillImage.fillAmount = mana.MaxMP > 0 ? (float)mana.CurrentMP / mana.MaxMP : 0f;
                if (mpText != null) mpText.text = $"MP {mana.CurrentMP} / {mana.MaxMP}";
            }
            if (level != null && lvText != null) lvText.text = $"Lv {level.CurrentLevel} / {level.MaxLevel}";
        }
    }
}
