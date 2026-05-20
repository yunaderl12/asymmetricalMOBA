using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;
using Game.DemonKing;
using Game.Monster;

namespace Game.UI
{
    public class BirdsEyeControlUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform listParent;
        [SerializeField] private DemonMonsterPickerUI pickerUI;
        [SerializeField] private Text statusText;
        [SerializeField] private Sprite buttonSprite;

        private DemonKingViewSwitcher viewSwitcher;
        private readonly List<Spawner> spawners = new List<Spawner>();
        private readonly List<Text> rowTexts = new List<Text>();

        private void Awake()
        {
            if (root != null) root.SetActive(false);
        }

        private void Start()
        {
            var dk = GameObject.FindWithTag("DemonKing");
            if (dk != null) viewSwitcher = dk.GetComponent<DemonKingViewSwitcher>();
            if (viewSwitcher != null) viewSwitcher.OnViewChanged += OnViewChanged;

            CollectSpawners();
            BuildRows();
        }

        private void OnDestroy()
        {
            if (viewSwitcher != null) viewSwitcher.OnViewChanged -= OnViewChanged;
        }

        private void OnViewChanged(bool birdsEye)
        {
            if (root != null) root.SetActive(birdsEye);
            TogglePlayerVisibility(!birdsEye);
            Cursor.lockState = birdsEye ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = birdsEye;
            if (birdsEye) Refresh();
        }

        private void TogglePlayerVisibility(bool visible)
        {
            foreach (var p in GameObject.FindGameObjectsWithTag("Player"))
            {
                if (p == null) continue;
                int n = p.transform.childCount;
                for (int i = 0; i < n; i++)
                {
                    var c = p.transform.GetChild(i);
                    if (c.name != "Body" && c.name != "FloatingUI") continue;
                    c.gameObject.SetActive(visible);
                }
            }
        }

        private void CollectSpawners()
        {
            spawners.Clear();
            foreach (var go in GameObject.FindGameObjectsWithTag("Spawner"))
            {
                var sp = go.GetComponent<Spawner>();
                if (sp != null) spawners.Add(sp);
            }
        }

        private void BuildRows()
        {
            if (listParent == null) return;
            for (int i = listParent.childCount - 1; i >= 0; i--) Destroy(listParent.GetChild(i).gameObject);
            rowTexts.Clear();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            for (int i = 0; i < spawners.Count; i++)
            {
                int idx = i;
                var sp = spawners[i];

                var row = new GameObject("Row_" + sp.name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                row.transform.SetParent(listParent, false);
                var rt = row.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(0f, 90f);
                rt.anchoredPosition = new Vector2(0f, -i * 100f);
                var img = row.GetComponent<Image>(); if (buttonSprite != null) { img.sprite = buttonSprite; img.type = Image.Type.Sliced; }
                img.color = new Color(0.18f, 0.05f, 0.08f, 0.92f);

                var infoGO = new GameObject("Info", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                infoGO.transform.SetParent(row.transform, false);
                var ir = infoGO.GetComponent<RectTransform>();
                ir.anchorMin = new Vector2(0f, 0f); ir.anchorMax = new Vector2(0.7f, 1f);
                ir.offsetMin = new Vector2(12f, 4f); ir.offsetMax = new Vector2(-4f, -4f);
                var it = infoGO.GetComponent<Text>();
                it.font = font; it.fontSize = 16; it.alignment = TextAnchor.MiddleLeft; it.color = Color.white;
                it.text = "";
                rowTexts.Add(it);

                var btnGO = new GameObject("AddButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                btnGO.transform.SetParent(row.transform, false);
                var br = btnGO.GetComponent<RectTransform>();
                br.anchorMin = new Vector2(0.7f, 0.15f); br.anchorMax = new Vector2(1f, 0.85f);
                br.offsetMin = new Vector2(4f, 0f); br.offsetMax = new Vector2(-12f, 0f);
                var bImg = btnGO.GetComponent<Image>(); if (buttonSprite != null) { bImg.sprite = buttonSprite; bImg.type = Image.Type.Sliced; }
                bImg.color = new Color(0.85f, 0.45f, 0.15f, 0.95f);
                var bL = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                bL.transform.SetParent(btnGO.transform, false);
                var bLR = bL.GetComponent<RectTransform>();
                bLR.anchorMin = Vector2.zero; bLR.anchorMax = Vector2.one; bLR.offsetMin = Vector2.zero; bLR.offsetMax = Vector2.zero;
                var bLT = bL.GetComponent<Text>(); bLT.font = font; bLT.fontSize = 22; bLT.fontStyle = FontStyle.Bold; bLT.alignment = TextAnchor.MiddleCenter; bLT.color = Color.white;
                bLT.text = "+追加";

                var btn = btnGO.GetComponent<Button>();
                btn.onClick.AddListener(() => OpenAddPicker(idx));
            }
        }

        private void Update()
        {
            if (root == null || !root.activeSelf) return;
            Refresh();
        }

        private void Refresh()
        {
            for (int i = 0; i < spawners.Count && i < rowTexts.Count; i++)
            {
                var sp = spawners[i];
                if (sp == null || rowTexts[i] == null) continue;
                var hc = sp.GetComponent<Game.Combat.HealthComponent>();
                var lvl = sp.GetComponent<LevelComponent>();
                string hpStr = hc != null ? hc.CurrentHP + "/" + hc.MaxHP : "?/?";
                int lv = lvl != null ? lvl.CurrentLevel : 1;
                string invStr = "(空)";
                if (sp.Inv != null && sp.Inv.Count > 0)
                {
                    var dict = new Dictionary<string, int>();
                    foreach (var m in sp.Inv) { if (m == null) continue; if (!dict.ContainsKey(m.monsterName)) dict[m.monsterName] = 0; dict[m.monsterName]++; }
                    var parts = new List<string>();
                    foreach (var kv in dict) parts.Add(kv.Key + "×" + kv.Value);
                    invStr = string.Join(", ", parts);
                }
                rowTexts[i].text = sp.name + "  HP " + hpStr + "  Lv " + lv + "  Cost " + sp.CurrentCost + "/" + sp.MaxCost + "\n   inv: " + invStr;
            }
        }

        private void OpenAddPicker(int spawnerIdx)
        {
            if (pickerUI == null) return;
            if (spawnerIdx < 0 || spawnerIdx >= spawners.Count) return;
            var sp = spawners[spawnerIdx];

            var deckMobs = new List<MonsterData>();
            if (GameSession.Instance != null && GameSession.Instance.DemonDeck != null)
            {
                foreach (var m in GameSession.Instance.DemonDeck)
                {
                    if (m != null && !deckMobs.Contains(m)) deckMobs.Add(m);
                }
            }
            pickerUI.Show(sp.name + " に魔物を追加", deckMobs, false, picked => OnPicked(sp, picked));
        }

        private void OnPicked(Spawner sp, MonsterData m)
        {
            if (m == null) return;
            if (sp.AddToInv(m))
            {
                if (statusText != null) statusText.text = "追加: " + m.monsterName;
            }
            else
            {
                if (statusText != null) statusText.text = "コスト不足: " + m.monsterName + " を追加できない";
            }
            Refresh();
        }
    }
}
