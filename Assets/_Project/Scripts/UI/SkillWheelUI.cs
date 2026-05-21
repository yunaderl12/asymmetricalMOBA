using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Game.Hero;
using Game.Skill;

namespace Game.UI
{
    public class SkillWheelUI : MonoBehaviour
    {
        [SerializeField] private string heroTag = "Player";
        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform slotsParent;
        [SerializeField] private Image[] slotBackgrounds;
        [SerializeField] private Text[] slotNameTexts;
        [SerializeField] private Text[] slotSpCostTexts;
        [SerializeField] private float radius = 150f;
        [SerializeField] private Color slotNormalColor = new Color(0f, 0f, 0f, 0.7f);
        [SerializeField] private Color slotHighlightColor = new Color(1f, 0.85f, 0.2f, 0.95f);

        private HeroInputActions inputs;
        private SkillRuntime runtime;
        private HeroFPSController controller;
        private Vector2 mouseAccum;
        private int hoveredIndex = -1;
        private bool active;

        private void Awake()
        {
            if (inputs == null) inputs = new HeroInputActions();
            if (root != null) root.SetActive(false);
        }

        private void Start()
        {
            var hero = Game.Hero.HumanHeroMarker.Instance != null ? Game.Hero.HumanHeroMarker.Instance.gameObject : GameObject.FindWithTag(heroTag);
            if (hero == null) { Debug.LogWarning("[SkillWheel] hero not found"); return; }
            runtime = hero.GetComponent<SkillRuntime>();
            controller = hero.GetComponent<HeroFPSController>();
            LayoutSlots();
            RefreshSlotText();
        }

        private void OnEnable()
        {
            if (inputs == null) inputs = new HeroInputActions();
            inputs.Hero.Enable();
        }

        private void OnDisable()
        {
            if (inputs == null) return;
            inputs.Hero.Disable();
        }

        private void Update()
        {
            if (ConsoleUI.IsConsoleActive) return;
            bool held = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;

            if (held && !active) Open();
            else if (!held && active) Close();

            if (!active || runtime == null) return;

            Vector2 delta = inputs.Hero.Look.ReadValue<Vector2>();
            mouseAccum += delta;
            if (mouseAccum.sqrMagnitude < 16f) return;

            float ang = Mathf.Atan2(mouseAccum.x, mouseAccum.y) * Mathf.Rad2Deg;
            if (ang < 0f) ang += 360f;
            int slots = SkillRuntime.SLOT_COUNT;
            float step = 360f / slots;
            int idx = Mathf.RoundToInt(ang / step) % slots;
            if (idx != hoveredIndex)
            {
                hoveredIndex = idx;
                UpdateHighlight();
            }
        }

        private void Open()
        {
            if (runtime == null) return;
            active = true;
            mouseAccum = Vector2.zero;
            hoveredIndex = runtime.SelectedIndex;
            if (root != null) root.SetActive(true);
            if (controller != null) controller.LookEnabled = false;
            RefreshSlotText();
            UpdateHighlight();
        }

        private void Close()
        {
            active = false;
            if (root != null) root.SetActive(false);
            if (controller != null) controller.LookEnabled = true;
            if (runtime != null && hoveredIndex >= 0) runtime.SetSelectedIndex(hoveredIndex);
        }

        private void LayoutSlots()
        {
            if (slotBackgrounds == null) return;
            int n = slotBackgrounds.Length;
            for (int i = 0; i < n; i++)
            {
                if (slotBackgrounds[i] == null) continue;
                float ang = (i * 360f / n - 90f) * Mathf.Deg2Rad;
                Vector2 pos = new Vector2(Mathf.Cos(ang), -Mathf.Sin(ang)) * radius;
                var rt = slotBackgrounds[i].rectTransform;
                rt.anchoredPosition = pos;
            }
        }

        private void RefreshSlotText()
        {
            if (runtime == null) return;
            for (int i = 0; i < SkillRuntime.SLOT_COUNT; i++)
            {
                var s = runtime.EquippedSkills.Count > i ? runtime.EquippedSkills[i] : null;
                if (slotNameTexts != null && i < slotNameTexts.Length && slotNameTexts[i] != null)
                    slotNameTexts[i].text = s != null ? s.skillName : "-";
                if (slotSpCostTexts != null && i < slotSpCostTexts.Length && slotSpCostTexts[i] != null)
                    slotSpCostTexts[i].text = s != null ? "SP " + s.spCost : "";
            }
        }

        private void UpdateHighlight()
        {
            if (slotBackgrounds == null) return;
            for (int i = 0; i < slotBackgrounds.Length; i++)
            {
                if (slotBackgrounds[i] == null) continue;
                slotBackgrounds[i].color = (i == hoveredIndex) ? slotHighlightColor : slotNormalColor;
            }
        }
    }
}
