using UnityEngine;
using UnityEngine.UI;
using Game.Hero;

namespace Game.UI
{
    /// 「いまの編成」の紙。スキル5枚とパッシブを見せ、押すと編成パネルを開く。
    /// 大きい形（勇者ロビー）と、横長の小さい形（マッチロビー）がある。
    public class LoadoutSummary
    {
        private readonly LobbySkillCard[] cards = new LobbySkillCard[5];
        private Text passiveGlyph;
        private HudShape passiveFelt;
        private Text passiveName;
        private Text passiveDesc;

        /// 勇者ロビーの大きい形。
        public static LoadoutSummary Create(Transform stage, float x, float y, float w, float h, HudStyle.Palette p, System.Action onClick)
        {
            var s = new LoadoutSummary();
            var rt = LobbyStyle.At(stage, "LoadoutSummary", x, y, w, h);
            LobbyStyle.Tilt(rt, 0.6f);
            var paper = LobbyStyle.PaperSheet(rt, p, 6f, 2, 0, 7f);
            LobbyStyle.Text(rt, "Header", 36f, 26f, 400f, 48f, "いまの編成", 34, p.Ink);
            LobbyStyle.Text(rt, "Hint", w - 436f, 30f, 400f, 40f, "押すと編成を変えられる", 20, LobbyStyle.SubInk, TextAnchor.MiddleRight);

            for (int i = 0; i < 5; i++)
            {
                var cardRt = LobbyStyle.At(rt, "Card_" + (i + 1), 36f + i * 156f, 96f, 140f, 176f);
                s.cards[i] = LobbySkillCard.Create(cardRt, i + 1, i == HeroSkillLoadout.FreeSlot, p, i, true, null);
            }
            LobbyStyle.DashedLine(rt, 36f, 330f, w - 72f, LobbyStyle.Divider);

            var token = LobbyStyle.At(rt, "Passive", 36f, 356f, 92f, 92f);
            s.passiveGlyph = LobbyStyle.FeltToken(token, HudStyle.Town, p.Stitch, "", 34, p);
            s.passiveFelt = token.Find("Felt").GetComponent<HudShape>();
            LobbyStyle.Text(rt, "PassiveLabel", 150f, 352f, 300f, 28f, "パッシブ", 19, LobbyStyle.SubInk);
            s.passiveName = LobbyStyle.Text(rt, "PassiveName", 150f, 380f, 600f, 38f, "", 28, p.Ink);
            s.passiveDesc = LobbyStyle.Paragraph(rt, "PassiveDesc", 150f, 422f, w - 190f, h - 432f, "", 20, p.Ink);

            LobbyStyle.MakeButton(rt, paper, onClick).GetComponent<LobbyPress>().hoverScale = 1.015f;
            return s;
        }

        /// マッチロビーの横長の小さい形。右端に「編成を変える」ボタン。
        public static LoadoutSummary CreateCompact(Transform stage, float x, float y, float w, float h, HudStyle.Palette p, System.Action onClick)
        {
            var s = new LoadoutSummary();
            var rt = LobbyStyle.At(stage, "LoadoutSummary", x, y, w, h);
            LobbyStyle.PaperSheet(rt, p, 6f, 0, 2, 6f);
            for (int i = 0; i < 5; i++)
            {
                var cardRt = LobbyStyle.At(rt, "Card_" + (i + 1), 22f + i * 108f, 24f, 96f, 128f);
                s.cards[i] = LobbySkillCard.Create(cardRt, i + 1, i == HeroSkillLoadout.FreeSlot, p, i, false, null);
            }
            var token = LobbyStyle.At(rt, "Passive", 584f, 52f, 76f, 76f);
            s.passiveGlyph = LobbyStyle.FeltToken(token, HudStyle.Town, p.Stitch, "", 30, p);
            s.passiveFelt = token.Find("Felt").GetComponent<HudShape>();

            var button = LobbyStyle.At(rt, "EditLoadout", w - 114f, 44f, 92f, 92f);
            var body = LobbyStyle.FeltPlate(button, p, 16f, 5f, 4f);
            HudStyle.AddText(HudStyle.Filled(button, "Label"), "編成を\n変える", 19, p.Light, TextAnchor.MiddleCenter).lineSpacing = 1.1f;
            LobbyStyle.MakeButton(button, body, onClick);
            return s;
        }

        public void Show(HeroSkillLoadout loadout)
        {
            for (int i = 0; i < cards.Length; i++) cards[i].Set(loadout.Skills[i]);
            var passive = loadout.Passive;
            passiveGlyph.text = passive != null ? HeroLoadoutEditor.Initial(passive.passiveName) : "-";
            passiveFelt.color = passive != null ? HudStyle.Town : HudStyle.DownFelt;
            if (passiveName != null) passiveName.text = passive != null ? passive.passiveName : "なし";
            if (passiveDesc != null) passiveDesc.text = passive != null ? passive.description : "このジョブにはまだパッシブがない";
        }
    }
}
