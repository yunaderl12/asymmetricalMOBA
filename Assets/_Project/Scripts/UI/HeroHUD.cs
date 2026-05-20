using UnityEngine;
using UnityEngine.UI;
using Game.Combat;
using Game.Core;
using Game.Skill;

namespace Game.UI
{
    public class HeroHUD : MonoBehaviour
    {
        [SerializeField] private string heroTag = "Player";
        [SerializeField] private Image hpFillImage;
        [SerializeField] private Text hpText;
        [SerializeField] private Image spFillImage;
        [SerializeField] private Text spText;
        [SerializeField] private Image expFillImage;
        [SerializeField] private Text expText;
        [SerializeField] private Text lvText;

        [Header("Skill Slots (5)")]
        [SerializeField] private Image[] slotBackgrounds;
        [SerializeField] private Text[] slotNameTexts;
        [SerializeField] private Text[] slotSpCostTexts;
        [SerializeField] private Text[] slotCooldownTexts;
        [SerializeField] private Image[] slotCooldownOverlays;
        [SerializeField] private Color slotNormalColor = new Color(0.1f, 0.1f, 0.15f, 0.8f);
        [SerializeField] private Color slotSelectedColor = new Color(1f, 0.85f, 0.2f, 0.9f);

        private HealthComponent health;
        private SkillPointComponent skill;
        private LevelComponent level;
        private ExpComponent exp;
        private SkillRuntime runtime;

        private void Start()
        {
            var hero = GameObject.FindWithTag(heroTag);
            if (hero == null)
            {
                Debug.LogWarning("[HeroHUD] Hero not found by tag '" + heroTag + "'");
                return;
            }
            health = hero.GetComponent<HealthComponent>();
            skill = hero.GetComponent<SkillPointComponent>();
            level = hero.GetComponent<LevelComponent>();
            exp = hero.GetComponent<ExpComponent>();
            runtime = hero.GetComponent<SkillRuntime>();
            if (health != null) health.OnHPChanged += OnHPChanged;
            if (skill != null) skill.OnSPChanged += OnSPChanged;
            if (level != null) level.OnLevelChanged += OnLevelChanged;
            if (exp != null) exp.OnExpChanged += OnExpChanged;
            if (runtime != null) { runtime.OnSelectionChanged += OnSelectionChanged; runtime.OnSkillCast += OnSkillCast; }
            RefreshSlots();
            UpdateAll();
        }

        private void OnDestroy()
        {
            if (health != null) health.OnHPChanged -= OnHPChanged;
            if (skill != null) skill.OnSPChanged -= OnSPChanged;
            if (level != null) level.OnLevelChanged -= OnLevelChanged;
            if (exp != null) exp.OnExpChanged -= OnExpChanged;
            if (runtime != null) { runtime.OnSelectionChanged -= OnSelectionChanged; runtime.OnSkillCast -= OnSkillCast; }
        }

        private void OnHPChanged(int cur, int max) { UpdateAll(); }
        private void OnSPChanged(int cur, int max) { UpdateAll(); }
        private void OnLevelChanged(int lv) { UpdateAll(); }
        private void OnExpChanged(int cur, int req) { UpdateAll(); }
        private void OnSelectionChanged(int idx) { RefreshSlots(); }
        private void OnSkillCast(int idx) { RefreshSlots(); }

        private void Update()
        {
            if (runtime == null || slotCooldownTexts == null) return;
            for (int i = 0; i < slotCooldownTexts.Length; i++)
            {
                if (slotCooldownTexts[i] == null) continue;
                float cd = runtime.GetCooldownRemaining(i);
                slotCooldownTexts[i].gameObject.SetActive(cd > 0f);
                if (cd > 0f) slotCooldownTexts[i].text = cd.ToString("F1");
            }
            if (slotCooldownOverlays != null)
            {
                for (int i = 0; i < slotCooldownOverlays.Length; i++)
                {
                    if (slotCooldownOverlays[i] == null) continue;
                    var skill = runtime.EquippedSkills.Count > i ? runtime.EquippedSkills[i] : null;
                    float remaining = runtime.GetCooldownRemaining(i);
                    float ratio = (skill != null && skill.cooldown > 0f) ? Mathf.Clamp01(remaining / skill.cooldown) : 0f;
                    slotCooldownOverlays[i].fillAmount = ratio;
                }
            }
        }

        private void RefreshSlots()
        {
            if (runtime == null) return;
            int selected = runtime.SelectedIndex;
            for (int i = 0; i < SkillRuntime.SLOT_COUNT; i++)
            {
                var s = runtime.EquippedSkills.Count > i ? runtime.EquippedSkills[i] : null;
                if (slotBackgrounds != null && i < slotBackgrounds.Length && slotBackgrounds[i] != null)
                    slotBackgrounds[i].color = (i == selected) ? slotSelectedColor : slotNormalColor;
                if (slotNameTexts != null && i < slotNameTexts.Length && slotNameTexts[i] != null)
                    slotNameTexts[i].text = s != null ? s.skillName : "-";
                if (slotSpCostTexts != null && i < slotSpCostTexts.Length && slotSpCostTexts[i] != null)
                    slotSpCostTexts[i].text = s != null ? "SP " + s.spCost : "";
            }
        }

        private void UpdateAll()
        {
            if (health != null)
            {
                if (hpFillImage != null)
                    hpFillImage.fillAmount = health.MaxHP > 0 ? (float)health.CurrentHP / health.MaxHP : 0f;
                if (hpText != null)
                    hpText.text = $"HP {health.CurrentHP} / {health.MaxHP}";
            }
            if (skill != null)
            {
                if (spFillImage != null)
                    spFillImage.fillAmount = skill.MaxSP > 0 ? (float)skill.CurrentSP / skill.MaxSP : 0f;
                if (spText != null)
                    spText.text = $"SP {skill.CurrentSP} / {skill.MaxSP}";
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
