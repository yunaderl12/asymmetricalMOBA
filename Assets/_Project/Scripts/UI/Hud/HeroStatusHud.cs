using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Combat;
using Game.Core;
using Game.Hero;
using Game.Skill;

namespace Game.UI
{
    /// 勇者の画面の左下と右上。自分（肖像・Lv・HP・MP・Exp）、その上に状態異常と味方3人、右上にいる場所。
    /// 配置は Dead by Daylight にならい、チームの様子を左下にまとめる。
    public class HeroStatusHud : MonoBehaviour
    {
        private const float LowHpRatio = 0.25f;
        private const float AllyRefreshInterval = 0.5f;
        private const float AreaRefreshInterval = 0.25f;
        private const float TownAreaRadius = 40f; // 仮値: はじまりのまちと見なす半径（m）
        private const int AllyRows = 3;
        private const float HitShakeSeconds = 0.35f;
        private const float HitShakePixels = 9f;
        private const float HitShakeFrequency = 38f;

        private static readonly Color LowFelt = HudStyle.Hex(0x5a1f35);
        private static readonly Color LowStitch = HudStyle.Hex(0xffb4a8);

        private struct StatusInfo
        {
            public StatusEffectType Type;
            public string Name, Mark;
            public Color Color;
        }

        private static readonly StatusInfo[] Statuses =
        {
            new StatusInfo { Type = StatusEffectType.Stun, Name = "スタン", Mark = "★", Color = HudStyle.Hex(0xd99a00) },
            new StatusInfo { Type = StatusEffectType.Slow, Name = "スロー", Mark = "遅", Color = HudStyle.Hex(0x2c6bc9) },
            new StatusInfo { Type = StatusEffectType.DamageOverTime, Name = "継続ダメージ", Mark = "傷", Color = HudStyle.Hex(0xe8622c) },
            new StatusInfo { Type = StatusEffectType.Silence, Name = "沈黙", Mark = "黙", Color = HudStyle.Hex(0x7a55c8) },
        };

        private class AllyRow
        {
            public GameObject Root;
            public HudShape Felt;
            public Image Face;
            public Text Name, Info;
            public HudBar Bar;
            public GameObject BarRoot;
            public HealthComponent Health;
            public bool ShownDown;
            public int ShownHp = -1;
        }

        private class StatusChip
        {
            public RectTransform Root;
            public Text Label;
            public int ShownSeconds = -1;
        }

        private readonly HudStyle.Palette pal = HudStyle.HeroPalette;

        private GameObject hero;
        private HealthComponent health;
        private ManaComponent mana;
        private LevelComponent level;
        private ExpComponent exp;
        private SkillRuntime runtime;
        private StatusEffectController status;

        private RectTransform selfPanel, portraitPanel;
        private Vector2 selfPanelBase, portraitBase;
        private float hitShakeTimer;
        private HudShape panelFelt;
        private HudStitch panelStitch;
        private Text nameText, levelText, hpText, mpText, expText, areaText;
        private HudBar hpBar, mpBar, expBar;
        private readonly List<AllyRow> allies = new List<AllyRow>();
        private readonly List<StatusChip> chips = new List<StatusChip>();

        private float nextAllyRefresh, nextAreaRefresh;
        private int shownHp = -1, shownHpMax = -1, shownMp = -1, shownMpMax = -1, shownLevel = -1, shownExp = -1, shownExpReq = -1, shownCost = -1;
        private bool shownLow;
        private string shownArea;

        public static HeroStatusHud Create(GameObject hero)
        {
            var root = HudStyle.Filled(HudStyle.Canvas.transform, "HeroStatusHud");
            var hud = root.gameObject.AddComponent<HeroStatusHud>();
            hud.Bind(hero);
            hud.Build(root);
            return hud;
        }

        private void Bind(GameObject heroGO)
        {
            hero = heroGO;
            health = hero.GetComponent<HealthComponent>();
            mana = hero.GetComponent<ManaComponent>();
            level = hero.GetComponent<LevelComponent>();
            exp = hero.GetComponent<ExpComponent>();
            runtime = hero.GetComponent<SkillRuntime>();
            status = hero.GetComponent<StatusEffectController>();
        }

        // ---- 組み立て ----

        private void Build(RectTransform root)
        {
            var bottomLeft = Vector2.zero;
            var topLeft = new Vector2(0f, 1f);
            var classData = HudStyle.ClassOf(hero);

            // 自分のフェルト。肖像が左に重なるので、中身は左を広くあける。
            var panel = HudStyle.Place(root, "Self", bottomLeft, bottomLeft, new Vector2(118f, 40f), new Vector2(560f, 154f));
            selfPanel = panel;
            selfPanelBase = panel.anchoredPosition;
            HudStyle.DropShadow(panel, HudShape.Kind.RoundRect, pal.Shadow, 20f, 6f);
            panelFelt = HudStyle.Shape(panel, "Felt", HudShape.Kind.RoundRect, pal.Felt, 20f, HudStyle.FeltTexture);
            panelStitch = HudStyle.Stitch(panel, pal.Stitch, false, 14f, 7f);

            var content = HudStyle.Place(panel, "Content", topLeft, topLeft, new Vector2(94f, -14f), new Vector2(442f, 130f));
            nameText = HudStyle.Label(content, "Class", topLeft, topLeft, Vector2.zero, new Vector2(150f, 32f),
                HudStyle.ClassLabel(classData != null ? classData.className : null), 25, Color.white, TextAnchor.MiddleLeft);
            levelText = HudStyle.Label(content, "Level", topLeft, topLeft, new Vector2(150f, 0f), new Vector2(200f, 32f), "", 22, pal.Light, TextAnchor.MiddleLeft);
            hpBar = BuildStatRow(content, "Hp", "HP", -38f, 24f, HudStyle.Hp, 21, out hpText);
            mpBar = BuildStatRow(content, "Mp", "MP", -70f, 20f, HudStyle.Mp, 21, out mpText);
            expBar = BuildStatRow(content, "Exp", "Exp", -100f, 10f, HudStyle.Exp, 18, out expText);

            var portrait = HudStyle.Place(root, "Portrait", bottomLeft, bottomLeft, new Vector2(40f, 40f), new Vector2(152f, 152f));
            HudStyle.Portrait(portrait, classData != null ? classData.icon : null, classData != null ? classData.avatarColor : pal.Felt, 6f, pal.Shadow);
            portraitPanel = portrait;
            portraitBase = portrait.anchoredPosition;

            // 味方。下から順に積む（自分の状態異常の上）。
            for (int i = 0; i < AllyRows; i++) allies.Add(BuildAlly(root, new Vector2(40f, 290f + i * 76f)));

            // 状態異常。自分のフェルトの上に横へ並べる。
            var chipRow = HudStyle.Place(root, "Statuses", bottomLeft, bottomLeft, new Vector2(200f, 214f), new Vector2(700f, 46f));
            foreach (var info in Statuses) chips.Add(BuildChip(chipRow, info));

            // 右上: いる場所。
            var area = HudStyle.Place(root, "Area", Vector2.one, Vector2.one, new Vector2(-40f, -30f), new Vector2(360f, 56f));
            areaText = HudStyle.AddText(HudStyle.Filled(area, "Name"), "", 38, Color.white, TextAnchor.MiddleRight, 6f, pal.Outline);
            var line = HudStyle.Place(area, "Line", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, -4f), new Vector2(300f, 4f));
            HudStyle.DropShadow(line, HudShape.Kind.RoundRect, pal.Outline, 2f, 2f);
            HudStyle.Shape(line, "Line", HudShape.Kind.RoundRect, Color.white, 2f);
        }

        /// 「HP [バー] 156 / 180」の1段。
        private HudBar BuildStatRow(RectTransform content, string name, string label, float y, float barHeight, Color color, int fontSize, out Text value)
        {
            var topLeft = new Vector2(0f, 1f);
            var row = HudStyle.Place(content, name, topLeft, topLeft, new Vector2(0f, y), new Vector2(442f, 28f));
            HudStyle.Label(row, "Label", topLeft, topLeft, Vector2.zero, new Vector2(44f, 28f), label, fontSize, pal.Light, TextAnchor.MiddleLeft);
            value = HudStyle.Label(row, "Value", Vector2.one, Vector2.one, Vector2.zero, new Vector2(118f, 28f), "", fontSize, pal.Light, TextAnchor.MiddleRight);
            var barRt = HudStyle.Place(row, "Bar", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(48f, 0f), new Vector2(442f - 48f - 126f, barHeight));
            return HudBar.Create(barRt, color, pal.Track);
        }

        private AllyRow BuildAlly(RectTransform root, Vector2 position)
        {
            var topLeft = new Vector2(0f, 1f);
            var rowRt = HudStyle.Place(root, "Ally", Vector2.zero, Vector2.zero, position, new Vector2(340f, 64f));
            var row = new AllyRow { Root = rowRt.gameObject };

            var strip = HudStyle.Place(rowRt, "Strip", topLeft, topLeft, new Vector2(34f, -6f), new Vector2(306f, 52f));
            HudStyle.DropShadow(strip, HudShape.Kind.RoundRect, pal.Shadow, 12f, 4f);
            row.Felt = HudStyle.Shape(strip, "Felt", HudShape.Kind.RoundRect, pal.Felt, 12f, HudStyle.FeltTexture);
            row.Name = HudStyle.Label(strip, "Name", topLeft, topLeft, new Vector2(42f, -2f), new Vector2(140f, 28f), "", 20, pal.Light, TextAnchor.MiddleLeft);
            row.Info = HudStyle.Label(strip, "Info", Vector2.one, Vector2.one, new Vector2(-14f, -2f), new Vector2(130f, 28f), "", 18, pal.Light, TextAnchor.MiddleRight);
            var bar = HudStyle.Place(strip, "Bar", topLeft, topLeft, new Vector2(42f, -32f), new Vector2(250f, 12f));
            row.Bar = HudBar.Create(bar, HudStyle.Hp, pal.Track);
            row.BarRoot = bar.gameObject;

            var face = HudStyle.Place(rowRt, "Portrait", topLeft, topLeft, Vector2.zero, new Vector2(64f, 64f));
            row.Face = HudStyle.Portrait(face, null, pal.Felt, 4f, pal.Shadow);
            row.Root.SetActive(false);
            return row;
        }

        private StatusChip BuildChip(RectTransform parent, StatusInfo info)
        {
            var root = HudStyle.Place(parent, "Status_" + info.Type, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(200f, 46f));
            HudStyle.DropShadow(root, HudShape.Kind.RoundRect, pal.Shadow, 23f, 5f);
            HudStyle.Shape(root, "Border", HudShape.Kind.RoundRect, Color.white, 26f).rectTransform.offsetMin = new Vector2(-3f, -3f);
            ((RectTransform)root.Find("Border")).offsetMax = new Vector2(3f, 3f);
            HudStyle.Shape(root, "Paper", HudShape.Kind.RoundRect, pal.Paper, 23f, HudStyle.PaperTexture);

            var mark = HudStyle.Place(root, "Mark", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(34f, 34f));
            HudStyle.Shape(mark, "Circle", HudShape.Kind.Circle, info.Color);
            HudStyle.AddText(HudStyle.Filled(mark, "Text"), info.Mark, 19, Color.white, TextAnchor.MiddleCenter);

            var label = HudStyle.Label(root, "Label", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(48f, 0f), new Vector2(150f, 40f), "", 21, pal.Ink, TextAnchor.MiddleLeft);
            root.gameObject.SetActive(false);
            return new StatusChip { Root = root, Label = label };
        }

        // ---- 毎フレーム ----

        private void Update()
        {
            if (hero == null) return;
            UpdateSelf();
            UpdateHitShake();
            UpdateStatuses();
            if (Time.unscaledTime >= nextAllyRefresh)
            {
                nextAllyRefresh = Time.unscaledTime + AllyRefreshInterval;
                RefreshAllies();
            }
            UpdateAllies();
            if (Time.unscaledTime >= nextAreaRefresh)
            {
                nextAreaRefresh = Time.unscaledTime + AreaRefreshInterval;
                SetArea(AreaName(hero.transform.position));
            }
        }

        private void UpdateSelf()
        {
            if (health != null && (health.CurrentHP != shownHp || health.MaxHP != shownHpMax))
            {
                // HP が減ったら自分の欄を小さく揺らす（殴られたことに気づけるように）。
                if (shownHp >= 0 && health.CurrentHP < shownHp && health.MaxHP == shownHpMax) hitShakeTimer = HitShakeSeconds;
                shownHp = health.CurrentHP;
                shownHpMax = health.MaxHP;
                float ratio = shownHpMax > 0 ? (float)shownHp / shownHpMax : 0f;
                hpBar.Set(ratio);
                hpText.text = Mathf.Max(0, shownHp) + " / " + shownHpMax;
                bool low = ratio <= LowHpRatio;
                if (low != shownLow)
                {
                    shownLow = low;
                    panelFelt.color = low ? LowFelt : pal.Felt;
                    panelStitch.color = low ? LowStitch : pal.Stitch;
                    hpBar.SetColor(low ? HudStyle.HpLow : HudStyle.Hp);
                }
            }

            // 勇者のMPは ManaComponent が持っている。消費するのは魔法系のスキルだけ。
            int cost = runtime != null && runtime.CurrentSkill != null ? runtime.CurrentSkill.mpCost : 0;
            if (mana != null && (mana.CurrentMP != shownMp || mana.MaxMP != shownMpMax || cost != shownCost))
            {
                shownMp = mana.CurrentMP;
                shownMpMax = mana.MaxMP;
                shownCost = cost;
                float max = Mathf.Max(1, shownMpMax);
                mpBar.Set(shownMp / max);
                mpText.text = shownMp + " / " + shownMpMax;
                // 選択中のスキルで減る分。足りないときは出さない（カードの消費MPが赤くなる）。
                if (cost > 0 && cost <= shownMp) mpBar.SetPreview((shownMp - cost) / max, shownMp / max);
                else mpBar.SetPreview(0f, 0f);
            }

            if (level != null && level.CurrentLevel != shownLevel)
            {
                shownLevel = level.CurrentLevel;
                levelText.text = "Lv " + shownLevel + " / " + level.MaxLevel;
            }

            if (exp != null && (exp.CurrentExp != shownExp || exp.RequiredExp != shownExpReq))
            {
                shownExp = exp.CurrentExp;
                shownExpReq = exp.RequiredExp;
                bool maxed = level != null && level.IsMaxLevel;
                expBar.Set(maxed ? 1f : (shownExpReq > 0 ? (float)shownExp / shownExpReq : 0f));
                expText.text = maxed ? "MAX" : "次まで " + HudStyle.Thousands(Mathf.Max(0, shownExpReq - shownExp));
            }
        }

        // 左右に細かく揺れて、すぐ収まる。
        private void UpdateHitShake()
        {
            if (selfPanel == null || hitShakeTimer <= 0f) return;
            hitShakeTimer -= Time.unscaledDeltaTime;
            float fade = Mathf.Clamp01(hitShakeTimer / HitShakeSeconds);
            float t = (HitShakeSeconds - hitShakeTimer) * HitShakeFrequency;
            Vector2 offset = new Vector2(Mathf.Sin(t * 2f * Mathf.PI), Mathf.Sin(t * 1.3f * Mathf.PI) * 0.4f) * HitShakePixels * fade * fade;
            selfPanel.anchoredPosition = selfPanelBase + offset;
            if (portraitPanel != null) portraitPanel.anchoredPosition = portraitBase + offset;
        }

        private void UpdateStatuses()
        {
            // StatusEffectController は最初に状態異常を受けたときに付くので、なければ探し直す。
            if (status == null && Time.frameCount % 15 == 0) status = hero.GetComponent<StatusEffectController>();
            float x = 0f;
            for (int i = 0; i < chips.Count; i++)
            {
                var chip = chips[i];
                bool active = status != null && status.IsActive(Statuses[i].Type);
                if (chip.Root.gameObject.activeSelf != active) chip.Root.gameObject.SetActive(active);
                if (!active) continue;
                int seconds = Mathf.CeilToInt(status.Remaining(Statuses[i].Type));
                if (seconds != chip.ShownSeconds)
                {
                    chip.ShownSeconds = seconds;
                    chip.Label.text = Statuses[i].Name + " " + seconds;
                }
                float width = 48f + chip.Label.preferredWidth + 18f;
                chip.Root.sizeDelta = new Vector2(width, 46f);
                chip.Root.anchoredPosition = new Vector2(x, 0f);
                x += width + 14f;
            }
        }

        private void RefreshAllies()
        {
            var found = new List<GameObject>();
            foreach (var h in GameObject.FindGameObjectsWithTag("Player"))
            {
                if (h == null || h == hero || h.GetComponent<HealthComponent>() == null) continue;
                found.Add(h);
            }
            found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            for (int i = 0; i < allies.Count; i++)
            {
                var row = allies[i];
                var h = i < found.Count ? found[i] : null;
                if (row.Root.activeSelf != (h != null)) row.Root.SetActive(h != null);
                if (h == null) { row.Health = null; continue; }
                var hc = h.GetComponent<HealthComponent>();
                if (row.Health == hc) continue;
                row.Health = hc;
                row.ShownHp = -1;
                row.ShownDown = false;
                row.Name.text = HudStyle.HeroName(h);
                ApplyDown(row, false, HudStyle.ClassOf(h));
            }
        }

        private void UpdateAllies()
        {
            foreach (var row in allies)
            {
                if (row.Health == null) continue;
                bool down = row.Health.IsDead || row.Health.CurrentHP <= 0;
                if (down != row.ShownDown)
                {
                    row.ShownDown = down;
                    ApplyDown(row, down, HudStyle.ClassOf(row.Health.gameObject));
                }
                if (down || row.Health.CurrentHP == row.ShownHp) continue;
                row.ShownHp = row.Health.CurrentHP;
                row.Bar.Set(row.Health.MaxHP > 0 ? (float)row.ShownHp / row.Health.MaxHP : 0f);
            }
        }

        /// 倒れた味方は灰色の布にして、バーの代わりに「倒れた」を出す。
        private void ApplyDown(AllyRow row, bool down, HeroClassData data)
        {
            row.Felt.color = down ? HudStyle.DownFelt : pal.Felt;
            row.BarRoot.SetActive(!down);
            row.Info.text = down ? "倒れた" : HudStyle.ClassLabel(data != null ? data.className : null);
            row.Info.color = down ? HudStyle.CostPill : pal.Light;
            Color? grey = down ? new Color(0.45f, 0.45f, 0.5f, 1f) : (Color?)null;
            HudStyle.SetPortrait(row.Face, data != null ? data.icon : null, data != null ? data.avatarColor : pal.Felt, grey);
        }

        private void SetArea(string area)
        {
            if (area == shownArea) return;
            shownArea = area;
            areaText.text = area;
        }

        /// 今いる場所の名前。まちと魔王城は中心からの距離、水の上は川か橋、それ以外は地形の種類で決める。
        public static string AreaName(Vector3 p)
        {
            var map = StageMap.Map;
            if (map != null)
            {
                var flat = new Vector2(p.x, p.z);
                if (Vector2.Distance(flat, new Vector2(map.townX, map.townZ)) < TownAreaRadius) return "はじまりのまち";
                if (map.islandRadius > 0f && Vector2.Distance(flat, new Vector2(map.castleX, map.castleZ)) < map.islandRadius) return "魔王城";
            }
            if (StageMap.IsSea(p) || StageMap.IsDeepWater(p)) return "橋";
            if (StageMap.IsShallow(p)) return "浅瀬";
            switch (StageMap.BiomeAt(p))
            {
                case StageMap.Biome.Desert: return "砂漠";
                case StageMap.Biome.Forest: return "森";
                case StageMap.Biome.Mountain: return "山";
                default: return "草原";
            }
        }
    }
}
