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
        [SerializeField] private Image mpFillImage;
        [SerializeField] private Text mpText;
        [SerializeField] private Image expFillImage;
        [SerializeField] private Text expText;
        [SerializeField] private Text lvText;

        [Header("Skill Slots (5)")]
        [SerializeField] private Image[] slotBackgrounds;
        [SerializeField] private Text[] slotNameTexts;
        [SerializeField] private Text[] slotSpCostTexts;
        [SerializeField] private Text[] slotCooldownTexts;
        [SerializeField] private Image[] slotCooldownOverlays;
        [SerializeField] private Text[] slotLevelTexts;
        [SerializeField] private Color slotNormalColor = new Color(0.1f, 0.1f, 0.15f, 0.8f);
        [SerializeField] private Color slotSelectedColor = new Color(1f, 0.85f, 0.2f, 0.9f);

        private HealthComponent health;
        private ManaComponent mana;
        private LevelComponent level;
        private ExpComponent exp;
        private SkillRuntime runtime;

        private void Start()
        {
            var hero = Game.Hero.HumanHeroMarker.Instance != null ? Game.Hero.HumanHeroMarker.Instance.gameObject : GameObject.FindWithTag(heroTag);
            if (hero == null)
            {
                Debug.LogWarning("[HeroHUD] Hero not found by tag '" + heroTag + "'");
                return;
            }
            health = hero.GetComponent<HealthComponent>();
            mana = hero.GetComponent<ManaComponent>();
            level = hero.GetComponent<LevelComponent>();
            exp = hero.GetComponent<ExpComponent>();
            runtime = hero.GetComponent<SkillRuntime>();
            if (health != null) health.OnHPChanged += OnHPChanged;
            if (mana != null) mana.OnMPChanged += OnMPChanged;
            if (level != null) level.OnLevelChanged += OnLevelChanged;
            if (exp != null) exp.OnExpChanged += OnExpChanged;
            if (runtime != null) { runtime.OnSelectionChanged += OnSelectionChanged; runtime.OnSkillCast += OnSkillCast; runtime.OnSkillLevelChanged += OnSkillLevelChanged; }
            RefreshSlots();
            UpdateAll();
        }

        private void OnDestroy()
        {
            if (health != null) health.OnHPChanged -= OnHPChanged;
            if (mana != null) mana.OnMPChanged -= OnMPChanged;
            if (level != null) level.OnLevelChanged -= OnLevelChanged;
            if (exp != null) exp.OnExpChanged -= OnExpChanged;
            if (runtime != null) { runtime.OnSelectionChanged -= OnSelectionChanged; runtime.OnSkillCast -= OnSkillCast; runtime.OnSkillLevelChanged -= OnSkillLevelChanged; }
        }

        private void OnHPChanged(int cur, int max) { UpdateAll(); }
        private void OnMPChanged(int cur, int max) { UpdateAll(); }
        private void OnLevelChanged(int lv) { UpdateAll(); }
        private void OnExpChanged(int cur, int req) { UpdateAll(); }
        private void OnSelectionChanged(int idx) { RefreshSlots(); }
        private void OnSkillCast(int idx) { RefreshSlots(); }
        private void OnSkillLevelChanged(int slot, int lv) { RefreshSlots(); }

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
                if (slotLevelTexts != null && i < slotLevelTexts.Length && slotLevelTexts[i] != null)
                {
                    if (s == null) slotLevelTexts[i].text = "";
                    else
                    {
                        int lv = runtime.GetSkillLevel(i);
                        slotLevelTexts[i].text = lv >= s.maxSkillLevel ? ("Lv " + lv + " MAX") : ("Lv " + lv);
                    }
                }
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
            // SP bar removed (heroes now use MP for skills)
            if (mana != null)
            {
                if (mpFillImage != null)
                    mpFillImage.fillAmount = mana.MaxMP > 0 ? (float)mana.CurrentMP / mana.MaxMP : 0f;
                if (mpText != null)
                    mpText.text = $"MP {mana.CurrentMP} / {mana.MaxMP}";
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
