using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Game.Monster;

namespace Game.UI
{
    public class DemonMonsterPickerUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform gridContent;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Text titleText;
        [SerializeField] private Sprite buttonSprite;
        [SerializeField] private int columns = 3;
        [SerializeField] private Vector2 cellSize = new Vector2(200f, 70f);
        [SerializeField] private Vector2 cellGap = new Vector2(12f, 12f);

        private Action<MonsterData> currentCallback;
        private readonly List<GameObject> spawnedButtons = new List<GameObject>();

        private void Awake()
        {
            if (root != null) root.SetActive(false);
        }

        private void Start()
        {
            if (cancelButton != null) cancelButton.onClick.AddListener(Hide);
        }

        public void Show(string title, List<MonsterData> candidates, bool includeEmptyOption, Action<MonsterData> onPicked)
        {
            Show(title, candidates, includeEmptyOption, -1, onPicked);
        }

        public void Show(string title, List<MonsterData> candidates, bool includeEmptyOption, int maxCost, Action<MonsterData> onPicked)
        {
            Debug.Log($"[DemonMonsterPickerUI] Show title='{title}', candidates={(candidates != null ? candidates.Count : -1)}, maxCost={maxCost}, root={(root != null ? "ok" : "NULL")}");
            EnsureHighPriorityCanvas();
            currentCallback = onPicked;
            if (titleText != null)
            {
                titleText.text = maxCost > 0 ? title + "  (上限 Cost " + maxCost + ")" : title;
            }
            ClearButtons();
            BuildButtons(candidates, includeEmptyOption, maxCost);
            if (root != null)
            {
                if (_clickOutsideBackdrop != null) _clickOutsideBackdrop.SetActive(true);
                root.SetActive(true);
                root.transform.SetAsLastSibling();
                Debug.Log($"[DemonMonsterPickerUI] root active={root.activeSelf}, parent={(root.transform.parent != null ? root.transform.parent.name : "ROOT")}");
            }
            else
            {
                Debug.LogError("[DemonMonsterPickerUI] root field is not assigned in the inspector");
            }
        }

        // Reparents `root` to a dedicated ScreenSpaceOverlay canvas at sortingOrder 11
        // so the picker draws above MainStage's BirdsEyeMap (order 7) and the
        // SpawnerActionPopupCanvas (order 10). Also injects a fullscreen
        // transparent click-outside backdrop that calls Hide.
        private static Canvas _highPriorityCanvas;
        private bool _reparented;
        private GameObject _clickOutsideBackdrop;
        private void EnsureHighPriorityCanvas()
        {
            if (_reparented || root == null) return;
            if (_highPriorityCanvas == null)
            {
                var existing = GameObject.Find("DemonMonsterPickerCanvas");
                if (existing != null) _highPriorityCanvas = existing.GetComponent<Canvas>();
            }
            if (_highPriorityCanvas == null)
            {
                var go = new GameObject("DemonMonsterPickerCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                _highPriorityCanvas = go.GetComponent<Canvas>();
                _highPriorityCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _highPriorityCanvas.sortingOrder = 11;
                var scaler = go.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }
            // Capture the original local pose so reparenting doesn't accidentally
            // re-anchor the panel.
            var rt = root.GetComponent<RectTransform>();
            var anchorMin = rt != null ? rt.anchorMin : Vector2.zero;
            var anchorMax = rt != null ? rt.anchorMax : Vector2.one;
            var pivot = rt != null ? rt.pivot : new Vector2(0.5f, 0.5f);
            var sizeDelta = rt != null ? rt.sizeDelta : Vector2.zero;
            var anchoredPos = rt != null ? rt.anchoredPosition : Vector2.zero;

            // Click-outside backdrop -> sibling BEFORE root, so root paints over it.
            _clickOutsideBackdrop = new GameObject("ClickOutsideBackdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            _clickOutsideBackdrop.transform.SetParent(_highPriorityCanvas.transform, false);
            var bRT = (RectTransform)_clickOutsideBackdrop.transform;
            bRT.anchorMin = Vector2.zero; bRT.anchorMax = Vector2.one;
            bRT.offsetMin = Vector2.zero; bRT.offsetMax = Vector2.zero;
            var bImg = _clickOutsideBackdrop.GetComponent<Image>();
            bImg.color = new Color(0f, 0f, 0f, 0.0f); // fully transparent
            bImg.raycastTarget = true;
            var bBtn = _clickOutsideBackdrop.GetComponent<Button>();
            bBtn.targetGraphic = bImg;
            bBtn.transition = Selectable.Transition.None;
            bBtn.onClick.AddListener(Hide);

            root.transform.SetParent(_highPriorityCanvas.transform, false);
            if (rt != null)
            {
                rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
                rt.sizeDelta = sizeDelta; rt.anchoredPosition = anchoredPos;
            }
            root.transform.SetAsLastSibling(); // root above backdrop
            Debug.Log($"[DemonMonsterPickerUI] reparented root to {_highPriorityCanvas.name} (sortingOrder={_highPriorityCanvas.sortingOrder})");
            _reparented = true;
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
            if (_clickOutsideBackdrop != null) _clickOutsideBackdrop.SetActive(false);
            currentCallback = null;
        }

        private void Update()
        {
            if (root == null || !root.activeSelf) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Hide();
        }

        private void ClearButtons()
        {
            foreach (var b in spawnedButtons) if (b != null) Destroy(b);
            spawnedButtons.Clear();
        }

        private void BuildButtons(List<MonsterData> candidates, bool includeEmptyOption, int maxCost)
        {
            if (gridContent == null) return;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var combined = new List<MonsterData>();
            if (includeEmptyOption) combined.Add(null);
            if (candidates != null) foreach (var c in candidates) if (c != null) combined.Add(c);

            for (int i = 0; i < combined.Count; i++)
            {
                var md = combined[i];
                int row = i / columns;
                int col = i % columns;
                bool overCost = (md != null && maxCost > 0 && md.cost > maxCost);

                var go = new GameObject("Cand_" + (md != null ? md.name : "Empty"), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                go.transform.SetParent(gridContent, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = cellSize;
                rt.anchoredPosition = new Vector2(col * (cellSize.x + cellGap.x), -(row * (cellSize.y + cellGap.y)));
                var img = go.GetComponent<Image>();
                if (buttonSprite != null) { img.sprite = buttonSprite; img.type = Image.Type.Sliced; }
                if (overCost) img.color = new Color(0.18f, 0.18f, 0.2f, 0.6f);
                else img.color = md != null ? new Color(0.3f, 0.15f, 0.18f, 0.95f) : new Color(0.2f, 0.2f, 0.22f, 0.95f);

                var lblGO = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                lblGO.transform.SetParent(go.transform, false);
                var lr = lblGO.GetComponent<RectTransform>();
                lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
                lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
                var lt = lblGO.GetComponent<Text>();
                lt.font = font; lt.fontSize = 20; lt.fontStyle = FontStyle.Bold; lt.alignment = TextAnchor.MiddleCenter;
                lt.color = overCost ? new Color(0.7f, 0.7f, 0.7f, 1f) : Color.white;
                if (md != null)
                {
                    string costStr = "Cost " + md.cost;
                    if (overCost) costStr += " (上限超)";
                    lt.text = md.monsterName + "\n" + costStr;
                }
                else lt.text = "(空)";

                var btn = go.GetComponent<Button>();
                btn.interactable = !overCost;
                MonsterData captured = md;
                btn.onClick.AddListener(() => OnCandidateClicked(captured));
                spawnedButtons.Add(go);
            }

            int rowsCount = (combined.Count + columns - 1) / columns;
            float height = rowsCount * cellSize.y + (rowsCount - 1) * cellGap.y;
            float width = columns * cellSize.x + (columns - 1) * cellGap.x;
            gridContent.sizeDelta = new Vector2(width, height);
        }

        private void OnCandidateClicked(MonsterData md)
        {
            var cb = currentCallback;
            Hide();
            cb?.Invoke(md);
        }
    }
}
