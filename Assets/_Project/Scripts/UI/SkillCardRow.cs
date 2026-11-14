using UnityEngine;
using UnityEngine.UI;
using Game.Combat;
using Game.Skill;

namespace Game.UI
{
    /// 試合中のスキル表示。紙のカードを5枚、少し傾けてテープで貼ったように並べる。
    /// 左上にキー番号、右上にLv、中央にイラスト、下に名前と消費MP。選択中は浮き上がって金の縁がつき、まっすぐになる。
    /// クールダウン中はグレーの幕が下がっていき、残り秒を出す。MPが足りないときはイラストを灰色にして消費MPを赤くする。物理系のスキルは消費MPの札を出さない。
    public class SkillCardRow : MonoBehaviour
    {
        private const float CardWidth = 118f;
        private const float CardHeight = 168f;
        private const float CardGap = 10f;
        private const float Margin = 40f;
        private const float SelectedLift = 26f;
        private const float SelectedScale = 1.06f;
        private const float Tilt = 1.3f;
        private const float HoverSharpness = 16f;

        private static readonly Color Shade = new Color(0.12f, 0.08f, 0.2f, 0.72f);
        private static readonly Color IconOff = new Color(0.55f, 0.55f, 0.6f, 0.45f);
        private static readonly Color[] Tapes =
        {
            new Color(0.35f, 0.75f, 0.75f, 0.8f),
            new Color(0.91f, 0.36f, 0.47f, 0.8f),
            new Color(0.96f, 0.72f, 0.16f, 0.8f),
            new Color(0.47f, 0.47f, 0.86f, 0.8f),
            new Color(0.35f, 0.75f, 0.75f, 0.8f),
        };

        private class Card
        {
            public RectTransform Root;
            public HudShape Gold;
            public Image Icon;
            public HudShape IconFallback;
            public Text Level, Name, Cost, Cooldown;
            public HudShape CostPill;
            public RectTransform ShadeRect;
            public SkillData Shown;
            public int ShownLevel = -1;
            public bool ShownShort;
            public float Hover;
            public float BaseX;
            public float RestTilt;
            public bool WasCooling;
            public float Ready; // クールタイムが明けた瞬間に 1 になり、0 へ戻る（札が跳ねて光る）
        }

        private readonly HudStyle.Palette pal = HudStyle.HeroPalette;
        private SkillRuntime runtime;
        private ManaComponent mana;
        private Card[] cards;
        private int lastSelected = -1;

        public static SkillCardRow Create(SkillRuntime runtime)
        {
            var canvas = HudStyle.Canvas.transform;
            int count = SkillRuntime.SLOT_COUNT;
            float width = count * CardWidth + (count - 1) * CardGap;
            var rt = HudStyle.Place(canvas, "SkillCards", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-Margin, Margin), new Vector2(width, CardHeight));

            var row = rt.gameObject.AddComponent<SkillCardRow>();
            row.runtime = runtime;
            row.mana = runtime.GetComponent<ManaComponent>();
            row.cards = new Card[count];
            for (int i = 0; i < count; i++) row.cards[i] = row.BuildCard(i);

            HudStyle.Label(rt, "WheelHint", new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 40f), new Vector2(300f, 30f),
                "SHIFT スキルホイール", 21, Color.white, TextAnchor.LowerLeft, 4f, row.pal.Outline);
            return row;
        }

        private Card BuildCard(int index)
        {
            var card = new Card();
            card.Root = HudStyle.Place(transform, "Card_" + index, Vector2.zero, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(CardWidth, CardHeight));
            card.BaseX = index * (CardWidth + CardGap) + CardWidth * 0.5f;
            card.RestTilt = index % 2 == 0 ? Tilt : -Tilt;
            card.Root.anchoredPosition = new Vector2(card.BaseX, 0f);

            card.Gold = HudStyle.Shape(card.Root, "Gold", HudShape.Kind.RoundRect, HudStyle.Gold, 15f);
            HudStyle.Stretch(card.Gold.rectTransform, 5f);
            HudStyle.Paper(card.Root, pal, HudShape.Kind.RoundRect, 10f);

            var top = new Vector2(0.5f, 1f);
            var topLeft = new Vector2(0f, 1f);
            var tape = HudStyle.Place(card.Root, "Tape", top, top, new Vector2(0f, 9f), new Vector2(40f, 18f));
            tape.localEulerAngles = new Vector3(0f, 0f, 4f);
            HudStyle.Shape(tape, "Tape", HudShape.Kind.RoundRect, Tapes[index % Tapes.Length], 0f);

            var icon = HudStyle.Place(card.Root, "Icon", top, top, new Vector2(0f, -22f), new Vector2(76f, 76f));
            card.IconFallback = HudStyle.Shape(icon, "Fallback", HudShape.Kind.Circle, Color.gray);
            var iconImage = HudStyle.AddGraphic<Image>(HudStyle.Filled(icon, "Image"));
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            card.Icon = iconImage;

            card.Level = HudStyle.Label(card.Root, "Level", Vector2.one, Vector2.one, new Vector2(-7f, -4f), new Vector2(60f, 24f), "", 17, HudStyle.Brown, TextAnchor.MiddleRight);
            card.Name = HudStyle.Label(card.Root, "Name", top, top, new Vector2(0f, -100f), new Vector2(CardWidth - 8f, 30f), "", 22, pal.Ink, TextAnchor.MiddleCenter);
            card.Name.horizontalOverflow = HorizontalWrapMode.Wrap;
            card.Name.resizeTextForBestFit = true;
            card.Name.resizeTextMinSize = 14;
            card.Name.resizeTextMaxSize = 22;

            var pill = HudStyle.Place(card.Root, "Cost", top, top, new Vector2(0f, -134f), new Vector2(76f, 24f));
            card.CostPill = HudStyle.Shape(pill, "Pill", HudShape.Kind.RoundRect, HudStyle.MpPill, 12f);
            card.Cost = HudStyle.AddText(HudStyle.Filled(pill, "Text"), "", 17, HudStyle.MpPillText, TextAnchor.MiddleCenter);

            // キー番号の札。カードの左上からはみ出させる。
            var key = HudStyle.Place(card.Root, "Key", topLeft, topLeft, new Vector2(-8f, 8f), new Vector2(38f, 38f));
            HudStyle.Shape(key, "Felt", HudShape.Kind.RoundRect, pal.Felt, 10f, HudStyle.FeltTexture);
            HudStyle.AddText(HudStyle.Filled(key, "Text"), (index + 1).ToString(), 22, Color.white, TextAnchor.MiddleCenter);

            // 最前面の幕。カードの下辺から伸び、残り時間に合わせて縮む。
            var shade = HudStyle.Shape(card.Root, "Cooldown", HudShape.Kind.RoundRect, Shade, 10f);
            card.ShadeRect = shade.rectTransform;
            card.ShadeRect.gameObject.SetActive(false);
            card.Cooldown = HudStyle.Label(card.Root, "CooldownText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(CardWidth, 60f),
                "", 44, Color.white, TextAnchor.MiddleCenter, 5f, pal.Outline);
            card.Cooldown.gameObject.SetActive(false);
            return card;
        }

        // スキルの装備は試合開始の前後で変わるので、毎フレーム見比べて変わったカードだけ更新する。
        private void Update()
        {
            if (runtime == null) return;
            int selected = runtime.SelectedIndex;
            float blend = 1f - Mathf.Exp(-HoverSharpness * Time.unscaledDeltaTime);
            for (int i = 0; i < cards.Length; i++)
            {
                var skill = runtime.EquippedSkills.Count > i ? runtime.EquippedSkills[i] : null;
                RefreshContent(cards[i], skill, i);
                UpdateHover(cards[i], i == selected, blend);
                bool cooling = UpdateCooldown(cards[i], skill, i);
                UpdateAffordable(cards[i], skill, cooling);
                UpdateReadyPop(cards[i], cooling);
            }
            // 選択中のカードは隣より手前に描く。
            if (selected != lastSelected && selected >= 0 && selected < cards.Length)
            {
                cards[selected].Root.SetAsLastSibling();
                lastSelected = selected;
            }
        }

        private void RefreshContent(Card card, SkillData skill, int index)
        {
            int level = skill != null ? runtime.GetSkillLevel(index) : 0;
            if (card.Shown == skill && card.ShownLevel == level) return;
            card.Shown = skill;
            card.ShownLevel = level;
            card.ShownShort = false;

            bool hasIcon = skill != null && skill.icon != null;
            card.Icon.enabled = hasIcon;
            card.Icon.sprite = hasIcon ? skill.icon : null;
            card.Icon.color = Color.white;
            card.IconFallback.enabled = skill != null && !hasIcon;
            if (skill == null)
            {
                card.Name.text = "-";
                card.Cost.text = "";
                card.CostPill.enabled = false;
                card.Level.text = "";
                return;
            }
            card.IconFallback.color = skill.EffectiveIconColor();
            card.Name.text = skill.DisplayName;
            // 物理系のスキルはMPを使わないので札を出さない（クールタイムだけで使える）。
            bool usesMp = skill.mpCost > 0;
            card.Cost.text = usesMp ? "MP " + skill.mpCost : "";
            card.CostPill.enabled = usesMp;
            card.CostPill.color = HudStyle.MpPill;
            card.Cost.color = HudStyle.MpPillText;
            card.Level.text = level >= skill.maxSkillLevel ? "MAX" : "Lv" + level;
        }

        private static void UpdateHover(Card card, bool selected, float blend)
        {
            card.Hover = Mathf.Lerp(card.Hover, selected ? 1f : 0f, blend);
            card.Root.anchoredPosition = new Vector2(card.BaseX, SelectedLift * card.Hover);
            card.Root.localScale = Vector3.one * Mathf.Lerp(1f, SelectedScale, card.Hover);
            card.Root.localEulerAngles = new Vector3(0f, 0f, card.RestTilt * (1f - card.Hover));
            var gold = HudStyle.Gold;
            gold.a = card.Hover;
            card.Gold.color = gold;
        }

        private bool UpdateCooldown(Card card, SkillData skill, int index)
        {
            float remaining = skill != null ? runtime.GetCooldownRemaining(index) : 0f;
            bool cooling = remaining > 0f;
            if (card.ShadeRect.gameObject.activeSelf != cooling) card.ShadeRect.gameObject.SetActive(cooling);
            if (card.Cooldown.gameObject.activeSelf != cooling) card.Cooldown.gameObject.SetActive(cooling);
            if (!cooling) return false;
            float ratio = skill.cooldown > 0f ? Mathf.Clamp01(remaining / skill.cooldown) : 0f;
            card.ShadeRect.anchorMax = new Vector2(1f, ratio);
            card.Cooldown.text = remaining >= 10f ? Mathf.CeilToInt(remaining).ToString() : remaining.ToString("F1");
            return true;
        }

        private const float ReadyPopSeconds = 0.4f;

        // クールタイムが明けたら、札がぽんと跳ねて金の縁が光る。
        private static void UpdateReadyPop(Card card, bool cooling)
        {
            if (card.WasCooling && !cooling) card.Ready = 1f;
            card.WasCooling = cooling;
            if (card.Ready <= 0f) return;
            card.Ready = Mathf.Max(0f, card.Ready - Time.unscaledDeltaTime / ReadyPopSeconds);
            float bump = Mathf.Sin((1f - card.Ready) * Mathf.PI);
            card.Root.localScale *= 1f + 0.18f * bump;
            card.Root.anchoredPosition += new Vector2(0f, 14f * bump);
            var gold = card.Gold.color;
            gold.a = Mathf.Max(gold.a, card.Ready);
            card.Gold.color = gold;
        }

        /// MPが足りないカード。クールダウン中は幕で十分なので変えない。
        private void UpdateAffordable(Card card, SkillData skill, bool cooling)
        {
            bool shortOfMp = skill != null && !cooling && mana != null && mana.CurrentMP < skill.mpCost;
            if (shortOfMp == card.ShownShort) return;
            card.ShownShort = shortOfMp;
            card.Icon.color = shortOfMp ? IconOff : Color.white;
            card.CostPill.color = shortOfMp ? HudStyle.Danger : HudStyle.MpPill;
            card.Cost.color = shortOfMp ? Color.white : HudStyle.MpPillText;
        }
    }
}
