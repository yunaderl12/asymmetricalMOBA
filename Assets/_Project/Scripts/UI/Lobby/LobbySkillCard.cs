using UnityEngine;
using UnityEngine.UI;
using Game.Skill;

namespace Game.UI
{
    /// ロビーのスキルの札。紙のカードに番号の布、絵、名前、消費MPかCTを載せる。5枠目（自由枠）は金色のふちで目立たせる。
    public class LobbySkillCard
    {
        public RectTransform Root { get; private set; }
        public Button Button { get; private set; }

        private Image icon;
        private Text glyph;
        private Text nameText;
        private Text subText;
        private Text tagText;
        private HudShape paper;
        private HudShape ring;
        private bool free;
        private Color selectColor;

        /// rt の大きさでカードを作る。tag を出すときはカードの下に1行足す（rt の外）。
        public static LobbySkillCard Create(RectTransform rt, int key, bool free, HudStyle.Palette p, int seed, bool showTag, System.Action onClick)
        {
            var card = new LobbySkillCard { Root = rt, free = free, selectColor = p.Ribbon };
            float w = rt.sizeDelta.x, h = rt.sizeDelta.y;
            float k = w / 140f;

            card.ring = LobbyStyle.Ring(rt, HudShape.Kind.RoundRect, 12f * k, 4f);
            card.ring.enabled = free;
            card.paper = HudStyle.Paper(rt, p, HudShape.Kind.RoundRect, 12f * k, seed, 5f);
            card.paper.color = free ? LobbyStyle.SoftPaper : LobbyStyle.LockedPaper;
            LobbyStyle.Tape(rt, w * 0.5f - 22f * k, -10f * k, 44f * k, 18f * k, free ? LobbyStyle.TapeYellow : LobbyStyle.Tapes[(key + 3) % 4], -4f);

            var badge = LobbyStyle.At(rt, "Key", -9f * k, -9f * k, 40f * k, 40f * k);
            var badgePal = p;
            badgePal.Felt = free ? p.Ribbon : p.Felt;
            HudStyle.Felt(badge, badgePal, HudShape.Kind.RoundRect, 10f * k, 0f, 3f);
            HudStyle.AddText(HudStyle.Filled(badge, "Num"), key.ToString(), Mathf.RoundToInt(22f * k), Color.white, TextAnchor.MiddleCenter);

            float iconSize = 92f * k;
            var iconRt = LobbyStyle.At(rt, "Icon", (w - iconSize) * 0.5f, 14f * k, iconSize, iconSize);
            card.icon = HudStyle.AddGraphic<Image>(iconRt);
            card.icon.preserveAspect = true;
            card.icon.raycastTarget = false;
            card.glyph = HudStyle.AddText(HudStyle.Filled(iconRt, "Glyph"), "", Mathf.RoundToInt(44f * k), p.Ink, TextAnchor.MiddleCenter);

            float nameY = 14f * k + iconSize + 2f * k;
            card.nameText = LobbyStyle.Text(rt, "Name", 0f, nameY, w, 30f * k, "", Mathf.RoundToInt(23f * k), p.Ink, TextAnchor.MiddleCenter);
            if (h - nameY > 54f * k)
                card.subText = LobbyStyle.Text(rt, "Sub", 0f, nameY + 28f * k, w, 24f * k, "", Mathf.RoundToInt(17f * k), LobbyStyle.SubInk, TextAnchor.MiddleCenter);
            if (showTag)
                card.tagText = LobbyStyle.Text(rt, "Tag", -20f, h + 6f, w + 40f, 24f, free ? "自由枠" : "ジョブ・共通", 19,
                    free ? p.Ribbon : LobbyStyle.SubInk, TextAnchor.MiddleCenter);

            LobbyStyle.Tilt(rt, free ? 0f : (key % 2 == 0 ? 1.4f : -1.4f));
            if (onClick != null) card.Button = LobbyStyle.MakeButton(rt, card.paper, onClick);
            return card;
        }

        public void Set(SkillData skill)
        {
            icon.sprite = skill != null ? skill.icon : null;
            icon.enabled = icon.sprite != null;
            glyph.text = skill != null && icon.sprite == null && !string.IsNullOrEmpty(skill.DisplayName) ? skill.DisplayName.Substring(0, 1) : "";
            nameText.text = skill != null ? skill.DisplayName : "-";
            if (subText != null) subText.text = skill != null ? CostLine(skill) : "";
        }

        /// 選んでいる印（赤いふち）。自由枠はふだん金色のふち。
        public void SetHighlight(bool on)
        {
            ring.enabled = free || on;
            ring.color = on ? selectColor : HudStyle.Gold;
        }

        /// 「MP 10」。MPを使わないスキルは「CT 15秒」。
        public static string CostLine(SkillData s)
        {
            return s.mpCost > 0 ? "MP " + s.mpCost : "CT " + s.cooldown.ToString("0.#") + "秒";
        }
    }
}
