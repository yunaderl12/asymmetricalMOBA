using UnityEngine;
using UnityEngine.UI;
using Game.Skill;

namespace Game.UI
{
    /// 試合中のスキル表示。縦長のカードを並べ、左上にレベル、中央にイラスト、下部に名前を出す。
    /// 選択中のカードは浮き上がる。クールダウン中はグレーの幕が下がっていく。コードだけで組み立てる。
    public class SkillCardRow : MonoBehaviour
    {
        private const float CardWidth = 100f;
        private const float CardHeight = 150f;
        private const float CardGap = 10f;
        private const float Margin = 24f;
        private const float SelectedLift = 16f;
        private const float SelectedScale = 1.12f;
        private const float HoverSharpness = 16f;
        private const float BorderSize = 3f;
        private const float GlowSize = 9f;

        private static readonly Color BodyTop = new Color(0.2f, 0.22f, 0.3f, 0.96f);
        private static readonly Color BodyBottom = new Color(0.07f, 0.08f, 0.12f, 0.96f);
        private static readonly Color BorderNormal = new Color(0.32f, 0.34f, 0.42f, 1f);
        private static readonly Color BorderSelected = new Color(1f, 0.82f, 0.3f, 1f);
        private static readonly Color GlowSelected = new Color(1f, 0.75f, 0.2f, 0.4f);
        private static readonly Color Shade = new Color(0.16f, 0.16f, 0.18f, 0.74f);
        private static readonly Color ChipColor = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color NameNormal = new Color(0.85f, 0.87f, 0.92f, 1f);
        private static readonly Color CostColor = new Color(0.55f, 0.8f, 1f, 1f);

        private class Card
        {
            public RectTransform Root;
            public RoundedCardGraphic Glow, Border;
            public DiamondGraphic Icon;
            public Text Level, Name, Cost, Cooldown;
            public RectTransform ShadeRect;
            public SkillData Shown;
            public int ShownLevel = -1;
            public float Hover;
            public float BaseX;
        }

        private SkillRuntime runtime;
        private Card[] cards;
        private int lastSelected = -1;

        public static SkillCardRow Create(Transform canvas, Font font, SkillRuntime runtime)
        {
            var go = new GameObject("SkillCards", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
            int count = SkillRuntime.SLOT_COUNT;
            rt.sizeDelta = new Vector2(count * CardWidth + (count - 1) * CardGap, CardHeight);
            rt.anchoredPosition = new Vector2(-Margin, Margin);

            var row = go.AddComponent<SkillCardRow>();
            row.runtime = runtime;
            row.cards = new Card[count];
            for (int i = 0; i < count; i++) row.cards[i] = row.BuildCard(i, font);
            return row;
        }

        private Card BuildCard(int index, Font font)
        {
            var card = new Card();
            var root = new GameObject("Card_" + index, typeof(RectTransform));
            root.transform.SetParent(transform, false);
            card.Root = (RectTransform)root.transform;
            card.Root.anchorMin = card.Root.anchorMax = Vector2.zero;
            // 下辺の中央を軸にして、選択時に上へ向かって大きくする。
            card.Root.pivot = new Vector2(0.5f, 0f);
            card.Root.sizeDelta = new Vector2(CardWidth, CardHeight);
            card.BaseX = index * (CardWidth + CardGap) + CardWidth * 0.5f;
            card.Root.anchoredPosition = new Vector2(card.BaseX, 0f);

            card.Glow = MakeCard(card.Root, "Glow", GlowSize, 14f + GlowSize);
            card.Border = MakeCard(card.Root, "Border", BorderSize, 12f);
            var body = MakeCard(card.Root, "Body", 0f, 10f);
            body.color = Color.white;
            body.topTint = BodyTop;
            body.bottomTint = BodyBottom;

            var icon = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer));
            icon.transform.SetParent(card.Root, false);
            var iconRt = (RectTransform)icon.transform;
            iconRt.anchorMin = iconRt.anchorMax = iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.sizeDelta = new Vector2(78f, 82f);
            iconRt.anchoredPosition = new Vector2(0f, 8f);
            card.Icon = icon.AddComponent<DiamondGraphic>();
            card.Icon.raycastTarget = false;

            var strip = MakeCard(card.Root, "NameStrip", 0f, 10f);
            strip.color = ChipColor;
            var stripRt = strip.rectTransform;
            stripRt.anchorMin = Vector2.zero;
            stripRt.anchorMax = new Vector2(1f, 0f);
            stripRt.offsetMin = Vector2.zero;
            stripRt.offsetMax = new Vector2(0f, 36f);
            card.Name = MakeText(stripRt, font, "Name", 15, FontStyle.Bold, TextAnchor.MiddleCenter, NameNormal);
            card.Name.rectTransform.offsetMin = new Vector2(4f, 12f);
            card.Name.rectTransform.offsetMax = new Vector2(-4f, 0f);
            card.Name.resizeTextForBestFit = true;
            card.Name.resizeTextMinSize = 10;
            card.Name.resizeTextMaxSize = 15;
            card.Cost = MakeText(stripRt, font, "Cost", 11, FontStyle.Normal, TextAnchor.LowerCenter, CostColor);
            card.Cost.rectTransform.offsetMin = new Vector2(4f, 1f);
            card.Cost.rectTransform.offsetMax = new Vector2(-4f, -22f);

            var chip = MakeCard(card.Root, "LevelChip", 0f, 6f);
            chip.color = ChipColor;
            var chipRt = chip.rectTransform;
            chipRt.anchorMin = chipRt.anchorMax = chipRt.pivot = new Vector2(0f, 1f);
            chipRt.sizeDelta = new Vector2(46f, 20f);
            chipRt.anchoredPosition = new Vector2(5f, -5f);
            card.Level = MakeText(chipRt, font, "Level", 12, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.45f, 1f));

            // 最前面の幕。カードの下辺から伸び、残り時間に合わせて縮む。
            var shade = MakeCard(card.Root, "Cooldown", 0f, 10f);
            shade.color = Shade;
            card.ShadeRect = shade.rectTransform;
            card.ShadeRect.gameObject.SetActive(false);
            card.Cooldown = MakeText(card.Root, font, "CooldownText", 30, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            card.Cooldown.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            card.Cooldown.gameObject.SetActive(false);
            return card;
        }

        private static RoundedCardGraphic MakeCard(RectTransform parent, string name, float outset, float radius)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-outset, -outset);
            rt.offsetMax = new Vector2(outset, outset);
            var g = go.AddComponent<RoundedCardGraphic>();
            g.cornerRadius = radius;
            g.raycastTarget = false;
            return g;
        }

        private static Text MakeText(RectTransform parent, Font font, string name, int size, FontStyle style, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = anchor;
            t.color = color;
            t.raycastTarget = false;
            return t;
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
                UpdateCooldown(cards[i], skill, i);
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

            card.Icon.enabled = skill != null;
            SkillIconView.Set(card.Icon.rectTransform, skill);
            if (skill == null)
            {
                card.Name.text = "-";
                card.Cost.text = "";
                card.Level.text = "";
                return;
            }
            card.Icon.color = skill.EffectiveIconColor();
            card.Name.text = skill.skillName;
            card.Cost.text = "SP " + skill.spCost;
            card.Level.text = level >= skill.maxSkillLevel ? "MAX" : "Lv " + level;
        }

        private static void UpdateHover(Card card, bool selected, float blend)
        {
            card.Hover = Mathf.Lerp(card.Hover, selected ? 1f : 0f, blend);
            card.Root.anchoredPosition = new Vector2(card.BaseX, SelectedLift * card.Hover);
            card.Root.localScale = Vector3.one * Mathf.Lerp(1f, SelectedScale, card.Hover);
            card.Border.color = Color.Lerp(BorderNormal, BorderSelected, card.Hover);
            var glow = GlowSelected;
            glow.a *= card.Hover;
            card.Glow.color = glow;
        }

        private void UpdateCooldown(Card card, SkillData skill, int index)
        {
            float remaining = skill != null ? runtime.GetCooldownRemaining(index) : 0f;
            bool cooling = remaining > 0f;
            if (card.ShadeRect.gameObject.activeSelf != cooling) card.ShadeRect.gameObject.SetActive(cooling);
            if (card.Cooldown.gameObject.activeSelf != cooling) card.Cooldown.gameObject.SetActive(cooling);
            if (!cooling) return;
            float ratio = skill.cooldown > 0f ? Mathf.Clamp01(remaining / skill.cooldown) : 0f;
            card.ShadeRect.anchorMax = new Vector2(1f, ratio);
            card.Cooldown.text = remaining.ToString("F1");
        }
    }
}
