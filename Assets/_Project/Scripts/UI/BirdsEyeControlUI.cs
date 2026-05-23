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
            if (viewSwitcher != null) viewSwitcher.OnModeChanged += OnModeChanged;

            CollectSpawners();
            BuildRows();
        }

        private void OnDestroy()
        {
            if (viewSwitcher != null) viewSwitcher.OnModeChanged -= OnModeChanged;
        }

        private void OnModeChanged(DemonKingViewSwitcher.ViewMode mode)
        {
            bool show = mode == DemonKingViewSwitcher.ViewMode.BirdsEye;
            if (root != null) root.SetActive(show);
            TogglePlayerVisibility(mode == DemonKingViewSwitcher.ViewMode.Tps);
            Cursor.lockState = (mode == DemonKingViewSwitcher.ViewMode.Tps) ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = mode != DemonKingViewSwitcher.ViewMode.Tps;
            if (show) Refresh();
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
                var sp = spawners[i];

                var row = new GameObject("Row_" + sp.name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                row.transform.SetParent(listParent, false);
                var rt = row.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(0f, 72f);
                rt.anchoredPosition = new Vector2(0f, -i * 80f);
                var img = row.GetComponent<Image>(); if (buttonSprite != null) { img.sprite = buttonSprite; img.type = Image.Type.Sliced; }
                img.color = new Color(0.18f, 0.05f, 0.08f, 0.92f);
                img.raycastTarget = false;

                var infoGO = new GameObject("Info", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                infoGO.transform.SetParent(row.transform, false);
                var ir = infoGO.GetComponent<RectTransform>();
                ir.anchorMin = new Vector2(0f, 0f); ir.anchorMax = new Vector2(1f, 1f);
                ir.offsetMin = new Vector2(10f, 4f); ir.offsetMax = new Vector2(-10f, -4f);
                var it = infoGO.GetComponent<Text>();
                it.font = font; it.fontSize = 15; it.alignment = TextAnchor.MiddleLeft; it.color = Color.white;
                it.raycastTarget = false;
                it.text = "";
                rowTexts.Add(it);
            }

            if (statusText != null) statusText.text = "スポナーを左クリック → アクション選択";
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
    }
}
