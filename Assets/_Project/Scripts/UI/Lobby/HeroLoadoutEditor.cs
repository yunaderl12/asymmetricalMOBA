using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Combat;
using Game.Hero;
using Game.Skill;

namespace Game.UI
{
    /// 勇者の編成パネル。紙のシートに、ジョブ・スキル5枠・パッシブ・スキルの一覧・詳細を並べる。
    /// 勇者ロビーとマッチロビーの両方で使う。枠を押して選び、一覧のスキルを押すとその枠に入る（もう入っているスキルなら入れ替え）。
    /// 1〜4枠はジョブのスキルか共通スキル、5枠目はどのスキルでも入る。入らないスキルは薄く出す。
    public class HeroLoadoutEditor : MonoBehaviour
    {
        private const float PanelX = 640f, PanelY = 40f, PanelW = 1240f, PanelH = 1000f;
        private const float ChipHeight = 42f, ChipRowStep = 50f, ChipLeft = 150f, ChipRight = 840f;

        public bool IsOpen { get; private set; }
        public Action OnLoadoutChanged;
        public Action<bool> OnOpenChanged;

        private HudStyle.Palette pal;
        private HeroSkillLoadout loadout;
        private RectTransform panel;
        private Image backdrop;
        private RectTransform classRow;
        private readonly LobbySkillCard[] slots = new LobbySkillCard[5];
        private RectTransform passiveRow;
        private RectTransform chipArea;
        private Text chipHeader;
        private Text noteText;
        private float noteResetAt = -1f;
        private int selectedSlot = HeroSkillLoadout.FreeSlot;
        private float panelOffset = PanelW + 120f;

        // 詳細
        private Image detailIcon;
        private Text detailGlyph;
        private Text detailName;
        private Text detailOwner;
        private Text detailStats;
        private Text detailEffect;
        private Text detailDesc;
        private object detailShown;

        public static HeroLoadoutEditor Create(Transform stage)
        {
            var rt = LobbyStyle.At(stage, "LoadoutEditor", 0f, 0f, LobbyStyle.StageWidth, LobbyStyle.StageHeight);
            var editor = rt.gameObject.AddComponent<HeroLoadoutEditor>();
            editor.Build(rt);
            return editor;
        }

        private void Build(RectTransform rt)
        {
            pal = HudStyle.HeroPalette;

            // 外側を押すと閉じる。少し暗くする。
            var dim = HudStyle.Node(rt, "Backdrop");
            dim.anchorMin = new Vector2(-1f, -1f); dim.anchorMax = new Vector2(2f, 2f);
            dim.offsetMin = dim.offsetMax = Vector2.zero;
            backdrop = HudStyle.AddGraphic<Image>(dim);
            backdrop.color = new Color(0.08f, 0.04f, 0.15f, 0f);
            var close = dim.gameObject.AddComponent<Button>();
            close.transition = Selectable.Transition.None;
            close.targetGraphic = backdrop;
            backdrop.raycastTarget = true;
            close.onClick.AddListener(Hide);

            panel = LobbyStyle.At(rt, "Panel", PanelX, PanelY, PanelW, PanelH);
            var paper = LobbyStyle.PaperSheet(panel, pal, 8f, 2, 1, 8f);
            paper.raycastTarget = true; // パネルの中を押しても閉じない

            var header = LobbyStyle.At(panel, "Header", -16f, 30f, 220f, 58f);
            var ribbon = HudStyle.Shape(header, "Ribbon", HudShape.Kind.Ribbon, pal.Ribbon);
            ribbon.notch = 22f;
            HudStyle.AddText(HudStyle.Filled(header, "Text"), "編成", 30, Color.white, TextAnchor.MiddleCenter);

            LobbyStyle.Text(panel, "EscHint", PanelW - 260f, 34f, 160f, 40f, "Esc で閉じる", 20, LobbyStyle.SubInk, TextAnchor.MiddleRight);
            var closeRt = LobbyStyle.At(panel, "Close", PanelW - 86f, 26f, 58f, 58f);
            var closeBody = HudStyle.Felt(closeRt, pal, HudShape.Kind.Circle, 0f, 0f, 4f);
            HudStyle.AddText(HudStyle.Filled(closeRt, "X"), "×", 32, Color.white, TextAnchor.MiddleCenter);
            LobbyStyle.MakeButton(closeRt, closeBody, Hide);

            // ジョブ
            LobbyStyle.Text(panel, "ClassHeader", 60f, 110f, 300f, 36f, "ジョブ", 26, pal.Ink);
            classRow = LobbyStyle.At(panel, "Classes", 60f, 150f, 640f, 160f);

            // スキル5枠
            LobbyStyle.Text(panel, "SkillHeader", 60f, 324f, 300f, 36f, "スキル", 26, pal.Ink);
            for (int i = 0; i < slots.Length; i++)
            {
                int index = i;
                var cardRt = LobbyStyle.At(panel, "Slot_" + (i + 1), 60f + i * 130f, 374f, 116f, 144f);
                slots[i] = LobbySkillCard.Create(cardRt, i + 1, i == HeroSkillLoadout.FreeSlot, pal, i, false, () => OnSlotClicked(index));
            }
            var note = LobbyStyle.At(panel, "Note", 200f, 318f, 540f, 40f);
            LobbyStyle.Tilt(note, -2f);
            var noteBody = HudStyle.Shape(note, "Note", HudShape.Kind.RoundRect, LobbyStyle.Note, 2f, HudStyle.PaperTexture);
            HudStyle.DropShadow(note, HudShape.Kind.RoundRect, new Color(0.12f, 0.06f, 0.2f, 0.3f), 2f, 3f).rectTransform.SetAsFirstSibling();
            noteBody.raycastTarget = false;
            noteText = HudStyle.AddText(HudStyle.Filled(note, "Text"), NoteDefault, 17, pal.Ink, TextAnchor.MiddleCenter);

            // パッシブ
            LobbyStyle.Text(panel, "PassiveHeader", 760f, 324f, 300f, 36f, "パッシブ", 26, pal.Ink);
            LobbyStyle.Text(panel, "PassiveHint", 890f, 326f, 200f, 36f, "1つ選ぶ", 18, LobbyStyle.SubInk);
            passiveRow = LobbyStyle.At(panel, "Passives", 760f, 370f, 420f, 150f);

            LobbyStyle.DashedLine(panel, 60f, 536f, PanelW - 120f, LobbyStyle.Divider);

            // スキルの一覧
            chipHeader = LobbyStyle.Text(panel, "FreeHeader", 60f, 552f, 780f, 36f, "", 26, pal.Ink);
            chipArea = LobbyStyle.At(panel, "FreeSkills", 0f, 596f, ChipRight, PanelH - 610f);

            BuildDetail();

            gameObject.SetActive(false);
        }

        private void BuildDetail()
        {
            var card = LobbyStyle.At(panel, "Detail", 870f, 566f, 340f, 400f);
            LobbyStyle.Tilt(card, 1.2f);
            var body = HudStyle.Paper(card, pal, HudShape.Kind.RoundRect, 6f, 4, 5f);
            body.color = LobbyStyle.SoftPaper;
            LobbyStyle.Tape(card, 125f, -12f, 90f, 26f, LobbyStyle.TapePink, -3f);

            var iconRt = LobbyStyle.At(card, "Icon", 24f, 24f, 72f, 72f);
            detailIcon = HudStyle.AddGraphic<Image>(iconRt);
            detailIcon.preserveAspect = true;
            detailIcon.raycastTarget = false;
            detailGlyph = LobbyStyle.FeltToken(LobbyStyle.At(card, "Token", 24f, 24f, 72f, 72f), HudStyle.Town, pal.Stitch, "", 30, pal, 3f);
            detailName = LobbyStyle.Text(card, "Name", 110f, 26f, 220f, 42f, "", 30, pal.Ink);
            detailOwner = LobbyStyle.Text(card, "Owner", 110f, 68f, 220f, 26f, "", 18, HudStyle.Town);
            detailStats = LobbyStyle.Paragraph(card, "Stats", 22f, 110f, 310f, 72f, "", 17, pal.Ink);
            detailEffect = LobbyStyle.Paragraph(card, "Effect", 22f, 182f, 310f, 98f, "", 17, LobbyStyle.SubInk);
            detailDesc = LobbyStyle.Paragraph(card, "Desc", 22f, 284f, 296f, 108f, "", 18, pal.Ink);
            ShowDetail(null);
        }

        private const string NoteDefault = "1〜4枠はジョブか共通のスキル。5枠目はどれでも入る";

        private void ShowNote(string message)
        {
            noteText.text = message;
            noteText.color = pal.Ribbon;
            noteResetAt = Time.unscaledTime + 2.5f;
        }

        public void Bind(HeroSkillLoadout newLoadout)
        {
            loadout = newLoadout;
            Refresh();
        }

        private void Refresh()
        {
            if (loadout == null) return;
            BuildClasses();
            RefreshSlots();
            BuildPassives();
            BuildChips();
            if (detailShown == null) ShowDetail(loadout.Skills[selectedSlot]);
        }

        // ---- ジョブ ----

        private void BuildClasses()
        {
            LobbyStyle.Clear(classRow);
            var classes = loadout.Classes;
            if (classes == null) return;
            for (int i = 0; i < classes.Count; i++)
            {
                var c = classes[i];
                if (c == null) continue;
                int index = i;
                bool on = i == loadout.ClassIndex;
                var rt = LobbyStyle.At(classRow, "Cls_" + c.className, i * 140f, 0f, 120f, 160f);
                var face = LobbyStyle.At(rt, "Face", 5f, 0f, 110f, 110f);
                if (on) LobbyStyle.Ring(face, HudShape.Kind.Circle, 0f, 6f);
                LobbyStyle.Sticker(face, c.icon != null ? c.icon : c.portrait, c.avatarColor, pal);
                LobbyStyle.Text(rt, "Name", -10f, 118f, 140f, 32f, HudStyle.ClassLabel(c.className), 21, on ? pal.Ribbon : pal.Ink, TextAnchor.MiddleCenter);
                LobbyStyle.MakeHitButton(rt, () => OnClassClicked(index));
            }
        }

        private void OnClassClicked(int index)
        {
            if (loadout == null || index == loadout.ClassIndex) return;
            loadout.SelectClass(index);
            detailShown = null;
            Refresh();
            OnLoadoutChanged?.Invoke();
        }

        // ---- スキル枠 ----

        private void RefreshSlots()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].Set(loadout.Skills[i]);
                slots[i].SetHighlight(i == selectedSlot);
            }
        }

        private void OnSlotClicked(int index)
        {
            if (loadout == null) return;
            selectedSlot = index;
            ShowDetail(loadout.Skills[index]);
            RefreshSlots();
            BuildChips();
        }

        // ---- パッシブ ----

        private void BuildPassives()
        {
            LobbyStyle.Clear(passiveRow);
            var cls = loadout.CurrentClass;
            if (cls == null) return;
            if (cls.passives.Count == 0)
            {
                LobbyStyle.Text(passiveRow, "None", 0f, 30f, 400f, 40f, "このジョブにはまだありません", 20, LobbyStyle.SubInk);
                return;
            }
            for (int i = 0; i < cls.passives.Count; i++)
            {
                var passive = cls.passives[i];
                if (passive == null) continue;
                bool on = passive == loadout.Passive;
                var rt = LobbyStyle.At(passiveRow, "Passive_" + i, i * 160f, 0f, 130f, 150f);
                var token = LobbyStyle.At(rt, "Token", 13f, 4f, 104f, 104f);
                if (on) LobbyStyle.Ring(token, HudShape.Kind.Circle, 0f, 6f);
                LobbyStyle.FeltToken(token, on ? HudStyle.Town : pal.Felt, pal.Stitch, Initial(passive.passiveName), 38, pal);
                LobbyStyle.Text(rt, "Name", -14f, 114f, 158f, 30f, passive.passiveName, 18, pal.Ink, TextAnchor.MiddleCenter);
                LobbyStyle.MakeHitButton(rt, () => OnPassiveClicked(passive));
            }
        }

        private void OnPassiveClicked(PassiveData passive)
        {
            ShowDetail(passive);
            if (loadout == null || loadout.Passive == passive) return;
            loadout.SetPassive(passive);
            BuildPassives();
            OnLoadoutChanged?.Invoke();
        }

        // ---- スキルの一覧 ----

        private void BuildChips()
        {
            LobbyStyle.Clear(chipArea);
            bool free = selectedSlot == HeroSkillLoadout.FreeSlot;
            chipHeader.text = (selectedSlot + 1) + "枠目に入れるスキル" + (free ? "（どれでも）" : "（ジョブか共通）");
            var all = loadout.AllSkills();
            var current = loadout.CurrentClass;

            // 共通 → いまのジョブ → ほかのジョブ。
            var groups = new List<KeyValuePair<string, List<SkillData>>>();
            var common = all.FindAll(loadout.IsCommonSkill);
            if (common.Count > 0) groups.Add(new KeyValuePair<string, List<SkillData>>("共通", common));
            var order = new List<HeroClassData>();
            if (current != null) order.Add(current);
            foreach (var c in loadout.Classes) if (c != null && !order.Contains(c)) order.Add(c);
            foreach (var c in order)
            {
                var pool = c.ClassSkillPool();
                var mine = all.FindAll(s => pool.Contains(s) && !loadout.IsCommonSkill(s));
                if (mine.Count > 0) groups.Add(new KeyValuePair<string, List<SkillData>>(HudStyle.ClassLabel(c.className), mine));
            }

            float y = 0f;
            foreach (var g in groups)
            {
                LobbyStyle.Text(chipArea, "Owner", 60f, y, 90f, ChipHeight, g.Key, 18, LobbyStyle.SubInk);
                float x = ChipLeft;
                foreach (var s in g.Value)
                {
                    float w = ChipWidth(s);
                    if (x + w > ChipRight && x > ChipLeft) { x = ChipLeft; y += ChipRowStep; }
                    BuildChip(s, x, y, w, System.Array.IndexOf(loadout.Skills, s), loadout.CanEquip(selectedSlot, s));
                    x += w + 8f;
                }
                y += ChipRowStep;
            }
        }

        private static float ChipWidth(SkillData s)
        {
            return 6f + 32f + 5f + s.DisplayName.Length * 17f + 6f + ChipCost(s).Length * 8.5f + 12f + 8f;
        }

        private static string ChipCost(SkillData s)
        {
            return s.mpCost > 0 ? "MP " + s.mpCost : "CT " + s.cooldown.ToString("0.#") + "秒";
        }

        /// equippedSlot: このスキルが入っている枠（なければ -1）。usable: いま選んでいる枠に入れられるか。
        private void BuildChip(SkillData s, float x, float y, float w, int equippedSlot, bool usable)
        {
            var rt = LobbyStyle.At(chipArea, "Skill_" + s.skillName, x, y, w, ChipHeight);
            bool on = equippedSlot == selectedSlot;
            if (on) LobbyStyle.Ring(rt, HudShape.Kind.RoundRect, 12f, 4f);
            var chipPal = pal;
            chipPal.Felt = on ? pal.Ribbon : equippedSlot >= 0 ? HudStyle.Town : pal.Felt;
            var body = HudStyle.Felt(rt, chipPal, HudShape.Kind.RoundRect, 12f, 0f, 4f);

            var iconRt = LobbyStyle.At(rt, "Icon", 5f, 5f, 32f, 32f);
            HudStyle.Shape(iconRt, "Chip", HudShape.Kind.RoundRect, OwnerColor(s), 8f);
            if (s.icon != null)
            {
                var icon = HudStyle.AddGraphic<Image>(HudStyle.Filled(iconRt, "Art"));
                icon.sprite = s.icon;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
            }
            else
            {
                HudStyle.AddText(HudStyle.Filled(iconRt, "Glyph"), Initial(s.DisplayName), 18, Color.white, TextAnchor.MiddleCenter);
            }
            float nameW = s.DisplayName.Length * 17f + 2f;
            LobbyStyle.Text(rt, "Name", 42f, 0f, nameW, ChipHeight, s.DisplayName, 16, pal.Light);
            string cost = ChipCost(s);
            var pill = LobbyStyle.At(rt, "Cost", 42f + nameW + 4f, 10f, cost.Length * 8.5f + 12f, 22f);
            HudStyle.Shape(pill, "Pill", HudShape.Kind.RoundRect, s.mpCost > 0 ? HudStyle.MpPill : LobbyStyle.LockedPaper, 8f);
            HudStyle.AddText(HudStyle.Filled(pill, "Text"), cost, 13, s.mpCost > 0 ? HudStyle.MpPillText : HudStyle.Brown, TextAnchor.MiddleCenter);

            // 入っている枠の番号
            if (equippedSlot >= 0 && !on)
            {
                var badge = LobbyStyle.At(rt, "Slot", -10f, -10f, 26f, 26f);
                HudStyle.Shape(badge, "Dot", HudShape.Kind.Circle, pal.Outline);
                HudStyle.AddText(HudStyle.Filled(badge, "Num"), (equippedSlot + 1).ToString(), 16, Color.white, TextAnchor.MiddleCenter);
            }
            if (!usable) rt.gameObject.AddComponent<CanvasGroup>().alpha = 0.38f;

            LobbyStyle.MakeButton(rt, body, () => OnChipClicked(s));
        }

        private Color OwnerColor(SkillData s)
        {
            if (s.ownerClass == null) return HudStyle.Town;
            switch (s.ownerClass.className)
            {
                case "Mage": return HudStyle.Mp;
                case "Priest": return HudStyle.Hex(0xf5b829);
                case "Warrior": return HudStyle.Hp;
                default: return s.ownerClass.avatarColor;
            }
        }

        private void OnChipClicked(SkillData skill)
        {
            ShowDetail(skill);
            if (loadout == null || loadout.Skills[selectedSlot] == skill) return;
            if (!loadout.CanEquip(selectedSlot, skill))
            {
                ShowNote("ほかのジョブのスキルは5枠目にだけ入れられる");
                return;
            }
            if (!loadout.Place(selectedSlot, skill))
            {
                ShowNote("入れ替えると" + (selectedSlot + 1) + "枠目のスキルが1〜4枠に入らない");
                return;
            }
            RefreshSlots();
            BuildChips();
            OnLoadoutChanged?.Invoke();
        }

        // ---- 詳細 ----

        private void ShowDetail(object item)
        {
            detailShown = item;
            var skill = item as SkillData;
            var passive = item as PassiveData;
            detailIcon.sprite = skill != null ? skill.icon : null;
            detailIcon.enabled = detailIcon.sprite != null;
            detailGlyph.transform.parent.gameObject.SetActive(!detailIcon.enabled);
            if (skill != null)
            {
                detailGlyph.text = Initial(skill.DisplayName);
                detailName.text = skill.DisplayName;
                detailOwner.text = skill.ownerClass != null ? HudStyle.ClassLabel(skill.ownerClass.className) + " のスキル" : "共通スキル";
                detailStats.text = StatsLine(skill);
                detailEffect.text = EffectLine(skill);
                detailDesc.text = string.IsNullOrEmpty(skill.description) ? "（説明なし）" : skill.description;
            }
            else if (passive != null)
            {
                detailGlyph.text = Initial(passive.passiveName);
                detailName.text = passive.passiveName;
                detailOwner.text = "パッシブ";
                detailStats.text = passive.StatsLine();
                detailEffect.text = passive.howToUse;
                detailDesc.text = passive.description;
            }
            else
            {
                detailGlyph.text = "?";
                detailName.text = "";
                detailOwner.text = "";
                detailStats.text = "";
                detailEffect.text = "";
                detailDesc.text = "スキルやパッシブを押すと、ここに詳しく出る";
            }
        }

        /// 数値の項目を2つずつ1行に並べる（項目の途中で折り返さないように）。
        private static string StatsLine(SkillData s)
        {
            var items = new List<string> { "消費MP " + (s.mpCost > 0 ? s.mpCost.ToString() : "なし"), "CT " + Num(s.cooldown) + "秒" };
            if (s.range > 0f) items.Add("射程 " + Num(s.range) + "m");
            if (s.areaRadius > 0f) items.Add("範囲 " + Num(s.areaRadius) + "m");
            if (s.duration > 0f) items.Add("効果時間 " + Num(s.duration) + "秒" + (s.durationPerLevel > 0f ? "（Lvごと+" + Num(s.durationPerLevel) + "）" : ""));
            if (s.castTime > 0f) items.Add("溜め " + Num(s.castTime) + "秒");
            string text = "";
            for (int i = 0; i < items.Count; i++) text += (i == 0 ? "" : i % 2 == 0 ? "\n" : "　") + items[i];
            return text;
        }

        private static string EffectLine(SkillData s)
        {
            string line = "効果: " + EffectName(s);
            if (s.effectType == SkillEffectType.Damage || s.effectType == SkillEffectType.Heal)
            {
                line += " " + Num(s.effectValue);
                if (s.levelGrowthRate > 0f) line += "（Lvごと+" + Mathf.RoundToInt(s.levelGrowthRate * 100f) + "%）";
            }
            line += "\n対象: " + TargetName(s.targetType);
            if (s.statusEffect != StatusEffectType.None)
            {
                line += "　状態異常: " + StatusName(s.statusEffect);
                if (s.statusDuration > 0f) line += " " + Num(s.statusDuration) + "秒";
                if (s.statusEffect == StatusEffectType.Slow && s.statusPower > 0f) line += "（移動速度 -" + Mathf.RoundToInt(s.statusPower * 100f) + "%）";
                else if (s.statusEffect == StatusEffectType.DamageOverTime && s.statusPower > 0f) line += "（1回 " + Num(s.statusPower) + "）";
            }
            return line;
        }

        private static string EffectName(SkillData s)
        {
            switch (s.effectType)
            {
                case SkillEffectType.Damage:
                    return s.damageType == SkillDamageType.Magical ? "魔法ダメージ" : s.damageType == SkillDamageType.Physical ? "物理ダメージ" : "ダメージ";
                case SkillEffectType.Heal: return "回復";
                case SkillEffectType.Buff: return "強化";
                case SkillEffectType.Debuff: return "弱体";
                default: return "-";
            }
        }

        private static string TargetName(SkillTargetType t)
        {
            switch (t)
            {
                case SkillTargetType.Self: return "自分";
                case SkillTargetType.Single: return "単体";
                case SkillTargetType.Area: return "範囲";
                default: return "-";
            }
        }

        private static string StatusName(StatusEffectType t)
        {
            switch (t)
            {
                case StatusEffectType.Stun: return "スタン";
                case StatusEffectType.Slow: return "スロー";
                case StatusEffectType.DamageOverTime: return "継続ダメージ";
                case StatusEffectType.Silence: return "沈黙";
                default: return "-";
            }
        }

        private static string Num(float v) => v.ToString("0.#");

        public static string Initial(string name) => string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1);

        // ---- 開け閉め ----

        public void Show()
        {
            if (IsOpen) return;
            IsOpen = true;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            selectedSlot = HeroSkillLoadout.FreeSlot;
            detailShown = null;
            Refresh();
            OnOpenChanged?.Invoke(true);
        }

        public void Hide()
        {
            if (!IsOpen) return;
            IsOpen = false;
            OnOpenChanged?.Invoke(false);
        }

        public void Toggle()
        {
            if (IsOpen) Hide(); else Show();
        }

        private void Update()
        {
            if (noteResetAt > 0f && Time.unscaledTime >= noteResetAt)
            {
                noteResetAt = -1f;
                noteText.text = NoteDefault;
                noteText.color = pal.Ink;
            }
            float target = IsOpen ? 0f : PanelW + 120f;
            panelOffset = Mathf.Lerp(panelOffset, target, 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime));
            if (Mathf.Abs(panelOffset - target) < 0.5f) panelOffset = target;
            panel.anchoredPosition = new Vector2(PanelX + panelOffset, -PanelY);
            float dimAlpha = 0.35f * (1f - panelOffset / (PanelW + 120f));
            backdrop.color = new Color(backdrop.color.r, backdrop.color.g, backdrop.color.b, dimAlpha);
            if (!IsOpen && panelOffset >= PanelW + 120f) gameObject.SetActive(false);
        }
    }
}
