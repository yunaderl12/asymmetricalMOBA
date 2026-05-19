using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Skill;

namespace Game.UI
{
    public class SkillPickerUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform gridContent;
        [SerializeField] private Button cancelButton;
        [SerializeField] private GameObject candidateButtonPrefab;
        [SerializeField] private Sprite buttonSprite;
        [SerializeField] private int columns = 4;
        [SerializeField] private Vector2 cellSize = new Vector2(170f, 96f);
        [SerializeField] private Vector2 cellGap = new Vector2(12f, 12f);

        private Action<SkillData> currentCallback;
        private readonly List<GameObject> spawnedButtons = new List<GameObject>();

        private void Awake()
        {
            if (root != null) root.SetActive(false);
        }

        private void Start()
        {
            if (cancelButton != null) cancelButton.onClick.AddListener(Hide);
        }

        public void Show(List<SkillData> candidates, Action<SkillData> onPicked)
        {
            currentCallback = onPicked;
            ClearButtons();
            if (candidates != null) BuildButtons(candidates);
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

        private void BuildButtons(List<SkillData> candidates)
        {
            if (gridContent == null) return;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            for (int i = 0; i < candidates.Count; i++)
            {
                var s = candidates[i];
                if (s == null) continue;
                int row = i / columns;
                int col = i % columns;

                var go = new GameObject("Cand_" + s.skillName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                go.transform.SetParent(gridContent, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = cellSize;
                rt.anchoredPosition = new Vector2(col * (cellSize.x + cellGap.x), -(row * (cellSize.y + cellGap.y)));
                var img = go.GetComponent<Image>();
                if (buttonSprite != null) { img.sprite = buttonSprite; img.type = Image.Type.Sliced; }
                img.color = new Color(0.2f, 0.2f, 0.28f, 0.95f);

                var nameGO = new GameObject("Name", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                nameGO.transform.SetParent(go.transform, false);
                var nrt = nameGO.GetComponent<RectTransform>();
                nrt.anchorMin = new Vector2(0f, 0.5f); nrt.anchorMax = new Vector2(1f, 1f);
                nrt.offsetMin = Vector2.zero; nrt.offsetMax = Vector2.zero;
                var nt = nameGO.GetComponent<Text>();
                nt.font = font; nt.fontSize = 18; nt.fontStyle = FontStyle.Bold; nt.alignment = TextAnchor.MiddleCenter; nt.color = Color.white;
                nt.text = s.skillName;

                var costGO = new GameObject("Cost", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                costGO.transform.SetParent(go.transform, false);
                var crt = costGO.GetComponent<RectTransform>();
                crt.anchorMin = new Vector2(0f, 0f); crt.anchorMax = new Vector2(1f, 0.5f);
                crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
                var ct = costGO.GetComponent<Text>();
                ct.font = font; ct.fontSize = 13; ct.alignment = TextAnchor.MiddleCenter; ct.color = new Color(0.75f, 0.85f, 1f, 1f);
                ct.text = $"SP {s.spCost}  CD {s.cooldown:F0}s";

                var btn = go.GetComponent<Button>();
                SkillData captured = s;
                btn.onClick.AddListener(() => OnCandidateClicked(captured));

                spawnedButtons.Add(go);
            }

            // Resize gridContent to fit rows
            int rowsCount = (candidates.Count + columns - 1) / columns;
            float height = rowsCount * cellSize.y + (rowsCount - 1) * cellGap.y;
            float width = columns * cellSize.x + (columns - 1) * cellGap.x;
            gridContent.sizeDelta = new Vector2(width, height);
        }

        private void OnCandidateClicked(SkillData s)
        {
            var cb = currentCallback;
            Hide();
            cb?.Invoke(s);
        }
    }
}
