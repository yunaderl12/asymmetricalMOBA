using System;
using System.Collections.Generic;
using UnityEngine;
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
            currentCallback = onPicked;
            if (titleText != null) titleText.text = title;
            ClearButtons();
            BuildButtons(candidates, includeEmptyOption);
            if (root != null) root.SetActive(true);
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
            currentCallback = null;
        }

        private void ClearButtons()
        {
            foreach (var b in spawnedButtons) if (b != null) Destroy(b);
            spawnedButtons.Clear();
        }

        private void BuildButtons(List<MonsterData> candidates, bool includeEmptyOption)
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

                var go = new GameObject("Cand_" + (md != null ? md.name : "Empty"), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                go.transform.SetParent(gridContent, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = cellSize;
                rt.anchoredPosition = new Vector2(col * (cellSize.x + cellGap.x), -(row * (cellSize.y + cellGap.y)));
                var img = go.GetComponent<Image>();
                if (buttonSprite != null) { img.sprite = buttonSprite; img.type = Image.Type.Sliced; }
                img.color = md != null ? new Color(0.3f, 0.15f, 0.18f, 0.95f) : new Color(0.2f, 0.2f, 0.22f, 0.95f);

                var lblGO = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                lblGO.transform.SetParent(go.transform, false);
                var lr = lblGO.GetComponent<RectTransform>();
                lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
                lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
                var lt = lblGO.GetComponent<Text>();
                lt.font = font; lt.fontSize = 22; lt.fontStyle = FontStyle.Bold; lt.alignment = TextAnchor.MiddleCenter; lt.color = Color.white;
                lt.text = md != null ? md.monsterName : "(空)";

                var btn = go.GetComponent<Button>();
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
