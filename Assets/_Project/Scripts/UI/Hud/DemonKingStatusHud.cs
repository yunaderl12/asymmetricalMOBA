using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Combat;
using Game.Core;
using Game.DemonKing;
using Game.Monster;

namespace Game.UI
{
    /// 魔王の行動中（TPS）の画面。勇者側と同じ作りを黒・暗い赤・骨色にしたもの。
    /// 左下に自分（Lv・HP・MP）、その上に城外のデバフの札と塔の一覧、右上にいる場所、中央下にその場の案内、右下にスキルの枠。
    /// 魔王スキルは内容が未定なので、探知以外は「未定」の枠を出す。
    public class DemonKingStatusHud : MonoBehaviour
    {
        private const float TowerRefreshInterval = 0.25f;

        private static readonly string[] SkillSlots = { "近接", "支援", "妨害" };

        private class TowerRow
        {
            public Spawner Spawner;
            public HealthComponent Health;
            public GameObject Root;
            public Text Name, Cost;
            public HudBar Bar;
            public int ShownHp = -1;
            public string ShownCost;
        }

        private readonly HudStyle.Palette pal = HudStyle.DemonPalette;

        private GameObject dk;
        private HealthComponent health;
        private ManaComponent mana;
        private LevelComponent level;
        private OutsideDebuff debuff;
        private DemonCastleRegen castle;
        private DemonKingViewSwitcher view;
        private DemonKingDetect detect;

        private Text levelText, hpText, mpText, areaText, contextText;
        private HudBar hpBar, mpBar;
        private GameObject contextRoot;
        private readonly List<TowerRow> towers = new List<TowerRow>();
        private RectTransform debuffRow;
        private Text speedChip, attackChip, drainChip;
        private RectTransform detectShade;
        private Text detectTime;

        private float nextTowerRefresh;
        private int shownHp = -1, shownHpMax = -1, shownMp = -1, shownMpMax = -1, shownLevel = -1, shownDebuff = -1;
        private string shownArea, shownContext;

        public static DemonKingStatusHud Create(GameObject demonKing)
        {
            var root = HudStyle.Filled(HudStyle.Canvas.transform, "DemonKingStatusHud");
            var hud = root.gameObject.AddComponent<DemonKingStatusHud>();
            hud.dk = demonKing;
            hud.health = demonKing.GetComponent<HealthComponent>();
            hud.mana = demonKing.GetComponent<ManaComponent>();
            hud.level = demonKing.GetComponent<LevelComponent>();
            hud.debuff = demonKing.GetComponent<OutsideDebuff>();
            hud.view = demonKing.GetComponent<DemonKingViewSwitcher>();
            hud.detect = demonKing.GetComponent<DemonKingDetect>();
            hud.castle = FindFirstObjectByType<DemonCastleRegen>();
            hud.Build(root);
            return hud;
        }

        // ---- 組み立て ----

        private void Build(RectTransform root)
        {
            var bottomLeft = Vector2.zero;
            var topLeft = new Vector2(0f, 1f);

            // 自分
            var panel = HudStyle.Place(root, "Self", bottomLeft, bottomLeft, new Vector2(118f, 40f), new Vector2(560f, 154f));
            HudStyle.Felt(panel, pal, HudShape.Kind.RoundRect, 20f, 7f, 6f);
            var content = HudStyle.Place(panel, "Content", topLeft, topLeft, new Vector2(94f, -18f), new Vector2(442f, 124f));
            HudStyle.Label(content, "Name", topLeft, topLeft, Vector2.zero, new Vector2(100f, 32f), "魔王", 25, pal.Light, TextAnchor.MiddleLeft);
            levelText = HudStyle.Label(content, "Level", topLeft, topLeft, new Vector2(100f, 0f), new Vector2(200f, 32f), "", 22, pal.Light, TextAnchor.MiddleLeft);
            hpBar = BuildStatRow(content, "Hp", "HP", -44f, 24f, HudStyle.Hp, out hpText);
            mpBar = BuildStatRow(content, "Mp", "MP", -82f, 20f, HudStyle.Mp, out mpText);

            var portrait = HudStyle.Place(root, "Portrait", bottomLeft, bottomLeft, new Vector2(40f, 40f), new Vector2(152f, 152f));
            BuildEmblem(portrait);

            // 城外のデバフ。外にいる間と、戻ってから回復しきるまで出す。
            debuffRow = HudStyle.Place(root, "Debuffs", bottomLeft, bottomLeft, new Vector2(200f, 214f), new Vector2(700f, 46f));
            speedChip = BuildChip(debuffRow, "Speed");
            attackChip = BuildChip(debuffRow, "Attack");
            drainChip = BuildChip(debuffRow, "Drain");

            BuildTowerList(root);

            // 右上: いる場所
            var area = HudStyle.Place(root, "Area", Vector2.one, Vector2.one, new Vector2(-40f, -30f), new Vector2(360f, 56f));
            areaText = HudStyle.AddText(HudStyle.Filled(area, "Name"), "", 38, pal.Light, TextAnchor.MiddleRight, 6f, pal.Outline);
            var line = HudStyle.Place(area, "Line", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, -4f), new Vector2(300f, 4f));
            HudStyle.DropShadow(line, HudShape.Kind.RoundRect, pal.Outline, 2f, 2f);
            HudStyle.Shape(line, "Line", HudShape.Kind.RoundRect, pal.Light, 2f);

            // 中央下: その場の案内
            var context = HudStyle.Place(root, "Context", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 76f), new Vector2(520f, 54f));
            HudStyle.DropShadow(context, HudShape.Kind.Ribbon, pal.Shadow, 0f, 4f);
            HudStyle.Shape(context, "Ribbon", HudShape.Kind.Ribbon, pal.Ribbon);
            contextText = HudStyle.AddText(HudStyle.Filled(context, "Text"), "", 25, pal.Light, TextAnchor.MiddleCenter);
            contextRoot = context.gameObject;
            contextRoot.SetActive(false);

            BuildSkillSlots(root);
        }

        private HudBar BuildStatRow(RectTransform content, string name, string label, float y, float barHeight, Color color, out Text value)
        {
            var topLeft = new Vector2(0f, 1f);
            var row = HudStyle.Place(content, name, topLeft, topLeft, new Vector2(0f, y), new Vector2(442f, 28f));
            HudStyle.Label(row, "Label", topLeft, topLeft, Vector2.zero, new Vector2(44f, 28f), label, 21, pal.Light, TextAnchor.MiddleLeft);
            value = HudStyle.Label(row, "Value", Vector2.one, Vector2.one, Vector2.zero, new Vector2(118f, 28f), "", 21, pal.Light, TextAnchor.MiddleRight);
            var barRt = HudStyle.Place(row, "Bar", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(48f, 0f), new Vector2(442f - 48f - 126f, barHeight));
            return HudBar.Create(barRt, color, pal.Track);
        }

        /// 魔王の肖像の代わり（絵は未定）。暗い赤の丸に「魔」。
        private void BuildEmblem(RectTransform parent)
        {
            HudStyle.DropShadow(parent, HudShape.Kind.Circle, pal.Shadow, 0f, 4f);
            HudStyle.Shape(parent, "Border", HudShape.Kind.Circle, pal.Light);
            var inner = HudStyle.Shape(parent, "Face", HudShape.Kind.Circle, HudStyle.Hex(0x2a0f18), 0f, HudStyle.FeltTexture);
            HudStyle.Stretch(inner.rectTransform, -6f);
            HudStyle.Stitch(parent, pal.Stitch, true, 0f, 14f);
            HudStyle.AddText(HudStyle.Filled(parent, "Mark"), "魔", 72, HudStyle.Hex(0xc8423c), TextAnchor.MiddleCenter, 5f, pal.Outline);
        }

        private Text BuildChip(RectTransform parent, string name)
        {
            var root = HudStyle.Place(parent, name, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(200f, 46f));
            HudStyle.DropShadow(root, HudShape.Kind.RoundRect, pal.Shadow, 23f, 5f);
            var ring = HudStyle.Shape(root, "Ring", HudShape.Kind.RoundRect, pal.Ribbon, 26f);
            HudStyle.Stretch(ring.rectTransform, 3f);
            HudStyle.Shape(root, "Paper", HudShape.Kind.RoundRect, pal.Paper, 23f, HudStyle.PaperTexture);
            var text = HudStyle.AddText(HudStyle.Filled(root, "Text"), "", 21, pal.Ink, TextAnchor.MiddleCenter);
            root.gameObject.SetActive(false);
            return text;
        }

        private void BuildTowerList(RectTransform root)
        {
            var spawners = new List<Spawner>(Spawner.All);
            spawners.Sort(HudStyle.CompareTowers);
            int count = Mathf.Min(spawners.Count, 6);
            float height = 54f + count * 34f;
            var panel = HudStyle.Place(root, "Towers", Vector2.zero, Vector2.zero, new Vector2(40f, 290f), new Vector2(330f, height));
            HudStyle.Felt(panel, pal, HudShape.Kind.RoundRect, 16f, 6f, 5f);
            var topLeft = new Vector2(0f, 1f);
            HudStyle.Label(panel, "Title", topLeft, topLeft, new Vector2(20f, -12f), new Vector2(200f, 30f), "塔", 23, pal.Light, TextAnchor.MiddleLeft);
            for (int i = 0; i < count; i++)
            {
                var sp = spawners[i];
                var rowRt = HudStyle.Place(panel, "Tower_" + i, topLeft, topLeft, new Vector2(20f, -48f - i * 34f), new Vector2(290f, 28f));
                var row = new TowerRow { Spawner = sp, Health = sp.GetComponent<HealthComponent>(), Root = rowRt.gameObject };
                row.Name = HudStyle.Label(rowRt, "Name", topLeft, topLeft, Vector2.zero, new Vector2(64f, 28f), HudStyle.TowerLabel(sp.name), 20, pal.Light, TextAnchor.MiddleLeft);
                var bar = HudStyle.Place(rowRt, "Bar", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(68f, 0f), new Vector2(150f, 12f));
                row.Bar = HudBar.Create(bar, HudStyle.Hp, pal.Track);
                row.Cost = HudStyle.Label(rowRt, "Cost", Vector2.one, Vector2.one, Vector2.zero, new Vector2(64f, 28f), "", 19, pal.Light, TextAnchor.MiddleRight);
                towers.Add(row);
            }
        }

        /// 右下のスキル枠。探知だけは中身がある（使うのは俯瞰画面のボタン）。
        private void BuildSkillSlots(RectTransform root)
        {
            const float w = 118f, h = 168f, gap = 10f;
            int count = SkillSlots.Length + 1;
            var row = HudStyle.Place(root, "Skills", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(count * w + (count - 1) * gap, h));
            var top = new Vector2(0.5f, 1f);
            for (int i = 0; i < count; i++)
            {
                var card = HudStyle.Place(row, "Slot_" + i, Vector2.zero, new Vector2(0.5f, 0f), new Vector2(i * (w + gap) + w * 0.5f, 0f), new Vector2(w, h));
                card.localEulerAngles = new Vector3(0f, 0f, i % 2 == 0 ? 1.3f : -1.3f);
                HudStyle.Paper(card, pal, HudShape.Kind.RoundRect, 10f);
                var icon = HudStyle.Place(card, "Icon", top, top, new Vector2(0f, -22f), new Vector2(76f, 76f));
                bool isDetect = i == count - 1;
                if (isDetect) BuildEye(icon);
                else
                {
                    HudStyle.Stitch(icon, pal.Ribbon, true, 0f, 0f).thickness = 3f;
                    HudStyle.AddText(HudStyle.Filled(icon, "Text"), "未定", 19, pal.Ribbon, TextAnchor.MiddleCenter);
                }
                HudStyle.Label(card, "Name", top, top, new Vector2(0f, -100f), new Vector2(w - 8f, 30f), isDetect ? "探知" : SkillSlots[i], 22, pal.Ink, TextAnchor.MiddleCenter);
                var pill = HudStyle.Place(card, "Cost", top, top, new Vector2(0f, -134f), new Vector2(76f, 24f));
                HudStyle.Shape(pill, "Pill", HudShape.Kind.RoundRect, HudStyle.Hex(0x5d3fb5), 12f);
                string cost = isDetect && detect != null ? "MP " + detect.MpCost : "MP ?";
                HudStyle.AddText(HudStyle.Filled(pill, "Text"), cost, 17, Color.white, TextAnchor.MiddleCenter);

                if (!isDetect) continue;
                var shade = HudStyle.Shape(card, "Cooldown", HudShape.Kind.RoundRect, new Color(0.05f, 0.02f, 0.04f, 0.72f), 10f);
                detectShade = shade.rectTransform;
                detectShade.gameObject.SetActive(false);
                detectTime = HudStyle.Label(card, "CooldownText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(w, 60f), "", 44, pal.Light, TextAnchor.MiddleCenter, 5f, pal.Outline);
                detectTime.gameObject.SetActive(false);
                HudStyle.Label(card, "Where", new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(160f, 26f), "俯瞰で使う", 17, pal.Light, TextAnchor.LowerCenter, 3f, pal.Outline);
            }
        }

        /// 探知の目（勇者の探知スキルの絵に合わせた、白目・青い瞳・黒目）。
        private static void BuildEye(RectTransform icon)
        {
            var white = HudStyle.Shape(icon, "White", HudShape.Kind.Circle, Color.white);
            white.rectTransform.anchorMin = new Vector2(0f, 0.22f);
            white.rectTransform.anchorMax = new Vector2(1f, 0.78f);
            var iris = HudStyle.Shape(icon, "Iris", HudShape.Kind.Circle, HudStyle.Hex(0x2c7be0));
            HudStyle.Stretch(iris.rectTransform, -22f);
            var pupil = HudStyle.Shape(icon, "Pupil", HudShape.Kind.Circle, HudStyle.Hex(0x10131c));
            HudStyle.Stretch(pupil.rectTransform, -30f);
        }

        // ---- 毎フレーム ----

        private void Update()
        {
            if (dk == null) return;
            UpdateSelf();
            UpdateDebuff();
            UpdateDetect();
            if (Time.unscaledTime >= nextTowerRefresh)
            {
                nextTowerRefresh = Time.unscaledTime + TowerRefreshInterval;
                UpdateTowers();
                bool inside = castle != null && castle.IsHostInside(dk);
                SetText(areaText, ref shownArea, inside ? "魔王城" : "魔王城の外");
                UpdateContext(inside);
            }
        }

        private void UpdateSelf()
        {
            if (health != null && (health.CurrentHP != shownHp || health.MaxHP != shownHpMax))
            {
                shownHp = health.CurrentHP;
                shownHpMax = health.MaxHP;
                hpBar.Set(shownHpMax > 0 ? (float)shownHp / shownHpMax : 0f);
                hpText.text = Mathf.Max(0, shownHp) + " / " + shownHpMax;
            }
            if (mana != null && (mana.CurrentMP != shownMp || mana.MaxMP != shownMpMax))
            {
                shownMp = mana.CurrentMP;
                shownMpMax = mana.MaxMP;
                mpBar.Set(shownMpMax > 0 ? (float)shownMp / shownMpMax : 0f);
                mpText.text = shownMp + " / " + shownMpMax;
            }
            if (level != null && level.CurrentLevel != shownLevel)
            {
                shownLevel = level.CurrentLevel;
                levelText.text = "Lv " + shownLevel + " / " + level.MaxLevel;
            }
        }

        /// デバフは移動速度と攻撃力に同じ割合でかかる。MPが減るのは城外にいる間だけ。
        private void UpdateDebuff()
        {
            int percent = debuff != null ? Mathf.RoundToInt(debuff.CurrentDebuff * 100f) : 0;
            bool draining = debuff != null && debuff.IsDraining;
            int key = percent * 2 + (draining ? 1 : 0);
            if (key == shownDebuff) return;
            shownDebuff = key;

            float x = 0f;
            x = PlaceChip(speedChip, percent > 0, "移動速度 −" + percent + "%", x);
            x = PlaceChip(attackChip, percent > 0, "攻撃力 −" + percent + "%", x);
            PlaceChip(drainChip, draining, "MP −" + (debuff != null ? debuff.MpDrainPerSecond : 0) + "/秒", x);
        }

        private static float PlaceChip(Text label, bool show, string text, float x)
        {
            var root = (RectTransform)label.transform.parent;
            if (root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
            if (!show) return x;
            label.text = text;
            float width = label.preferredWidth + 36f;
            root.sizeDelta = new Vector2(width, 46f);
            root.anchoredPosition = new Vector2(x, 0f);
            return x + width + 14f;
        }

        private void UpdateDetect()
        {
            if (detect == null && Time.frameCount % 30 == 0) detect = dk.GetComponent<DemonKingDetect>();
            if (detect == null || detectShade == null) return;
            float remaining = detect.CooldownRemaining;
            bool cooling = remaining > 0f;
            if (detectShade.gameObject.activeSelf != cooling) detectShade.gameObject.SetActive(cooling);
            if (detectTime.gameObject.activeSelf != cooling) detectTime.gameObject.SetActive(cooling);
            if (!cooling) return;
            float total = Mathf.Max(0.01f, BalanceConfig.Instance.dkDetectCooldown);
            detectShade.anchorMax = new Vector2(1f, Mathf.Clamp01(remaining / total));
            detectTime.text = Mathf.CeilToInt(remaining).ToString();
        }

        private void UpdateTowers()
        {
            foreach (var row in towers)
            {
                if (row.Spawner == null) { if (row.Root.activeSelf) row.Root.SetActive(false); continue; }
                int hp = row.Health != null ? row.Health.CurrentHP : 0;
                if (hp != row.ShownHp)
                {
                    row.ShownHp = hp;
                    row.Bar.Set(row.Health != null && row.Health.MaxHP > 0 ? (float)hp / row.Health.MaxHP : 0f);
                }
                string cost = row.Spawner.IsFunctional && hp > 0 ? row.Spawner.CurrentCost + "/" + row.Spawner.MaxCost : "壊れた";
                SetText(row.Cost, ref row.ShownCost, cost);
            }
        }

        private void UpdateContext(bool inside)
        {
            string text = null;
            if (view != null && view.IsAtViewer && view.TpsActive) text = "V で俯瞰へ";
            else if (debuff != null && debuff.IsDraining) text = "城外にいる間 MP が減り続ける";
            bool show = text != null;
            if (contextRoot.activeSelf != show) contextRoot.SetActive(show);
            if (show) SetText(contextText, ref shownContext, text);
        }

        private static void SetText(Text t, ref string shown, string value)
        {
            if (value == shown) return;
            shown = value;
            t.text = value;
        }
    }
}
