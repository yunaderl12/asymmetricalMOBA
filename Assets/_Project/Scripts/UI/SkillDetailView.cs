using UnityEngine;
using UnityEngine.UI;
using Game.Combat;
using Game.Skill;

namespace Game.UI
{
    /// <summary>
    /// Fixed box in the hero lobby settings panel that shows the clicked skill's
    /// cost, cooldown, range, effect and description. Built procedurally; Show(null) shows the hint.
    /// </summary>
    public class SkillDetailView : MonoBehaviour
    {
        private DiamondGraphic icon;
        private Text nameText;
        private Text ownerText;
        private Text statsText;
        private Text effectText;
        private Text descText;

        public static SkillDetailView Create(RectTransform parent, Font font)
        {
            var go = new GameObject("SkillDetail", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = new Color(0.12f, 0.12f, 0.16f, 0.9f);
            img.raycastTarget = false;

            var view = go.AddComponent<SkillDetailView>();
            view.Build(font);
            view.Show(null);
            return view;
        }

        private void Build(Font font)
        {
            var iGO = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer));
            iGO.transform.SetParent(transform, false);
            var iRT = (RectTransform)iGO.transform;
            iRT.anchorMin = new Vector2(0f, 1f); iRT.anchorMax = new Vector2(0f, 1f); iRT.pivot = new Vector2(0f, 1f);
            iRT.sizeDelta = new Vector2(40f, 40f); iRT.anchoredPosition = new Vector2(16f, -14f);
            icon = iGO.AddComponent<DiamondGraphic>();
            icon.raycastTarget = false;

            nameText = MakeText(font, "Name", 22, FontStyle.Bold, new Vector2(68f, -12f), new Vector2(-84f, 30f));
            nameText.color = new Color(1f, 0.9f, 0.4f, 1f);
            ownerText = MakeText(font, "Owner", 14, FontStyle.Normal, new Vector2(68f, -12f), new Vector2(-84f, 30f));
            ownerText.alignment = TextAnchor.MiddleRight;
            ownerText.color = new Color(0.75f, 0.75f, 0.8f, 1f);
            statsText = MakeText(font, "Stats", 16, FontStyle.Bold, new Vector2(68f, -46f), new Vector2(-84f, 24f));
            statsText.color = new Color(0.55f, 0.8f, 1f, 1f);
            effectText = MakeText(font, "Effect", 15, FontStyle.Normal, new Vector2(68f, -74f), new Vector2(-84f, 24f));
            descText = MakeText(font, "Desc", 14, FontStyle.Normal, new Vector2(68f, -104f), new Vector2(-84f, 48f));
            descText.alignment = TextAnchor.UpperLeft;
            descText.horizontalOverflow = HorizontalWrapMode.Wrap;
            descText.color = new Color(0.9f, 0.9f, 0.9f, 1f);
        }

        // Top-left anchored, stretched to the box width minus the given width inset.
        private Text MakeText(Font font, string name, int size, FontStyle style, Vector2 pos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = sizeDelta; rt.anchoredPosition = pos;
            var t = go.GetComponent<Text>();
            t.font = font; t.fontSize = size; t.fontStyle = style; t.color = Color.white;
            t.alignment = TextAnchor.MiddleLeft;
            t.raycastTarget = false;
            return t;
        }

        public void Show(SkillData s)
        {
            if (s == null)
            {
                icon.enabled = false;
                nameText.text = "";
                ownerText.text = "";
                statsText.text = "";
                effectText.text = "";
                descText.text = "スキルをクリックすると、消費SP・クールタイムなどの詳細をここに表示します";
                return;
            }
            icon.enabled = true;
            icon.color = s.EffectiveIconColor();
            nameText.text = s.skillName;
            ownerText.text = s.ownerClass != null ? s.ownerClass.className + " 専用" : "共通スキル";
            statsText.text = $"消費SP {s.spCost}　　CT {Num(s.cooldown)}秒　　射程 {(s.range > 0f ? Num(s.range) + "m" : "-")}　　効果時間 {(s.duration > 0f ? Num(s.duration) + "秒" : "-")}";
            effectText.text = EffectLine(s);
            descText.text = string.IsNullOrEmpty(s.description) ? "(説明なし)" : s.description;
        }

        private static string EffectLine(SkillData s)
        {
            string line = "効果: " + EffectName(s);
            // Damage / Heal use effectValue as a flat amount; buffs use it as a multiplier, explained in the description.
            if (s.effectType == SkillEffectType.Damage || s.effectType == SkillEffectType.Heal)
            {
                line += " " + Num(s.effectValue);
                if (s.levelGrowthRate > 0f) line += $"（スキルLvごとに +{Mathf.RoundToInt(s.levelGrowthRate * 100f)}%）";
            }
            line += "　　対象: " + TargetName(s.targetType);
            if (s.statusEffect != StatusEffectType.None)
            {
                line += "　　状態異常: " + StatusName(s.statusEffect);
                if (s.statusDuration > 0f) line += " " + Num(s.statusDuration) + "秒";
                if (s.statusEffect == StatusEffectType.Slow && s.statusPower > 0f) line += $"（移動速度 -{Mathf.RoundToInt(s.statusPower * 100f)}%）";
                else if (s.statusEffect == StatusEffectType.DamageOverTime && s.statusPower > 0f) line += $"（1回 {Num(s.statusPower)}）";
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

        // 3 -> "3", 2.5 -> "2.5"
        private static string Num(float v)
        {
            return v.ToString("0.#");
        }
    }
}
