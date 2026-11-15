using UnityEngine;
using UnityEngine.UI;
using Game.Combat;
using Game.Core;
using Game.Hero;
using Game.Skill;

namespace Game.UI
{
    /// スキルホイール。スキルホイールのキー（初期は左 Shift）を押している間、画面の真ん中に紙のスキルカード5枚が輪に並ぶ。
    /// マウスを動かした向きのカードが浮き上がって金の縁が付き、真ん中の布の円にそのスキルの名前・Lv・消費MP・残りクールタイムが出る。
    /// キーを離すと、選んでいたスキルに切り替わる。見た目は試合中HUDと同じ布と紙（HudStyle）で、コードで組み立てる。
    public class SkillWheelUI : MonoBehaviour
    {
        private const float Radius = 232f;
        private const float HubSize = 236f;
        private const float CardWidth = 118f;
        private const float CardHeight = 146f;
        private const float OpenSeconds = 0.12f;
        private const float HoverScale = 1.24f;
        private const float HoverPush = 20f;
        private const float HoverSharpness = 20f;
        // マウスの動きをためる量の上限。小さいほど、向きを変えたときにすぐ切り替わる。
        private const float AimLimit = 90f;
        private const float AimDeadZone = 14f;
        private static readonly Color Backdrop = new Color(0.06f, 0.03f, 0.12f, 0.5f);
        private static readonly Color Shade = new Color(0.1f, 0.06f, 0.18f, 0.6f);
        private static readonly Color[] Tapes =
        {
            new Color(0.91f, 0.36f, 0.47f, 0.85f), new Color(0.38f, 0.72f, 0.85f, 0.85f), new Color(0.98f, 0.8f, 0.3f, 0.85f),
            new Color(0.5f, 0.82f, 0.6f, 0.85f), new Color(0.74f, 0.58f, 0.95f, 0.85f),
        };

        // シーンに置いてある古いホイール。開いたままにならないよう隠すだけで、もう使わない。
        [SerializeField] private string heroTag = "Player";
        [SerializeField] private GameObject root;

        private class Slot
        {
            public RectTransform Root;
            public HudShape Gold;
            public Image Icon;
            public HudShape IconFallback;
            public Text Name, Cost, Cooldown;
            public HudShape CostPill;
            public GameObject ShadeObject;
            public Vector2 Direction;
            public float Tilt;
            public float Hover;
        }

        private HeroInputActions inputs;
        private SkillRuntime runtime;
        private ManaComponent mana;
        private HeroFPSController controller;
        private HudStyle.Palette pal;
        private RectTransform wheel;
        private CanvasGroup group;
        private RectTransform pointer;
        private Slot[] slots;
        private Text hubName, hubLevel, hubCost, hubState, hint;
        private GameObject hubCostObject;
        private Vector2 mouseAccum;
        private int hoveredIndex = -1;
        private bool active;
        private float openK;

        private void Awake()
        {
            EnsureInputs();
            if (root != null) root.SetActive(false);
        }

        private void EnsureInputs()
        {
            if (inputs != null) return;
            inputs = new HeroInputActions();
            KeyBindings.Track(inputs.asset);
        }

        private void Start()
        {
            // Not for the demon king side of the match.
            if (GameSession.Instance != null && GameSession.Instance.CurrentRole == GameSession.PlayerRole.DemonKing)
            {
                enabled = false;
                return;
            }

            var hero = HumanHeroMarker.Instance != null ? HumanHeroMarker.Instance.gameObject : GameObject.FindWithTag(heroTag);
            if (hero == null) { Debug.LogWarning("[SkillWheel] hero not found"); return; }
            runtime = hero.GetComponent<SkillRuntime>();
            mana = hero.GetComponent<ManaComponent>();
            controller = hero.GetComponent<HeroFPSController>();
            Build();
        }

        private void OnEnable()
        {
            EnsureInputs();
            inputs.Hero.Enable();
        }

        private void OnDisable()
        {
            if (active) Close(false);
            if (inputs != null) inputs.Hero.Disable();
        }

        private void OnDestroy()
        {
            if (wheel != null) Destroy(wheel.gameObject);
        }

        // ---------------------------------------------------------------- 組み立て

        private void Build()
        {
            pal = HudStyle.HeroPalette;
            var center = new Vector2(0.5f, 0.5f);
            wheel = HudStyle.Place(HudStyle.Canvas.transform, "SkillWheel", center, center, Vector2.zero, Vector2.zero);
            group = wheel.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 0f;

            // 後ろを少し暗くする丸と、カードをつなぐ縫い目の輪。
            var back = HudStyle.Place(wheel, "Backdrop", center, center, Vector2.zero, Vector2.one * (Radius * 2f + 300f));
            HudStyle.Shape(back, "Shade", HudShape.Kind.Circle, Backdrop, 0f);
            var ring = HudStyle.Place(wheel, "Ring", center, center, Vector2.zero, Vector2.one * (Radius * 2f));
            var stitch = HudStyle.Stitch(ring, new Color(pal.Paper.r, pal.Paper.g, pal.Paper.b, 0.7f), true, 0f, 0f);
            stitch.thickness = 4f;
            stitch.dash = 16f;
            stitch.gap = 12f;

            // 真ん中から選んでいるカードを指すリボン色の針。
            pointer = HudStyle.Place(wheel, "Pointer", center, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(30f, HubSize * 0.5f + 34f));
            var tip = HudStyle.Place(pointer, "Tip", new Vector2(0.5f, 1f), center, new Vector2(0f, -6f), new Vector2(30f, 30f));
            tip.localEulerAngles = new Vector3(0f, 0f, 45f);
            HudStyle.Shape(tip, "Felt", HudShape.Kind.RoundRect, pal.Ribbon, 5f, HudStyle.FeltTexture);

            // 真ん中の布の円。
            var hub = HudStyle.Place(wheel, "Hub", center, center, Vector2.zero, Vector2.one * HubSize);
            HudStyle.Felt(hub, pal, HudShape.Kind.Circle, 0f, 10f, 7f);
            hubLevel = HudStyle.Label(hub, "Level", center, center, new Vector2(0f, 62f), new Vector2(160f, 30f), "", 22, HudStyle.Gold, TextAnchor.MiddleCenter, 3f, pal.Outline);
            hubName = HudStyle.Label(hub, "Name", center, center, new Vector2(0f, 22f), new Vector2(HubSize - 44f, 52f), "", 34, Color.white, TextAnchor.MiddleCenter, 4f, pal.Outline);
            hubName.horizontalOverflow = HorizontalWrapMode.Wrap;
            hubName.resizeTextForBestFit = true;
            hubName.resizeTextMinSize = 18;
            hubName.resizeTextMaxSize = 34;
            var pill = HudStyle.Place(hub, "Cost", center, center, new Vector2(0f, -28f), new Vector2(110f, 32f));
            HudStyle.Shape(pill, "Pill", HudShape.Kind.RoundRect, HudStyle.MpPill, 16f);
            hubCost = HudStyle.AddText(HudStyle.Filled(pill, "Text"), "", 22, HudStyle.MpPillText, TextAnchor.MiddleCenter);
            hubCostObject = pill.gameObject;
            hubState = HudStyle.Label(hub, "State", center, center, new Vector2(0f, -68f), new Vector2(200f, 30f), "", 21, pal.Light, TextAnchor.MiddleCenter, 3f, pal.Outline);

            int count = SkillRuntime.SLOT_COUNT;
            slots = new Slot[count];
            for (int i = 0; i < count; i++) slots[i] = BuildSlot(i, count);

            // 下の案内の紙。
            var note = HudStyle.Place(wheel, "Hint", center, center, new Vector2(0f, -(Radius + CardHeight * 0.5f + 62f)), new Vector2(470f, 42f));
            note.localEulerAngles = new Vector3(0f, 0f, -0.8f);
            HudStyle.Paper(note, pal, HudShape.Kind.Torn, 0f, 5, 4f);
            hint = HudStyle.AddText(HudStyle.Filled(note, "Text"), "", 21, pal.Ink, TextAnchor.MiddleCenter);
            wheel.gameObject.SetActive(false);
        }

        // 1番を真上に、時計回りに並べる。
        private Slot BuildSlot(int index, int count)
        {
            var center = new Vector2(0.5f, 0.5f);
            var top = new Vector2(0.5f, 1f);
            var topLeft = new Vector2(0f, 1f);
            float angle = index * 360f / count * Mathf.Deg2Rad;
            var slot = new Slot { Direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)), Tilt = index % 2 == 0 ? 2.5f : -2.5f };
            slot.Root = HudStyle.Place(wheel, "Slot_" + index, center, center, slot.Direction * Radius, new Vector2(CardWidth, CardHeight));

            slot.Gold = HudStyle.Shape(slot.Root, "Gold", HudShape.Kind.RoundRect, HudStyle.Gold, 15f);
            HudStyle.Stretch(slot.Gold.rectTransform, 6f);
            HudStyle.Paper(slot.Root, pal, HudShape.Kind.RoundRect, 10f);
            var tape = HudStyle.Place(slot.Root, "Tape", top, top, new Vector2(0f, 9f), new Vector2(42f, 18f));
            tape.localEulerAngles = new Vector3(0f, 0f, index % 2 == 0 ? 4f : -4f);
            HudStyle.Shape(tape, "Tape", HudShape.Kind.RoundRect, Tapes[index % Tapes.Length], 0f);

            var icon = HudStyle.Place(slot.Root, "Icon", top, top, new Vector2(0f, -18f), new Vector2(80f, 80f));
            slot.IconFallback = HudStyle.Shape(icon, "Fallback", HudShape.Kind.Circle, Color.gray);
            slot.Icon = HudStyle.AddGraphic<Image>(HudStyle.Filled(icon, "Image"));
            slot.Icon.preserveAspect = true;
            slot.Icon.raycastTarget = false;

            slot.Name = HudStyle.Label(slot.Root, "Name", top, top, new Vector2(0f, -100f), new Vector2(CardWidth - 8f, 26f), "", 20, pal.Ink, TextAnchor.MiddleCenter);
            slot.Name.resizeTextForBestFit = true;
            slot.Name.resizeTextMinSize = 12;
            slot.Name.resizeTextMaxSize = 20;
            var pill = HudStyle.Place(slot.Root, "Cost", top, top, new Vector2(0f, -124f), new Vector2(64f, 18f));
            slot.CostPill = HudStyle.Shape(pill, "Pill", HudShape.Kind.RoundRect, HudStyle.MpPill, 9f);
            slot.Cost = HudStyle.AddText(HudStyle.Filled(pill, "Text"), "", 14, HudStyle.MpPillText, TextAnchor.MiddleCenter);

            var key = HudStyle.Place(slot.Root, "Key", topLeft, topLeft, new Vector2(-8f, 8f), new Vector2(36f, 36f));
            HudStyle.Shape(key, "Felt", HudShape.Kind.RoundRect, pal.Felt, 10f, HudStyle.FeltTexture);
            HudStyle.AddText(HudStyle.Filled(key, "Text"), (index + 1).ToString(), 21, Color.white, TextAnchor.MiddleCenter);

            var shade = HudStyle.Shape(slot.Root, "Cooldown", HudShape.Kind.RoundRect, Shade, 10f);
            slot.ShadeObject = shade.gameObject;
            slot.Cooldown = HudStyle.Label(shade.rectTransform, "Text", center, center, new Vector2(0f, 12f), new Vector2(CardWidth, 50f), "", 38, Color.white, TextAnchor.MiddleCenter, 4f, pal.Outline);
            slot.ShadeObject.SetActive(false);
            return slot;
        }

        // ---------------------------------------------------------------- 動き

        private void Update()
        {
            if (wheel == null) return;
            bool held = !InGameMenu.BlocksGameInput && runtime != null && KeyBindings.IsPressed(GameKey.SkillWheel);
            if (held && !active) Open();
            else if (!held && active) Close(true);

            // 開くときは少し膨らんで出て、閉じるときはすっと消える。
            openK = Mathf.MoveTowards(openK, active ? 1f : 0f, Time.unscaledDeltaTime / OpenSeconds);
            if (!active && openK <= 0f)
            {
                if (wheel.gameObject.activeSelf) wheel.gameObject.SetActive(false);
                return;
            }
            group.alpha = openK;
            wheel.localScale = Vector3.one * Mathf.Lerp(0.86f, 1f, 1f - (1f - openK) * (1f - openK));

            if (active) ReadAim();
            float blend = 1f - Mathf.Exp(-HoverSharpness * Time.unscaledDeltaTime);
            for (int i = 0; i < slots.Length; i++) UpdateSlot(slots[i], i, blend);
            UpdateHub();
        }

        private void ReadAim()
        {
            mouseAccum += inputs.Hero.Look.ReadValue<Vector2>();
            if (mouseAccum.magnitude > AimLimit) mouseAccum = mouseAccum.normalized * AimLimit;
            if (mouseAccum.magnitude < AimDeadZone) return;
            float angle = Mathf.Atan2(mouseAccum.x, mouseAccum.y) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;
            int count = slots.Length;
            hoveredIndex = Mathf.RoundToInt(angle / (360f / count)) % count;
        }

        private void UpdateSlot(Slot slot, int index, float blend)
        {
            var skill = runtime.EquippedSkills.Count > index ? runtime.EquippedSkills[index] : null;
            RefreshSlot(slot, skill, index);
            bool hovered = index == hoveredIndex;
            slot.Hover = Mathf.Lerp(slot.Hover, hovered ? 1f : 0f, blend);
            slot.Root.anchoredPosition = slot.Direction * (Radius + HoverPush * slot.Hover);
            slot.Root.localScale = Vector3.one * Mathf.Lerp(1f, HoverScale, slot.Hover);
            slot.Root.localEulerAngles = new Vector3(0f, 0f, slot.Tilt * (1f - slot.Hover));
            var gold = slot.Gold.color;
            gold.a = slot.Hover;
            slot.Gold.color = gold;
            if (hovered) slot.Root.SetAsLastSibling();
        }

        private void RefreshSlot(Slot slot, SkillData skill, int index)
        {
            bool hasIcon = skill != null && skill.icon != null;
            slot.Icon.enabled = hasIcon;
            slot.Icon.sprite = hasIcon ? skill.icon : null;
            slot.IconFallback.enabled = !hasIcon;
            slot.Name.text = skill != null ? skill.DisplayName : "-";
            bool costs = skill != null && skill.mpCost > 0;
            if (slot.CostPill.transform.parent.gameObject.activeSelf != costs) slot.CostPill.transform.parent.gameObject.SetActive(costs);
            if (costs)
            {
                bool lacking = mana != null && mana.CurrentMP < skill.mpCost;
                slot.Cost.text = "MP " + skill.mpCost;
                slot.Cost.color = lacking ? HudStyle.Danger : HudStyle.MpPillText;
            }
            float remaining = skill != null ? runtime.GetCooldownRemaining(index) : 0f;
            bool cooling = remaining > 0f;
            if (slot.ShadeObject.activeSelf != cooling) slot.ShadeObject.SetActive(cooling);
            if (cooling) slot.Cooldown.text = remaining >= 10f ? Mathf.CeilToInt(remaining).ToString() : remaining.ToString("F1");
        }

        private void UpdateHub()
        {
            int index = hoveredIndex;
            var skill = index >= 0 && runtime.EquippedSkills.Count > index ? runtime.EquippedSkills[index] : null;
            pointer.gameObject.SetActive(index >= 0);
            if (index >= 0)
            {
                float target = -index * 360f / slots.Length;
                float current = pointer.localEulerAngles.z;
                pointer.localEulerAngles = new Vector3(0f, 0f, Mathf.LerpAngle(current, target, 1f - Mathf.Exp(-26f * Time.unscaledDeltaTime)));
            }
            if (skill == null)
            {
                hubName.text = "-";
                hubLevel.text = "";
                hubState.text = "";
                hubCostObject.SetActive(false);
                return;
            }
            int level = runtime.GetSkillLevel(index);
            hubName.text = skill.DisplayName;
            hubLevel.text = level >= skill.maxSkillLevel ? "Lv MAX" : "Lv " + level;
            hubCostObject.SetActive(skill.mpCost > 0);
            bool lacking = skill.mpCost > 0 && mana != null && mana.CurrentMP < skill.mpCost;
            if (skill.mpCost > 0)
            {
                hubCost.text = "MP " + skill.mpCost;
                hubCost.color = lacking ? HudStyle.Danger : HudStyle.MpPillText;
            }
            float remaining = runtime.GetCooldownRemaining(index);
            if (remaining > 0f)
            {
                hubState.text = "あと " + (remaining >= 10f ? Mathf.CeilToInt(remaining).ToString() : remaining.ToString("F1")) + " 秒";
                hubState.color = pal.Light;
            }
            else if (lacking)
            {
                hubState.text = "MP が足りない";
                hubState.color = HudStyle.HpLow;
            }
            else
            {
                hubState.text = "つかえる";
                hubState.color = HudStyle.Exp;
            }
        }

        private void Open()
        {
            active = true;
            mouseAccum = Vector2.zero;
            hoveredIndex = runtime.SelectedIndex;
            wheel.gameObject.SetActive(true);
            wheel.SetAsLastSibling();
            pointer.localEulerAngles = new Vector3(0f, 0f, -hoveredIndex * 360f / slots.Length);
            hint.text = "マウスを動かして選ぶ ／ " + KeyBindings.DisplayName(GameKey.SkillWheel) + " を離すと決定";
            if (controller != null) controller.LookEnabled = false;
        }

        private void Close(bool apply)
        {
            active = false;
            if (controller != null) controller.LookEnabled = true;
            if (apply && runtime != null && hoveredIndex >= 0) runtime.SetSelectedIndex(hoveredIndex);
        }
    }
}
