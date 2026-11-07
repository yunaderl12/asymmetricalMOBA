using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Game.Core;
using Game.Monster;

namespace Game.UI
{
    /// 魔王の編成。左に使える魔物、真ん中にデッキ20枠、右に塔5つの最初の魔物と橋の番人。
    /// 魔王ロビーとマッチロビー（魔王側）の両方で使う。変えた内容はすぐ GameSession に書く。
    public class DemonLoadoutView : MonoBehaviour
    {
        public static readonly string[] SpawnerKeys = { "NW", "NE", "Center", "SW", "SE" };
        public static readonly string[] BridgeLabels = { "山の橋", "砂漠の橋", "森の橋" };
        private const int StartDkLevel = 1;
        private static readonly Color[] MonsterColors =
        {
            HudStyle.Hex(0x5b3a8a), HudStyle.Hex(0x2f5e7a), HudStyle.Hex(0x7a5a1e), HudStyle.Hex(0x3f6b3a),
            HudStyle.Hex(0x7a2f4e), HudStyle.Hex(0x4a4f7a), HudStyle.Hex(0x6b4a2e), HudStyle.Hex(0x2f6b66),
        };
        // 地図の上の塔の位置（紙の左上から）。
        private static readonly Vector2[] PinPositions = { new Vector2(30f, 92f), new Vector2(286f, 92f), new Vector2(158f, 222f), new Vector2(30f, 356f), new Vector2(286f, 356f) };

        private HudStyle.Palette pal;
        private List<MonsterData> pool;
        private List<MonsterData> guardians;
        private int maxCost;

        private MonsterData[] deck;
        private MonsterData[] spawnerMobs;

        private RectTransform poolList;
        private Text infoName;
        private Text infoText;
        private RectTransform deckGrid;
        private Text deckCount;
        private Text status;
        private RectTransform map;
        private RectTransform bridgeList;
        private RectTransform pickerLayer;

        public static DemonLoadoutView Create(Transform stage, List<MonsterData> pool, List<MonsterData> guardians, int maxCost)
        {
            var rt = LobbyStyle.At(stage, "DemonLoadout", 0f, 0f, LobbyStyle.StageWidth, LobbyStyle.StageHeight);
            var view = rt.gameObject.AddComponent<DemonLoadoutView>();
            view.pool = pool.FindAll(m => m != null);
            view.guardians = guardians.FindAll(m => m != null);
            view.maxCost = maxCost;
            view.Load();
            view.Build(rt);
            return view;
        }

        private void Load()
        {
            var session = GameSession.GetOrCreate();
            deck = new MonsterData[GameSession.DECK_MAX];
            spawnerMobs = new MonsterData[GameSession.SPAWNER_COUNT];
            var seen = new HashSet<MonsterData>();
            int n = 0;
            foreach (var m in session.DemonDeck) if (m != null && n < deck.Length && seen.Add(m)) deck[n++] = m;
            for (int i = 0; i < spawnerMobs.Length && i < session.SpawnerInitialMobs.Count; i++) spawnerMobs[i] = session.SpawnerInitialMobs[i];
        }

        public void SaveToSession()
        {
            var session = GameSession.GetOrCreate();
            session.DemonDeck = new List<MonsterData>();
            foreach (var m in deck) if (m != null) session.DemonDeck.Add(m);
            session.SpawnerInitialMobs = new List<MonsterData>(spawnerMobs);
        }

        private void Build(RectTransform rt)
        {
            pal = HudStyle.DemonPalette;

            // 左: 使える魔物
            var left = LobbyStyle.At(rt, "Pool", 40f, 130f, 470f, 790f);
            LobbyStyle.Tilt(left, -0.5f);
            LobbyStyle.PaperSheet(left, pal, 6f, 1, 3, 6f);
            LobbyStyle.Text(left, "Header", 24f, 22f, 300f, 44f, "使える魔物", 30, pal.Ink);
            LobbyStyle.Text(left, "Hint", 210f, 26f, 236f, 36f, "押すとデッキに入れる", 17, LobbyStyle.SubInk, TextAnchor.MiddleRight);
            poolList = LobbyStyle.At(left, "List", 22f, 78f, 426f, 530f);
            LobbyStyle.DashedLine(left, 24f, 626f, 422f, LobbyStyle.Divider);
            infoName = LobbyStyle.Text(left, "InfoName", 24f, 640f, 420f, 34f, "", 22, pal.Ink);
            infoText = LobbyStyle.Paragraph(left, "InfoText", 24f, 676f, 422f, 104f, "", 18, pal.Ink);

            // 真ん中: デッキ
            LobbyStyle.Text(rt, "DeckHeader", 550f, 196f, 160f, 50f, "デッキ", 36, pal.Light, TextAnchor.MiddleLeft, 6f, pal.Outline);
            deckCount = LobbyStyle.Text(rt, "DeckCount", 690f, 200f, 160f, 44f, "", 28, pal.Light);
            LobbyStyle.Text(rt, "DeckHint", 860f, 202f, 440f, 40f, "枠を押すとデッキから外す", 18, new Color(pal.Light.r, pal.Light.g, pal.Light.b, 0.7f));
            deckGrid = LobbyStyle.At(rt, "DeckGrid", 550f, 256f, 760f, 520f);
            var statusRt = LobbyStyle.At(rt, "Status", 550f, 800f, 560f, 46f);
            LobbyStyle.Tilt(statusRt, -1f);
            HudStyle.Paper(statusRt, pal, HudShape.Kind.RoundRect, 4f, 8, 4f);
            status = HudStyle.AddText(HudStyle.Filled(statusRt, "Text"), "", 19, pal.Ink, TextAnchor.MiddleCenter);

            // 右: 最初の配置
            var mapPaper = LobbyStyle.At(rt, "Spawners", 1350f, 130f, 530f, 500f);
            LobbyStyle.Tilt(mapPaper, 0.6f);
            LobbyStyle.PaperSheet(mapPaper, pal, 6f, 2, 1, 6f);
            LobbyStyle.Text(mapPaper, "Header", 22f, 18f, 200f, 44f, "最初の配置", 28, pal.Ink);
            LobbyStyle.Text(mapPaper, "Hint", 200f, 22f, 310f, 36f, "塔ごとの最初の魔物（上限 Cost " + maxCost + "）", 16, LobbyStyle.SubInk, TextAnchor.MiddleRight);
            LobbyStyle.DashedLine(mapPaper, 40f, 280f, 450f, LobbyStyle.Divider);
            var vertical = LobbyStyle.At(mapPaper, "Road", 263f, 80f, 450f, 3f);
            LobbyStyle.Tilt(vertical, -90f);
            LobbyStyle.DashedLine(vertical, 0f, 0f, 400f, LobbyStyle.Divider);
            map = LobbyStyle.At(mapPaper, "Pins", 0f, 0f, 530f, 500f);

            // 右中: 橋の番人
            var bridges = LobbyStyle.At(rt, "Bridges", 1350f, 660f, 530f, 230f);
            LobbyStyle.Tilt(bridges, -0.6f);
            LobbyStyle.PaperSheet(bridges, pal, 6f, 1, 2, 6f);
            LobbyStyle.Text(bridges, "Header", 22f, 16f, 300f, 40f, "橋の番人", 26, pal.Ink);
            bridgeList = LobbyStyle.At(bridges, "List", 22f, 62f, 486f, 160f);

            // 選択肢（いちばん手前）
            pickerLayer = LobbyStyle.At(rt, "PickerLayer", 0f, 0f, LobbyStyle.StageWidth, LobbyStyle.StageHeight);

            Status("押すとデッキに入れる / デッキの枠を押すと外す");
            ShowInfo(pool.Count > 0 ? pool[0] : null);
            RefreshAll();
        }

        private void RefreshAll()
        {
            BuildPool();
            BuildDeck();
            BuildPins();
            BuildBridges();
            SaveToSession();
        }

        private Color ColorOf(MonsterData m)
        {
            int i = pool.IndexOf(m);
            if (i < 0) i = Mathf.Abs(m.monsterName.GetHashCode());
            return MonsterColors[i % MonsterColors.Length];
        }

        private static string GlyphOf(MonsterData m)
        {
            switch (m.monsterName)
            {
                case "BasicMob": return "歩";
                case "TankMob": return "盾";
                case "FlyingMob": return "翼";
                default: return string.IsNullOrEmpty(m.monsterName) ? "?" : m.monsterName.Substring(0, 1);
            }
        }

        private bool InDeck(MonsterData m) => Array.IndexOf(deck, m) >= 0;

        private int DeckCount()
        {
            int n = 0;
            foreach (var m in deck) if (m != null) n++;
            return n;
        }

        private void Status(string text) => status.text = text;

        private void ShowInfo(MonsterData m)
        {
            infoName.text = m != null ? m.monsterName : "";
            infoText.text = m == null ? "" : string.IsNullOrEmpty(m.description) ? "（説明なし）" : m.description;
        }

        // ---- 使える魔物 ----

        private void BuildPool()
        {
            LobbyStyle.Clear(poolList);
            for (int i = 0; i < pool.Count; i++)
            {
                var m = pool[i];
                bool inDeck = InDeck(m);
                var row = LobbyStyle.At(poolList, "Avail_" + m.name, 0f, i * 102f, 426f, 92f);
                var bg = HudStyle.Shape(row, "Back", HudShape.Kind.RoundRect, inDeck ? new Color(1f, 0.95f, 0.77f, 1f) : new Color(0.1f, 0.07f, 0.08f, 0.08f), 14f);
                if (inDeck) LobbyStyle.Ring(row, HudShape.Kind.RoundRect, 14f, 4f);
                LobbyStyle.FeltToken(LobbyStyle.At(row, "Token", 10f, 9f, 74f, 74f), ColorOf(m), pal.Light, GlyphOf(m), 30, pal, 3f);
                LobbyStyle.Text(row, "Name", 98f, 8f, 200f, 32f, m.monsterName, 22, pal.Ink);
                var pill = LobbyStyle.At(row, "Cost", 98f + m.monsterName.Length * 15f + 14f, 13f, 76f, 24f);
                HudStyle.Shape(pill, "Pill", HudShape.Kind.RoundRect, HudStyle.CostPill, 8f);
                HudStyle.AddText(HudStyle.Filled(pill, "Text"), "Cost " + m.cost, 15, HudStyle.CostText, TextAnchor.MiddleCenter);
                LobbyStyle.Text(row, "Stats", 98f, 40f, 320f, 26f, "HP " + m.hp + "　Atk " + m.atk + "　Spd " + m.spd.ToString("0.#"), 17, pal.Ink);
                string note = !m.IsUnlockedAt(StartDkLevel) ? "魔王Lv" + m.unlockDkLevel + "で出せるようになる" : inDeck ? "デッキに入っている" : "";
                LobbyStyle.Text(row, "Note", 98f, 64f, 320f, 24f, note, 15, pal.Ribbon);
                LobbyStyle.MakeButton(row, bg, () => OnPoolClicked(m)).GetComponent<LobbyPress>().hoverScale = 1.02f;
                row.gameObject.AddComponent<LobbyHover>().onEnter = () => ShowInfo(m);
            }
        }

        private void OnPoolClicked(MonsterData m)
        {
            ShowInfo(m);
            int at = Array.IndexOf(deck, m);
            if (at >= 0)
            {
                Remove(at);
                return;
            }
            int free = Array.IndexOf(deck, null);
            if (free < 0)
            {
                Status("デッキがいっぱいで入れられない: " + m.monsterName);
                return;
            }
            deck[free] = m;
            Status("入れた: " + m.monsterName + "（" + (free + 1) + "枠目）");
            RefreshAll();
        }

        // ---- デッキ ----

        private void BuildDeck()
        {
            LobbyStyle.Clear(deckGrid);
            deckCount.text = DeckCount() + " / " + deck.Length;
            for (int i = 0; i < deck.Length; i++)
            {
                int index = i;
                var m = deck[i];
                var cell = LobbyStyle.At(deckGrid, "Slot_" + i, (i % 5) * 152f, (i / 5) * 130f, 140f, 118f);
                var cellPal = pal;
                cellPal.Felt = m != null ? ColorOf(m) : HudStyle.Hex(0x120a0e);
                cellPal.Stitch = m != null ? pal.Light : new Color(pal.Stitch.r, pal.Stitch.g, pal.Stitch.b, 0.6f);
                var body = HudStyle.Felt(cell, cellPal, HudShape.Kind.RoundRect, 14f, 5f, m != null ? 5f : 0f);
                LobbyStyle.Text(cell, "Num", 12f, 6f, 40f, 20f, (i + 1).ToString(), 14, new Color(pal.Light.r, pal.Light.g, pal.Light.b, 0.55f));
                if (m == null) continue;
                LobbyStyle.Text(cell, "Glyph", 0f, 14f, 140f, 54f, GlyphOf(m), 32, Color.white, TextAnchor.MiddleCenter, 5f, pal.Outline);
                LobbyStyle.Text(cell, "Name", 0f, 72f, 140f, 30f, m.monsterName, 16, pal.Light, TextAnchor.MiddleCenter);
                LobbyStyle.MakeButton(cell, body, () => Remove(index));
                cell.gameObject.AddComponent<LobbyHover>().onEnter = () => ShowInfo(m);
            }
        }

        private void Remove(int index)
        {
            var m = deck[index];
            if (m == null) return;
            deck[index] = null;
            // 前に詰める
            var rest = new List<MonsterData>();
            foreach (var d in deck) if (d != null) rest.Add(d);
            for (int i = 0; i < deck.Length; i++) deck[i] = i < rest.Count ? rest[i] : null;
            // デッキから外した魔物は最初の配置からも外す
            for (int i = 0; i < spawnerMobs.Length; i++) if (spawnerMobs[i] == m) spawnerMobs[i] = null;
            Status("外した: " + m.monsterName);
            RefreshAll();
        }

        // ---- 最初の配置 ----

        private void BuildPins()
        {
            LobbyStyle.Clear(map);
            for (int i = 0; i < spawnerMobs.Length && i < PinPositions.Length; i++)
            {
                int index = i;
                var m = spawnerMobs[i];
                var pos = PinPositions[i];
                var pinRt = LobbyStyle.At(map, "Spawn_" + SpawnerKeys[i], pos.x, pos.y, 214f, 64f);
                var pin = LobbyStyle.At(pinRt, "Pin", 0f, 10f, 44f, 44f);
                HudStyle.DropShadow(pin, HudShape.Kind.Circle, pal.Shadow, 0f, 3f);
                HudStyle.Shape(pin, "Felt", HudShape.Kind.Circle, pal.Stitch, 0f, HudStyle.FeltTexture);
                HudStyle.Shape(LobbyStyle.AtCenter(pin, "Dot", 22f, 22f, 14f, 14f), "Dot", HudShape.Kind.Circle, pal.Light);
                var tag = LobbyStyle.At(pinRt, "Tag", 52f, 0f, 162f, 64f);
                var body = HudStyle.Felt(tag, pal, HudShape.Kind.RoundRect, 10f, 0f, 3f);
                LobbyStyle.Text(tag, "Name", 12f, 4f, 150f, 28f, HudStyle.TowerLabel(SpawnerKeys[i]) + " ▼", 18, pal.Light);
                string mob = m != null ? m.monsterName + " " + m.cost + "/" + maxCost : "（空）0/" + maxCost;
                LobbyStyle.Text(tag, "Mob", 12f, 32f, 150f, 26f, mob, 15, pal.Light);
                LobbyStyle.MakeButton(pinRt, body, () => OpenSpawnerPicker(index, pinRt));
            }
        }

        private void OpenSpawnerPicker(int index, RectTransform near)
        {
            var candidates = new List<MonsterData>();
            foreach (var m in deck) if (m != null && !candidates.Contains(m)) candidates.Add(m);
            ShowPicker(HudStyle.TowerLabel(SpawnerKeys[index]) + "の最初の魔物", "（空）", candidates, true, near, picked =>
            {
                spawnerMobs[index] = picked;
                Status(HudStyle.TowerLabel(SpawnerKeys[index]) + ": " + (picked != null ? picked.monsterName : "（空）"));
                RefreshAll();
            });
        }

        // ---- 橋の番人 ----

        private void BuildBridges()
        {
            LobbyStyle.Clear(bridgeList);
            var session = GameSession.GetOrCreate();
            for (int i = 0; i < BridgeLabels.Length && i < session.BridgeGuardians.Length; i++)
            {
                int index = i;
                var m = session.BridgeGuardians[i];
                var row = LobbyStyle.At(bridgeList, "Bridge_" + i, 0f, i * 50f, 486f, 42f);
                var bg = HudStyle.Shape(row, "Back", HudShape.Kind.RoundRect, new Color(0.1f, 0.07f, 0.08f, 0.08f), 10f);
                LobbyStyle.Text(row, "Name", 14f, 0f, 200f, 42f, BridgeLabels[i], 21, pal.Ink);
                LobbyStyle.Text(row, "Mob", 200f, 0f, 272f, 42f, (m != null ? m.monsterName : "（なし）") + " ▼", 20,
                    m != null ? pal.Ribbon : LobbyStyle.SubInk, TextAnchor.MiddleRight);
                LobbyStyle.MakeButton(row, bg, () => OpenBridgePicker(index, row)).GetComponent<LobbyPress>().hoverScale = 1.02f;
            }
        }

        private void OpenBridgePicker(int index, RectTransform near)
        {
            ShowPicker(BridgeLabels[index] + "の番人", "（なし）", guardians, false, near, picked =>
            {
                GameSession.GetOrCreate().BridgeGuardians[index] = picked;
                Status(BridgeLabels[index] + ": " + (picked != null ? picked.monsterName : "（なし）"));
                RefreshAll();
            });
        }

        // ---- 選択肢 ----

        /// near の横に選択肢の布を出す。外側を押すと閉じる。checkCost のときは Cost の上限と解禁の魔王Lvも見る。
        private void ShowPicker(string title, string emptyLabel, List<MonsterData> candidates, bool checkCost, RectTransform near, Action<MonsterData> onPicked)
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

            var options = new List<MonsterData> { null };
            options.AddRange(candidates);
            float h = 64f + options.Count * 46f;
            // near の右下あたりに出す。画面からはみ出さないように寄せる。
            Vector3 world = near.TransformPoint(new Vector3(near.rect.xMax, near.rect.yMax, 0f));
            Vector2 local = pickerLayer.InverseTransformPoint(world);
            float x = Mathf.Clamp(local.x + 8f, 20f, LobbyStyle.StageWidth - 320f);
            float y = Mathf.Clamp(-local.y, 20f, LobbyStyle.StageHeight - h - 20f);
            var card = LobbyStyle.At(pickerLayer, "Picker", x, y, 300f, h);
            LobbyStyle.Ring(card, HudShape.Kind.RoundRect, 16f, 4f);
            var body = HudStyle.Felt(card, pal, HudShape.Kind.RoundRect, 16f, 0f, 6f);
            body.raycastTarget = true;
            LobbyStyle.Text(card, "Title", 16f, 12f, 270f, 40f, title, 20, pal.Light);
            for (int i = 0; i < options.Count; i++)
            {
                var m = options[i];
                bool overCost = checkCost && m != null && maxCost > 0 && m.cost > maxCost;
                bool locked = checkCost && m != null && !m.IsUnlockedAt(StartDkLevel);
                bool disabled = overCost || locked;
                var row = LobbyStyle.At(card, "Cand_" + (m != null ? m.name : "Empty"), 12f, 56f + i * 46f, 276f, 40f);
                var bg = HudStyle.Shape(row, "Back", HudShape.Kind.RoundRect, new Color(pal.Light.r, pal.Light.g, pal.Light.b, 0.08f), 10f);
                var c = disabled ? new Color(pal.Light.r, pal.Light.g, pal.Light.b, 0.4f) : pal.Light;
                LobbyStyle.Text(row, "Name", 12f, 0f, 160f, 40f, m != null ? m.monsterName : emptyLabel, 18, c);
                string right = m == null ? "" : locked ? "魔王Lv" + m.unlockDkLevel + "で解禁" : overCost ? "Cost " + m.cost + " 上限超" : "Cost " + m.cost;
                LobbyStyle.Text(row, "Cost", 120f, 0f, 146f, 40f, right, 15, c, TextAnchor.MiddleRight);
                if (disabled) continue;
                var picked = m;
                LobbyStyle.MakeButton(row, bg, () =>
                {
                    LobbyStyle.Clear(pickerLayer);
                    onPicked(picked);
                }).GetComponent<LobbyPress>().hoverScale = 1.03f;
                row.gameObject.AddComponent<LobbyHover>().onEnter = () => bg.color = new Color(1f, 0.79f, 0.24f, 0.28f);
                row.gameObject.GetComponent<LobbyHover>().onExit = () => bg.color = new Color(pal.Light.r, pal.Light.g, pal.Light.b, 0.08f);
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
