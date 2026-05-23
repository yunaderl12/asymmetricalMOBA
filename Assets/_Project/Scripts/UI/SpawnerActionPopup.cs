using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Game.Core;
using Game.DemonKing;
using Game.Monster;

namespace Game.UI
{
    public class SpawnerActionPopup : MonoBehaviour
    {
        public static SpawnerActionPopup Instance { get; private set; }

        private Game.Monster.Spawner currentSpawner;
        private DemonKingViewSwitcher currentViewSwitcher;
        private GameObject backdropGO;
        private RectTransform panelRT;
        private Text titleText;
        private Text statusText;

        public static SpawnerActionPopup GetOrCreate()
        {
            if (Instance != null) return Instance;
            var existing = FindObjectOfType<SpawnerActionPopup>(true);
            if (existing != null) { Instance = existing; return existing; }

            // Use a dedicated overlay canvas at sortingOrder 10 so the popup is
            // guaranteed to render above BirdsEyeMapCanvas (order 7) and any
            // other in-scene canvas. Without this it was being created on the
            // first found Canvas (often the MainStage HUD canvas at default
            // sortingOrder 0) and the BirdsEyeMap's opaque backdrop hid it.
            var canvasGOName = "SpawnerActionPopupCanvas";
            var canvasGO = GameObject.Find(canvasGOName);
            Canvas canvas;
            if (canvasGO != null) canvas = canvasGO.GetComponent<Canvas>();
            else
            {
                canvasGO = new GameObject(canvasGOName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasGO.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 10;
                var scaler = canvasGO.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }
            var go = new GameObject("SpawnerActionPopup", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var popup = go.AddComponent<SpawnerActionPopup>();
            popup.Build();
            go.SetActive(false);
            return popup;
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Build()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Backdrop (full-screen, click closes)
            backdropGO = new GameObject("Backdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            backdropGO.transform.SetParent(transform, false);
            var bRT = backdropGO.GetComponent<RectTransform>();
            bRT.anchorMin = Vector2.zero; bRT.anchorMax = Vector2.one;
            bRT.offsetMin = Vector2.zero; bRT.offsetMax = Vector2.zero;
            var bImg = backdropGO.GetComponent<Image>();
            bImg.color = new Color(0f, 0f, 0f, 0.55f);
            backdropGO.GetComponent<Button>().onClick.AddListener(Hide);

            // Panel
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(transform, false);
            panelRT = panel.GetComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 0.5f); panelRT.anchorMax = new Vector2(0.5f, 0.5f); panelRT.pivot = new Vector2(0.5f, 0.5f);
            panelRT.sizeDelta = new Vector2(360f, 360f);
            panelRT.anchoredPosition = Vector2.zero;
            var pImg = panel.GetComponent<Image>();
            pImg.color = new Color(0.1f, 0.05f, 0.08f, 0.97f);

            // Title
            var titleGO = MakeText(panel.transform, "Title", font, 22, FontStyle.Bold);
            var tRT = titleGO.GetComponent<RectTransform>();
            tRT.anchorMin = new Vector2(0f, 1f); tRT.anchorMax = new Vector2(1f, 1f); tRT.pivot = new Vector2(0.5f, 1f);
            tRT.sizeDelta = new Vector2(-20f, 40f); tRT.anchoredPosition = new Vector2(0f, -12f);
            titleText = titleGO.GetComponent<Text>();
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(1f, 0.85f, 0.4f, 1f);
            titleText.text = "Spawner";

            // Status (HP/Lv/Cost)
            var statusGO = MakeText(panel.transform, "Status", font, 16, FontStyle.Normal);
            var sRT = statusGO.GetComponent<RectTransform>();
            sRT.anchorMin = new Vector2(0f, 1f); sRT.anchorMax = new Vector2(1f, 1f); sRT.pivot = new Vector2(0.5f, 1f);
            sRT.sizeDelta = new Vector2(-20f, 60f); sRT.anchoredPosition = new Vector2(0f, -60f);
            statusText = statusGO.GetComponent<Text>();
            statusText.alignment = TextAnchor.MiddleCenter;
            statusText.color = Color.white;
            statusText.text = "";

            // Buttons (vertical)
            CreateButton(panel.transform, font, "戦況確認", new Vector2(0f, -150f), OnInspectClicked);
            CreateButton(panel.transform, font, "編成", new Vector2(0f, -220f), OnLoadoutClicked);
            CreateButton(panel.transform, font, "閉じる", new Vector2(0f, -290f), Hide);
        }

        private static GameObject MakeText(Transform parent, string name, Font font, int size, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font; t.fontSize = size; t.fontStyle = style; t.color = Color.white;
            t.raycastTarget = false;
            return go;
        }

        private void CreateButton(Transform parent, Font font, string label, Vector2 anchoredPos, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f); rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(220f, 56f);
            rt.anchoredPosition = anchoredPos;
            var img = go.GetComponent<Image>();
            img.color = new Color(0.4f, 0.18f, 0.22f, 0.95f);

            var lblGO = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            lblGO.transform.SetParent(go.transform, false);
            var lRT = lblGO.GetComponent<RectTransform>();
            lRT.anchorMin = Vector2.zero; lRT.anchorMax = Vector2.one; lRT.offsetMin = Vector2.zero; lRT.offsetMax = Vector2.zero;
            var lt = lblGO.GetComponent<Text>();
            lt.font = font; lt.fontSize = 20; lt.fontStyle = FontStyle.Bold; lt.alignment = TextAnchor.MiddleCenter; lt.color = Color.white;
            lt.text = label; lt.raycastTarget = false;

            go.GetComponent<Button>().onClick.AddListener(onClick);
        }

        public void Show(Game.Monster.Spawner sp, DemonKingViewSwitcher viewSwitcher)
        {
            if (sp == null) { Debug.LogWarning("[SpawnerActionPopup] Show called with null spawner"); return; }
            currentSpawner = sp;
            currentViewSwitcher = viewSwitcher;
            if (titleText != null) titleText.text = sp.name;
            UpdateStatus();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            var canvas = GetComponentInParent<Canvas>();
            Debug.Log($"[SpawnerActionPopup] Show {sp.name}; canvas={(canvas != null ? canvas.name : "NULL")} sortingOrder={(canvas != null ? canvas.sortingOrder : -1)}");
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            currentSpawner = null;
            currentViewSwitcher = null;
        }

        private void Update()
        {
            if (!gameObject.activeSelf) return;
            UpdateStatus();
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Hide();
        }

        private void UpdateStatus()
        {
            if (statusText == null || currentSpawner == null) return;
            var hc = currentSpawner.GetComponent<Game.Combat.HealthComponent>();
            var lc = currentSpawner.GetComponent<LevelComponent>();
            string hp = hc != null ? hc.CurrentHP + "/" + hc.MaxHP : "?/?";
            int lv = lc != null ? lc.CurrentLevel : 1;
            string inv = "(空)";
            if (currentSpawner.Inv != null && currentSpawner.Inv.Count > 0)
            {
                var dict = new Dictionary<string, int>();
                foreach (var m in currentSpawner.Inv) { if (m == null) continue; if (!dict.ContainsKey(m.monsterName)) dict[m.monsterName] = 0; dict[m.monsterName]++; }
                var parts = new List<string>();
                foreach (var kv in dict) parts.Add(kv.Key + "×" + kv.Value);
                inv = string.Join(", ", parts);
            }
            statusText.text = $"HP {hp}    Lv {lv}    Cost {currentSpawner.CurrentCost}/{currentSpawner.MaxCost}\ninv: {inv}";
        }

        private void OnInspectClicked()
        {
            if (currentViewSwitcher != null && currentSpawner != null)
            {
                currentViewSwitcher.EnterSpawnerOverhead(currentSpawner);
            }
            Hide();
        }

        private void OnLoadoutClicked()
        {
            if (currentSpawner == null) { Hide(); return; }
            var picker = FindObjectOfType<DemonMonsterPickerUI>(true);
            if (picker == null) { Debug.LogWarning("[SpawnerActionPopup] DemonMonsterPickerUI not found in scene"); return; }

            var deckMobs = new List<MonsterData>();
            if (GameSession.Instance != null && GameSession.Instance.DemonDeck != null)
            {
                foreach (var m in GameSession.Instance.DemonDeck)
                {
                    if (m != null && !deckMobs.Contains(m)) deckMobs.Add(m);
                }
            }
            int remaining = Mathf.Max(0, currentSpawner.MaxCost - currentSpawner.CurrentCost);
            var sp = currentSpawner;
            picker.Show(sp.name + "  cost " + sp.CurrentCost + "/" + sp.MaxCost + " (残 " + remaining + ")", deckMobs, false, remaining, picked => OnPickerResult(sp, picked));
            // Keep popup OPEN so the user can see live inv/cost while picking.
        }

        private void OnPickerResult(Game.Monster.Spawner sp, MonsterData picked)
        {
            if (sp == null || picked == null) return;
            if (sp.AddToInv(picked)) Debug.Log("[SpawnerActionPopup] added " + picked.monsterName + " to " + sp.name);
            else Debug.Log("[SpawnerActionPopup] cost exceeded for " + picked.monsterName);
            // Refresh the popup's status text immediately and reopen the picker so
            // the player can keep adding monsters until they hit the cost cap.
            UpdateStatus();
            if (currentSpawner != null)
            {
                var picker = FindObjectOfType<DemonMonsterPickerUI>(true);
                if (picker != null && currentSpawner.MaxCost - currentSpawner.CurrentCost > 0)
                {
                    var deckMobs = new List<MonsterData>();
                    if (GameSession.Instance != null && GameSession.Instance.DemonDeck != null)
                    {
                        foreach (var m in GameSession.Instance.DemonDeck)
                            if (m != null && !deckMobs.Contains(m)) deckMobs.Add(m);
                    }
                    int rem = Mathf.Max(0, currentSpawner.MaxCost - currentSpawner.CurrentCost);
                    var sp2 = currentSpawner;
                    picker.Show(sp2.name + "  cost " + sp2.CurrentCost + "/" + sp2.MaxCost + " (残 " + rem + ")", deckMobs, false, rem, p => OnPickerResult(sp2, p));
                }
            }
        }
    }
}
