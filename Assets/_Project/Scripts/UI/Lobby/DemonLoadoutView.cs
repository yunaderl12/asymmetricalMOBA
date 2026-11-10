using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Game.Core;
using Game.DemonKing;
using Game.Monster;

namespace Game.UI
{
    /// 魔王の編成（2026-11-08 に地図中心へ作り直し）。
    /// 真ん中に絵地図を大きく出し、山・森・砂漠それぞれに通常魔物3枠と中ボス1体、3本の橋に番人（ゴーレム）を置く。
    /// 右にフィールド魔法5枠（試合中に 1〜5 キーで使う）、左に説明。魔王ロビーとマッチロビー（魔王側）の両方で使う。
    /// 変えた内容はすぐ GameSession に書く。通常魔物はそのバイオームの BiomeSpawnTable の候補から選ぶ。
    public class DemonLoadoutView : MonoBehaviour
    {
        public static readonly string[] BiomeLabels = { "山", "森", "砂漠" };
        public static readonly string[] BridgeLabels = { "山の橋", "砂漠の橋", "森の橋" };
        private const float MapHeight = 780f;
        private const float SlotSize = 66f;
        private static readonly Color[] TokenColors =
        {
            HudStyle.Hex(0x5b3a8a), HudStyle.Hex(0x2f5e7a), HudStyle.Hex(0x7a5a1e), HudStyle.Hex(0x3f6b3a),
            HudStyle.Hex(0x7a2f4e), HudStyle.Hex(0x4a4f7a), HudStyle.Hex(0x6b4a2e), HudStyle.Hex(0x2f6b66),
        };
        private static readonly Color Empty = HudStyle.Hex(0x120a0e);
        private static readonly Color BossGold = HudStyle.Hex(0xc9962e);

        [Serializable]
        private class Illust
        {
            public float width, height, centerX, centerScreenY, upZ, upY;
        }

        private struct Option
        {
            public string label;
            public string note;
            public Sprite icon;
            public bool disabled;
            public Action pick;
            public Action hover;
        }

        private HudStyle.Palette pal;
        private BiomeSpawnData table;
        private List<MonsterData> guardians;
        private List<FieldSpellData> spells;

        private MonsterData[] biomeMonsters;
        private bool[] bossOn;
        private bool[] guardianOn;
        private FieldSpellData[] equipped;

        private Illust illust;
        private Vector2 mapSize;
        private RectTransform mapArea;
        private RectTransform marks;
        private RectTransform spellList;
        private Text infoName;
        private Text infoText;
        private Text status;
        private RectTransform pickerLayer;

        public static DemonLoadoutView Create(Transform stage, List<MonsterData> pool, List<MonsterData> guardians, int maxCost)
        {
            var rt = LobbyStyle.At(stage, "DemonLoadout", 0f, 0f, LobbyStyle.StageWidth, LobbyStyle.StageHeight);
            var view = rt.gameObject.AddComponent<DemonLoadoutView>();
            view.guardians = guardians.FindAll(m => m != null);
            view.table = BiomeSpawnData.Load();
            view.spells = new List<FieldSpellData>(Resources.LoadAll<FieldSpellData>("FieldSpells"));
            view.spells.Sort((a, b) => a.order.CompareTo(b.order));
            view.Load();
            view.Build(rt);
            return view;
        }

        // ---- データ ----

        private List<MonsterData> Candidates(int biome)
        {
            var entry = table != null ? table.For(GameSession.PlacementBiomes[biome]) : null;
            return entry != null ? entry.monsters.FindAll(m => m != null) : new List<MonsterData>();
        }

        private BossData BossOf(int biome)
        {
            var entry = table != null ? table.For(GameSession.PlacementBiomes[biome]) : null;
            return entry != null ? entry.boss : null;
        }

        private void Load()
        {
            var session = GameSession.GetOrCreate();
            int slots = GameSession.BIOME_MONSTER_SLOTS;
            biomeMonsters = new MonsterData[GameSession.BIOME_COUNT * slots];
            bossOn = new bool[GameSession.BIOME_COUNT];
            guardianOn = new bool[GameSession.BRIDGE_COUNT];
            equipped = new FieldSpellData[GameSession.FIELD_SPELL_SLOTS];
            for (int b = 0; b < GameSession.BIOME_COUNT; b++)
            {
                bossOn[b] = session.BiomeBossOn[b];
                var candidates = Candidates(b);
                for (int i = 0; i < slots; i++)
                {
                    var m = session.PlacementChosen ? session.BiomeMonsters[b * slots + i] : (i < candidates.Count ? candidates[i] : null);
                    // 候補から外れた魔物（表を変えたとき）は空にする。
                    biomeMonsters[b * slots + i] = m != null && candidates.Contains(m) ? m : null;
                }
            }
            for (int i = 0; i < guardianOn.Length; i++) guardianOn[i] = session.BridgeGuardianOn[i];
            bool any = false;
            for (int i = 0; i < equipped.Length; i++)
            {
                equipped[i] = session.FieldSpells[i];
                any |= equipped[i] != null;
            }
            if (!any)
                for (int i = 0; i < equipped.Length && i < spells.Count; i++) equipped[i] = spells[i];
        }

        public void SaveToSession()
        {
            var session = GameSession.GetOrCreate();
            session.PlacementChosen = true;
            Array.Copy(biomeMonsters, session.BiomeMonsters, biomeMonsters.Length);
            Array.Copy(bossOn, session.BiomeBossOn, bossOn.Length);
            Array.Copy(guardianOn, session.BridgeGuardianOn, guardianOn.Length);
            for (int i = 0; i < session.BridgeGuardians.Length; i++)
                session.BridgeGuardians[i] = guardianOn[i] && guardians.Count > 0 ? guardians[0] : null;
            Array.Copy(equipped, session.FieldSpells, equipped.Length);
            // 塔はすべて砂漠に立つので、塔で出せる魔物（デッキ）は砂漠に置いた通常魔物。5本の塔に順に割り振る。
            int desert = GameSession.PlacementIndex(StageMap.Biome.Desert) * GameSession.BIOME_MONSTER_SLOTS;
            session.SetTowerMonsters(new List<MonsterData>(biomeMonsters).GetRange(desert, GameSession.BIOME_MONSTER_SLOTS));
        }

        // ---- 組み立て ----

        private void Build(RectTransform rt)
        {
            pal = HudStyle.DemonPalette;

            // 左: 説明
            var left = LobbyStyle.At(rt, "Info", 40f, 130f, 380f, 760f);
            LobbyStyle.Tilt(left, -0.6f);
            LobbyStyle.PaperSheet(left, pal, 6f, 1, 3, 6f);
            LobbyStyle.Text(left, "Header", 24f, 20f, 330f, 44f, "配置", 30, pal.Ink);
            LobbyStyle.Paragraph(left, "How", 24f, 70f, 336f, 210f,
                "地図の上の枠を押して、山・森・砂漠に通常魔物（3枠）と中ボスを置く。\n橋の「番」を押すと番人（ゴーレム）を置く・外す。\n右のフィールド魔法は試合中に 1〜5 キーで使う（クールタイムだけ）。", 18, pal.Ink);
            LobbyStyle.DashedLine(left, 24f, 296f, 332f, LobbyStyle.Divider);
            infoName = LobbyStyle.Text(left, "InfoName", 24f, 312f, 336f, 40f, "", 26, pal.Ink);
            infoText = LobbyStyle.Paragraph(left, "InfoText", 24f, 356f, 336f, 380f, "", 18, pal.Ink);

            // 真ん中: 絵地図
            BuildMap(rt);

            // 右: フィールド魔法
            var right = LobbyStyle.At(rt, "Spells", 1380f, 130f, 500f, 760f);
            LobbyStyle.Tilt(right, 0.5f);
            LobbyStyle.FeltPlate(right, pal, 18f, 7f, 6f);
            LobbyStyle.Text(right, "Header", 26f, 18f, 300f, 46f, "フィールド魔法", 30, pal.Light, TextAnchor.MiddleLeft, 5f, pal.Outline);
            LobbyStyle.Text(right, "Hint", 250f, 24f, 224f, 36f, "5つ装備・押して入れ替え", 16, new Color(pal.Light.r, pal.Light.g, pal.Light.b, 0.7f), TextAnchor.MiddleRight);
            spellList = LobbyStyle.At(right, "List", 20f, 76f, 460f, 670f);

            var statusRt = LobbyStyle.At(rt, "Status", 470f, 860f, 820f, 44f);
            LobbyStyle.Tilt(statusRt, -0.8f);
            HudStyle.Paper(statusRt, pal, HudShape.Kind.RoundRect, 4f, 8, 4f);
            status = HudStyle.AddText(HudStyle.Filled(statusRt, "Text"), "", 19, pal.Ink, TextAnchor.MiddleCenter);

            pickerLayer = LobbyStyle.At(rt, "PickerLayer", 0f, 0f, LobbyStyle.StageWidth, LobbyStyle.StageHeight);

            Status("地図の枠を押して魔物を置く");
            ShowInfo("魔王の配置", "山・森・砂漠に置いた魔物が、試合中にそのバイオームで自然に湧く。同じ魔物を2枠に置くと、その魔物が多く湧く。中ボスを置かないバイオームは、橋の結界も初めから開いている。");
            RefreshAll();
        }

        private void BuildMap(RectTransform rt)
        {
            LoadIllust(out var texture);
            float w = illust != null ? MapHeight * illust.width / illust.height : MapHeight;
            mapSize = new Vector2(w, MapHeight);
            float cx = 440f + (920f - w) * 0.5f;
            var frame = LobbyStyle.At(rt, "MapFrame", cx - 14f, 92f - 14f, w + 28f, MapHeight + 28f);
            HudStyle.DropShadow(frame, HudShape.Kind.RoundRect, pal.Shadow, 18f, 7f);
            HudStyle.Paper(frame, pal, HudShape.Kind.Torn, 0f, 5, 0f);
            mapArea = LobbyStyle.At(frame, "Map", 14f, 14f, w, MapHeight);
            var image = HudStyle.AddGraphic<RawImage>(mapArea);
            image.texture = texture;
            image.color = texture != null ? Color.white : HudStyle.Hex(0x6a8a54);
            image.raycastTarget = false;
            marks = HudStyle.Place(mapArea, "Marks", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, mapSize);
        }

        private void LoadIllust(out Texture2D texture)
        {
            texture = Resources.Load<Texture2D>("UI/StageMap_Illust");
            var json = Resources.Load<TextAsset>("UI/StageMap_Illust");
            if (texture == null || json == null) return;
            var info = JsonUtility.FromJson<Illust>(json.text);
            if (info != null && info.width > 0f && info.height > 0f) illust = info;
        }

        /// ゲームの座標（x, z と高さ y）から地図の上の位置（中心が原点）。
        private Vector2 MapPoint(float x, float z, float y = 0f)
        {
            if (illust == null) return new Vector2(x / 500f * mapSize.x, z / 500f * mapSize.y);
            float iu = (x - illust.centerX) / illust.width;
            float iv = (z * illust.upZ + y * illust.upY - illust.centerScreenY) / illust.height;
            return new Vector2(iu * mapSize.x, iv * mapSize.y);
        }

        private void RefreshAll()
        {
            LobbyStyle.Clear(marks);
            var map = StageMap.Map;
            var plazas = new[] { map.dragonPlaza, map.forestPlaza, map.scorpionPlaza };
            // 札の置き場所（地図の上、バイオームの広場の近く）。はみ出さないように寄せる。
            var offsets = new[] { new Vector2(-40f, 120f), new Vector2(-30f, -115f), new Vector2(-150f, 90f) };
            for (int b = 0; b < GameSession.BIOME_COUNT; b++)
            {
                var plaza = plazas[b];
                Vector2 at = plaza != null ? MapPoint(plaza.x, plaza.z, plaza.height) : Vector2.zero;
                BuildBiomeCard(b, at, at + offsets[b]);
            }
            var starts = map.bridgeStarts;
            for (int i = 0; i < GameSession.BRIDGE_COUNT && i * 2 + 1 < starts.Length; i++)
                BuildGuardianPin(i, MapPoint(starts[i * 2], starts[i * 2 + 1]));
            BuildSpells();
            SaveToSession();
        }

        private Color ColorOf(MonsterData m)
        {
            int h = 0;
            foreach (char c in m.name) h = h * 31 + c;
            return TokenColors[Mathf.Abs(h) % TokenColors.Length];
        }

        private static string GlyphOf(MonsterData m)
        {
            if (m.name == "BasicMob") return "ゴ";
            return string.IsNullOrEmpty(m.monsterName) ? "?" : m.monsterName.Substring(0, 1);
        }

        private static string NameOf(MonsterData m) => m.name == "BasicMob" ? "ゴブリン" : m.monsterName;

        /// 丸い枠の内側に魔物の丸いアイコンを置く（inset はふちから内側への幅）。
        private static void Face(RectTransform parent, Sprite icon, float inset)
        {
            var face = HudStyle.Node(parent, "Icon");
            HudStyle.Stretch(face, -inset);
            var image = HudStyle.AddGraphic<Image>(face);
            image.sprite = icon;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        // ---- バイオームの札 ----

        private void BuildBiomeCard(int b, Vector2 plaza, Vector2 center)
        {
            const float w = 320f, h = 172f, scale = 0.88f;
            Vector2 half = mapSize * 0.5f;
            center.x = Mathf.Clamp(center.x, -half.x + w * scale * 0.5f + 6f, half.x - w * scale * 0.5f - 6f);
            center.y = Mathf.Clamp(center.y, -half.y + h * scale * 0.5f + 6f, half.y - h * scale * 0.5f - 6f);

            // 広場の印と、札へのひも。
            var dot = HudStyle.Place(marks, "Plaza_" + b, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), plaza, new Vector2(18f, 18f));
            HudStyle.Shape(dot, "Dot", HudShape.Kind.Circle, bossOn[b] ? BossGold : new Color(1f, 1f, 1f, 0.5f));
            Line(plaza, center, new Color(0.15f, 0.08f, 0.1f, 0.55f), 3f);

            var card = HudStyle.Place(marks, "Biome_" + b, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), center, new Vector2(w, h));
            card.localRotation = Quaternion.Euler(0f, 0f, b == 1 ? 0.8f : -0.7f);
            card.localScale = Vector3.one * scale;
            HudStyle.Felt(card, pal, HudShape.Kind.RoundRect, 14f, 5f, 5f);
            LobbyStyle.Text(card, "Name", 14f, 6f, 120f, 36f, BiomeLabels[b], 26, pal.Light, TextAnchor.MiddleLeft, 4f, pal.Outline);
            LobbyStyle.Text(card, "Hint", 110f, 10f, 196f, 28f, "通常魔物 3枠＋中ボス", 14, new Color(pal.Light.r, pal.Light.g, pal.Light.b, 0.65f), TextAnchor.MiddleRight);

            int slots = GameSession.BIOME_MONSTER_SLOTS;
            for (int i = 0; i < slots; i++)
            {
                int index = b * slots + i;
                var m = biomeMonsters[index];
                var slot = LobbyStyle.At(card, "Slot_" + i, 14f + i * 74f, 46f, SlotSize, SlotSize);
                var fill = m != null ? ColorOf(m) : Empty;
                LobbyStyle.IconToken(slot, fill, m != null ? pal.Light : new Color(pal.Stitch.r, pal.Stitch.g, pal.Stitch.b, 0.6f), m != null ? m.icon : null,
                    m != null ? GlyphOf(m) : "＋", m != null ? 28 : 26, pal, 3f);
                LobbyStyle.Text(card, "SlotName_" + i, 4f + i * 74f, 114f, 86f, 26f, m != null ? NameOf(m) : "空き", 14, m != null ? pal.Light : new Color(pal.Light.r, pal.Light.g, pal.Light.b, 0.5f), TextAnchor.MiddleCenter);
                LobbyStyle.MakeHitButton(slot, () => OpenMonsterPicker(b, index, slot));
                var monster = m;
                slot.gameObject.AddComponent<LobbyHover>().onEnter = () =>
                {
                    if (monster != null) ShowMonster(monster);
                    else ShowInfo(BiomeLabels[b] + "の空き枠", "押すと" + BiomeLabels[b] + "に置ける魔物から選べる。");
                };
            }

            // 中ボス: 押すと置く・外す。
            var boss = BossOf(b);
            var bossRt = LobbyStyle.At(card, "Boss", 236f, 40f, 74f, 74f);
            var ring = HudStyle.Shape(bossRt, "Ring", HudShape.Kind.Circle, bossOn[b] && boss != null ? BossGold : new Color(0.3f, 0.25f, 0.27f, 1f));
            var inner = LobbyStyle.AtCenter(bossRt, "Inner", 37f, 37f, 62f, 62f);
            HudStyle.Shape(inner, "Felt", HudShape.Kind.Circle, bossOn[b] && boss != null ? HudStyle.Hex(0x6a1e1e) : Empty, 0f, HudStyle.FeltTexture);
            if (bossOn[b] && boss != null && boss.icon != null) Face(inner, boss.icon, 3f);
            else
            {
                string bossGlyph = boss != null && !string.IsNullOrEmpty(boss.bossName) ? boss.bossName.Substring(0, 1) : "王";
                HudStyle.AddText(HudStyle.Filled(inner, "Glyph"), bossOn[b] ? bossGlyph : "×", 28, bossOn[b] ? Color.white : new Color(1f, 1f, 1f, 0.4f), TextAnchor.MiddleCenter, 4f, pal.Outline);
            }
            LobbyStyle.Text(card, "BossName", 214f, 114f, 118f, 26f, boss == null ? "（なし）" : bossOn[b] ? boss.bossName : "置かない", 14,
                bossOn[b] ? HudStyle.Gold : new Color(pal.Light.r, pal.Light.g, pal.Light.b, 0.5f), TextAnchor.MiddleCenter);
            LobbyStyle.Text(card, "BossCaption", 214f, 136f, 118f, 24f, "中ボス", 13, new Color(pal.Light.r, pal.Light.g, pal.Light.b, 0.6f), TextAnchor.MiddleCenter);
            if (boss != null)
            {
                LobbyStyle.MakeButton(bossRt, ring, () =>
                {
                    bossOn[b] = !bossOn[b];
                    Status(BiomeLabels[b] + "の中ボス: " + (bossOn[b] ? boss.bossName + "を置く" : "置かない（橋の結界も開く）"));
                    RefreshAll();
                });
                bossRt.gameObject.AddComponent<LobbyHover>().onEnter = () => ShowInfo(boss.bossName + "（中ボス）",
                    BiomeLabels[b] + "の広場に出る中ボス。倒されるまで" + BridgeLabelOf(b) + "の結界が閉じたまま。押すと置く・外すを切り替える。");
            }
        }

        private static string BridgeLabelOf(int biome) => biome == 0 ? "山の橋" : biome == 1 ? "森の橋" : "砂漠の橋";

        private void OpenMonsterPicker(int biome, int index, RectTransform near)
        {
            var options = new List<Option>
            {
                new Option { label = "空き", note = "", pick = () => SetMonster(biome, index, null) }
            };
            foreach (var m in Candidates(biome))
            {
                var picked = m;
                int same = 0;
                for (int i = 0; i < GameSession.BIOME_MONSTER_SLOTS; i++)
                    if (biomeMonsters[biome * GameSession.BIOME_MONSTER_SLOTS + i] == m && biome * GameSession.BIOME_MONSTER_SLOTS + i != index) same++;
                options.Add(new Option
                {
                    label = NameOf(m),
                    icon = m.icon,
                    note = (same > 0 ? "×" + (same + 1) + "  " : "") + "HP " + m.hp + " / Atk " + m.atk,
                    pick = () => SetMonster(biome, index, picked),
                    hover = () => ShowMonster(picked),
                });
            }
            ShowPicker(BiomeLabels[biome] + "に置く魔物", options, near);
        }

        private void SetMonster(int biome, int index, MonsterData m)
        {
            biomeMonsters[index] = m;
            Status(BiomeLabels[biome] + ": " + (m != null ? NameOf(m) + "を置いた" : "枠を空けた"));
            RefreshAll();
        }

        // ---- 橋の番人 ----

        private void BuildGuardianPin(int i, Vector2 at)
        {
            bool on = guardianOn[i] && guardians.Count > 0;
            var pin = HudStyle.Place(marks, "Guardian_" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), at, new Vector2(58f, 58f));
            HudStyle.DropShadow(pin, HudShape.Kind.Circle, pal.Shadow, 0f, 3f);
            var body = HudStyle.Shape(pin, "Felt", HudShape.Kind.Circle, on ? HudStyle.Hex(0x5a5048) : Empty, 0f, HudStyle.FeltTexture);
            HudStyle.Stitch(pin, on ? pal.Light : new Color(pal.Stitch.r, pal.Stitch.g, pal.Stitch.b, 0.5f), true, 0f, 5f);
            if (on && guardians[0].icon != null) Face(pin, guardians[0].icon, 6f);
            else HudStyle.AddText(HudStyle.Filled(pin, "Glyph"), on ? "番" : "×", 26, on ? Color.white : new Color(1f, 1f, 1f, 0.45f), TextAnchor.MiddleCenter, 4f, pal.Outline);
            var tag = HudStyle.Place(marks, "GuardianTag_" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), at + new Vector2(0f, -32f), new Vector2(150f, 28f));
            HudStyle.Paper(tag, pal, HudShape.Kind.RoundRect, 6f, 3 + i, 2f);
            HudStyle.AddText(HudStyle.Filled(tag, "Text"), on ? "番人: " + NameOf(guardians[0]) : "番人なし", 15, pal.Ink, TextAnchor.MiddleCenter);
            if (guardians.Count == 0) return;
            LobbyStyle.MakeButton(pin, body, () =>
            {
                guardianOn[i] = !guardianOn[i];
                Status(BridgeLabels[i] + ": " + (guardianOn[i] ? NameOf(guardians[0]) + "を番人に置いた" : "番人を外した"));
                RefreshAll();
            });
            pin.gameObject.AddComponent<LobbyHover>().onEnter = () => ShowInfo(BridgeLabels[i] + "の番人",
                NameOf(guardians[0]) + "が橋のたもとに立ち、近づく勇者に両手を叩きつけて岩の槍を走らせる。押すと置く・外すを切り替える。");
        }

        // ---- フィールド魔法 ----

        private void BuildSpells()
        {
            LobbyStyle.Clear(spellList);
            for (int i = 0; i < equipped.Length; i++)
            {
                int index = i;
                var s = equipped[i];
                var row = LobbyStyle.At(spellList, "Spell_" + i, 0f, i * 132f, 460f, 120f);
                var bg = HudStyle.Paper(row, pal, HudShape.Kind.RoundRect, 10f, 20 + i, 4f);
                LobbyStyle.KeyCap(LobbyStyle.At(row, "Key", 12f, 12f, 40f, 40f), pal, (i + 1).ToString());
                if (s != null)
                {
                    LobbyStyle.IconToken(LobbyStyle.At(row, "Token", 62f, 14f, 84f, 84f), s.color, pal.Light, s.icon, s.glyph, 34, pal, 3f);
                    LobbyStyle.Text(row, "Name", 160f, 10f, 200f, 36f, s.spellName, 24, pal.Ink);
                    LobbyStyle.Text(row, "Cool", 300f, 12f, 146f, 32f, "CT " + s.cooldown.ToString("0") + "秒", 17, pal.Ribbon, TextAnchor.MiddleRight);
                    LobbyStyle.Paragraph(row, "Desc", 160f, 46f, 288f, 70f, Short(s.description), 15, pal.Ink);
                }
                else LobbyStyle.Text(row, "Empty", 160f, 40f, 280f, 40f, "（空き）押して選ぶ", 20, LobbyStyle.SubInk);
                LobbyStyle.MakeButton(row, bg, () => OpenSpellPicker(index, row)).GetComponent<LobbyPress>().hoverScale = 1.02f;
                var spell = s;
                row.gameObject.AddComponent<LobbyHover>().onEnter = () =>
                {
                    if (spell != null) ShowSpell(spell);
                };
            }
        }

        private static string Short(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            int cut = text.IndexOf('。');
            return cut > 0 && cut < 60 ? text.Substring(0, cut + 1) : (text.Length > 60 ? text.Substring(0, 60) + "…" : text);
        }

        private void OpenSpellPicker(int index, RectTransform near)
        {
            var options = new List<Option> { new Option { label = "外す", pick = () => SetSpell(index, null) } };
            foreach (var s in spells)
            {
                var picked = s;
                int other = Array.IndexOf(equipped, s);
                options.Add(new Option
                {
                    label = s.spellName,
                    icon = s.icon,
                    note = other == index ? "装備中" : other >= 0 ? (other + 1) + "番と入れ替え" : "CT " + s.cooldown.ToString("0") + "秒",
                    pick = () => SetSpell(index, picked),
                    hover = () => ShowSpell(picked),
                });
            }
            ShowPicker((index + 1) + "番のフィールド魔法", options, near);
        }

        private void SetSpell(int index, FieldSpellData s)
        {
            int other = s != null ? Array.IndexOf(equipped, s) : -1;
            if (other >= 0 && other != index) equipped[other] = equipped[index]; // 同じ魔法は2つ持てないので入れ替える
            equipped[index] = s;
            Status((index + 1) + "番: " + (s != null ? s.spellName : "外した"));
            RefreshAll();
        }

        // ---- 説明 ----

        private void Status(string text) => status.text = text;

        private void ShowInfo(string title, string body)
        {
            infoName.text = title;
            infoText.text = body;
        }

        private void ShowMonster(MonsterData m)
        {
            ShowInfo(NameOf(m), (string.IsNullOrEmpty(m.description) ? "" : m.description + "\n\n") +
                "HP " + m.hp + "　Atk " + m.atk + "　Df " + m.df + " / mDf " + m.mDf + "\nSpd " + m.spd.ToString("0.#") + "　1バイオームに最大 " + m.maxSpwn + " 体");
        }

        private void ShowSpell(FieldSpellData s)
        {
            ShowInfo(s.spellName + "（フィールド魔法）", s.description + "\n\nクールタイム " + s.cooldown.ToString("0") + "秒" +
                (s.needsTarget ? (s.targetsBiome ? "／バイオームを選ぶ" : "／場所を選ぶ") : "／マップ全体"));
        }

        // ---- 地図の線 ----

        private void Line(Vector2 a, Vector2 b, Color color, float width)
        {
            Vector2 d = b - a;
            var rt = HudStyle.Place(marks, "Line", new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), a, new Vector2(d.magnitude, width));
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            var img = HudStyle.AddGraphic<Image>(rt);
            img.color = color;
            img.raycastTarget = false;
            rt.SetAsFirstSibling();
        }

        // ---- 選択肢 ----

        /// near の横に選択肢の布を出す。外側を押すと閉じる。
        private void ShowPicker(string title, List<Option> options, RectTransform near)
        {
            LobbyStyle.Clear(pickerLayer);
            var dim = HudStyle.Filled(pickerLayer, "Backdrop");
            dim.anchorMin = new Vector2(-1f, -1f); dim.anchorMax = new Vector2(2f, 2f);
            var dimShape = HudStyle.Shape(dim, "Dim", HudShape.Kind.RoundRect, new Color(0f, 0f, 0f, 0.25f), 0f);
            var closeButton = dim.gameObject.AddComponent<Button>();
            closeButton.transition = Selectable.Transition.None;
            closeButton.targetGraphic = dimShape;
            dimShape.raycastTarget = true;
            closeButton.onClick.AddListener(() => LobbyStyle.Clear(pickerLayer));

            const float w = 330f;
            float h = 64f + options.Count * 48f;
            Vector3 world = near.TransformPoint(new Vector3(near.rect.xMax, near.rect.yMax, 0f));
            Vector2 local = pickerLayer.InverseTransformPoint(world);
            float x = Mathf.Clamp(local.x + 8f, 20f, LobbyStyle.StageWidth - w - 20f);
            float y = Mathf.Clamp(-local.y, 20f, LobbyStyle.StageHeight - h - 20f);
            var card = LobbyStyle.At(pickerLayer, "Picker", x, y, w, h);
            LobbyStyle.Ring(card, HudShape.Kind.RoundRect, 16f, 4f);
            var body = HudStyle.Felt(card, pal, HudShape.Kind.RoundRect, 16f, 0f, 6f);
            body.raycastTarget = true;
            LobbyStyle.Text(card, "Title", 16f, 12f, w - 30f, 40f, title, 20, pal.Light);
            for (int i = 0; i < options.Count; i++)
            {
                var o = options[i];
                var row = LobbyStyle.At(card, "Option_" + i, 12f, 56f + i * 48f, w - 24f, 42f);
                var bg = HudStyle.Shape(row, "Back", HudShape.Kind.RoundRect, new Color(pal.Light.r, pal.Light.g, pal.Light.b, 0.08f), 10f);
                var c = o.disabled ? new Color(pal.Light.r, pal.Light.g, pal.Light.b, 0.4f) : pal.Light;
                float nameX = 12f;
                if (o.icon != null)
                {
                    Face(LobbyStyle.At(row, "Icon", 6f, 3f, 36f, 36f), o.icon, 0f);
                    nameX = 50f;
                }
                LobbyStyle.Text(row, "Name", nameX, 0f, 150f, 42f, o.label, 19, c);
                LobbyStyle.Text(row, "Note", 120f, 0f, w - 156f, 42f, o.note ?? "", 14, c, TextAnchor.MiddleRight);
                if (o.disabled) continue;
                var pick = o.pick;
                var hover = o.hover;
                LobbyStyle.MakeButton(row, bg, () =>
                {
                    LobbyStyle.Clear(pickerLayer);
                    pick?.Invoke();
                }).GetComponent<LobbyPress>().hoverScale = 1.03f;
                var lh = row.gameObject.AddComponent<LobbyHover>();
                lh.onEnter = () => { bg.color = new Color(1f, 0.79f, 0.24f, 0.28f); hover?.Invoke(); };
                lh.onExit = () => bg.color = new Color(pal.Light.r, pal.Light.g, pal.Light.b, 0.08f);
            }
        }

        public bool PickerOpen => pickerLayer != null && pickerLayer.childCount > 0;

        public void ClosePicker() => LobbyStyle.Clear(pickerLayer);
    }

    /// マウスを乗せた・外したときに呼ぶ。
    public class LobbyHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Action onEnter;
        public Action onExit;
        public void OnPointerEnter(PointerEventData e) => onEnter?.Invoke();
        public void OnPointerExit(PointerEventData e) => onExit?.Invoke();
    }
}
