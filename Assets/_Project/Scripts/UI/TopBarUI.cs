using UnityEngine;
using UnityEngine.UI;
using Game.Combat;

namespace Game.UI
{
    public class TopBarUI : MonoBehaviour
    {
        [Header("StartTown")]
        [SerializeField] private string startTownTag = "StartTown";
        [SerializeField] private Image startTownFill;
        [SerializeField] private Text startTownText;

        [Header("DemonKing")]
        [SerializeField] private string demonKingTag = "DemonKing";
        [SerializeField] private Image demonKingFill;
        [SerializeField] private Text demonKingText;

        private HealthComponent startTownHealth;
        private HealthComponent demonKingHealth;

        private void Start()
        {
            HealthBarGraphic.Adopt(startTownFill);
            HealthBarGraphic.Adopt(demonKingFill);
            BindStartTown();
            BindDemonKing();
            UpdateStartTown();
            UpdateDemonKing();
        }

        private void OnDestroy()
        {
            if (startTownHealth != null) startTownHealth.OnHPChanged -= OnStartTownHP;
            if (demonKingHealth != null) demonKingHealth.OnHPChanged -= OnDemonKingHP;
        }

        private void BindStartTown()
        {
            var go = GameObject.FindWithTag(startTownTag);
            if (go == null) { Debug.LogWarning("[TopBarUI] no StartTown"); return; }
            startTownHealth = go.GetComponent<HealthComponent>();
            if (startTownHealth == null) startTownHealth = go.GetComponentInChildren<HealthComponent>();
            if (startTownHealth != null) startTownHealth.OnHPChanged += OnStartTownHP;
        }

        private void BindDemonKing()
        {
            var go = GameObject.FindWithTag(demonKingTag);
            if (go == null) { Debug.LogWarning("[TopBarUI] no DemonKing"); return; }
            demonKingHealth = go.GetComponent<HealthComponent>();
            if (demonKingHealth != null) demonKingHealth.OnHPChanged += OnDemonKingHP;
        }

        private void OnStartTownHP(int cur, int max) { UpdateStartTown(); }
        private void OnDemonKingHP(int cur, int max) { UpdateDemonKing(); }

        private void UpdateStartTown()
        {
            if (startTownHealth == null) return;
            if (startTownFill != null) startTownFill.fillAmount = startTownHealth.MaxHP > 0 ? (float)startTownHealth.CurrentHP / startTownHealth.MaxHP : 0f;
            if (startTownText != null) startTownText.text = $"StartTown HP {startTownHealth.CurrentHP} / {startTownHealth.MaxHP}";
        }

        private void UpdateDemonKing()
        {
            if (demonKingHealth == null) return;
            if (demonKingFill != null) demonKingFill.fillAmount = demonKingHealth.MaxHP > 0 ? (float)demonKingHealth.CurrentHP / demonKingHealth.MaxHP : 0f;
            if (demonKingText != null) demonKingText.text = $"魔王 HP {demonKingHealth.CurrentHP} / {demonKingHealth.MaxHP}";
        }
    }
}
