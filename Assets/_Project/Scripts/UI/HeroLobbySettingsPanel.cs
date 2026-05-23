using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Hero;
using Game.Skill;

namespace Game.UI
{
    public class HeroLobbySettingsPanel : MonoBehaviour
    {
        public bool IsOpen { get; private set; }

        public Action<int> OnClassSelected;
        public Action<int> OnSlotSelectedForReplace;
        public Action<SkillData> OnAvailableSkillPicked;

        private RectTransform root;
        private EllipseGraphic currentClassFill;
        private Text currentClassNameText;
        private List<EllipseGraphic> carouselFills = new List<EllipseGraphic>();
        private List<PentagonGraphic> slotGraphics = new List<PentagonGraphic>();
        private List<Text> slotLabels = new List<Text>();
        private List<Button> slotButtons = new List<Button>();
        private RectTransform availableGrid;
        private List<GameObject> availableButtons = new List<GameObject>();
        private float panelWidth = 960f;
        private Coroutine slideRoutine;
        private int selectedSlot = -1;

        public static HeroLobbySettingsPanel Create(RectTransform parent)
        {
            var go = new GameObject("SettingsPanel", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<HeroLobbySettingsPanel>();
            p.Build();
            return p;
        }

        private void Build()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            root = (RectTransform)transform;
            root.anchorMin = new Vector2(1f, 0f); root.anchorMax = new Vector2(1f, 1f); root.pivot = new Vector2(0f, 0.5f);
            root.sizeDelta = new Vector2(panelWidth, 0f);
            root.anchoredPosition = new Vector2(panelWidth, 0f); // off-screen initially

            // Backdrop
            var bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.82f);
            bg.raycastTarget = true;

            // Title
            var titleGO = MakeText(root, "Title", font, 22, FontStyle.Bold, "設定");
            var tRT = (RectTransform)titleGO.transform;
            tRT.anchorMin = new Vector2(0f, 1f); tRT.anchorMax = new Vector2(1f, 1f); tRT.pivot = new Vector2(0.5f, 1f);
            tRT.sizeDelta = new Vector2(-32f, 36f); tRT.anchoredPosition = new Vector2(0f, -10f);
            titleGO.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            titleGO.GetComponent<Text>().color = new Color(1f, 0.85f, 0.4f, 1f);

            // Current class big oval (left, 160x220 at (40,-40) anchor (0,1)) — EllipseGraphic
            var ccGO = new GameObject("CurrentClass", typeof(RectTransform), typeof(CanvasRenderer));
            ccGO.transform.SetParent(root, false);
            var ccRT = (RectTransform)ccGO.transform;
            ccRT.anchorMin = new Vector2(0f, 1f); ccRT.anchorMax = new Vector2(0f, 1f); ccRT.pivot = new Vector2(0f, 1f);
            ccRT.sizeDelta = new Vector2(160f, 220f);
            ccRT.anchoredPosition = new Vector2(40f, -40f);
            currentClassFill = ccGO.AddComponent<EllipseGraphic>();
            currentClassFill.color = new Color(1f, 0.15f, 0.15f, 1f);
            currentClassFill.raycastTarget = false;

            // Current class name below big oval
            var cnGO = MakeText(root, "CurClassName", font, 18, FontStyle.Bold, "Swordsman");
            var cnRT = (RectTransform)cnGO.transform;
            cnRT.anchorMin = new Vector2(0f, 1f); cnRT.anchorMax = new Vector2(0f, 1f); cnRT.pivot = new Vector2(0f, 1f);
            cnRT.sizeDelta = new Vector2(160f, 26f); cnRT.anchoredPosition = new Vector2(40f, -266f);
            currentClassNameText = cnGO.GetComponent<Text>();
            currentClassNameText.alignment = TextAnchor.MiddleCenter;
            currentClassNameText.color = Color.white;

            // Carousel container: anchor (0,1) pos (220,-80) — small ovals 80x80
            _carouselContainer = new GameObject("Carousel", typeof(RectTransform)).GetComponent<RectTransform>();
            _carouselContainer.SetParent(root, false);
            _carouselContainer.anchorMin = new Vector2(0f, 1f); _carouselContainer.anchorMax = new Vector2(0f, 1f); _carouselContainer.pivot = new Vector2(0f, 1f);
            _carouselContainer.sizeDelta = new Vector2(560f, 120f);
            _carouselContainer.anchoredPosition = new Vector2(220f, -80f);

            // Skill set row (5 pentagons) anchor (0.5,1) pos (0,-300), size 56 each
            float pentSize = 56f;
            float pentSpacing = 72f;
            float pentStartX = -(pentSpacing * 2f); // -144 ... +144 (5 elements)
            for (int i = 0; i < 5; i++)
            {
                int captured = i;
                var sGO = new GameObject("Slot_" + i, typeof(RectTransform), typeof(CanvasRenderer));
                sGO.transform.SetParent(root, false);
                var sRT = (RectTransform)sGO.transform;
                sRT.anchorMin = new Vector2(0.5f, 1f); sRT.anchorMax = new Vector2(0.5f, 1f); sRT.pivot = new Vector2(0.5f, 0.5f);
                sRT.sizeDelta = new Vector2(pentSize, pentSize);
                sRT.anchoredPosition = new Vector2(pentStartX + i * pentSpacing, -300f);

                // Pentagon graphic (acts as Image / Raycast target for the Button)
                var pg = sGO.AddComponent<PentagonGraphic>();
                pg.color = new Color(0.5f, 0.5f, 0.5f, 1f);
                pg.raycastTarget = true;
                slotGraphics.Add(pg);

                // Outline (slightly larger pentagon behind, white, initially disabled)
                var outGO = new GameObject("Outline", typeof(RectTransform), typeof(CanvasRenderer));
                outGO.transform.SetParent(sGO.transform, false);
                var oRT = (RectTransform)outGO.transform;
                oRT.anchorMin = Vector2.zero; oRT.anchorMax = Vector2.one;
                oRT.offsetMin = new Vector2(-6f, -6f); oRT.offsetMax = new Vector2(6f, 6f);
                var outG = outGO.AddComponent<PentagonGraphic>();
                outG.color = Color.white; outG.raycastTarget = false;
                outGO.transform.SetSiblingIndex(0); // render behind the slot pentagon
                outGO.SetActive(false);
                outlineHosts.Add(outGO);

                // Label below
                var lblGO = MakeText(root, "SlotLbl_" + i, font, 11, FontStyle.Bold, "");
                var lRT = (RectTransform)lblGO.transform;
                lRT.anchorMin = new Vector2(0.5f, 1f); lRT.anchorMax = new Vector2(0.5f, 1f); lRT.pivot = new Vector2(0.5f, 1f);
                lRT.sizeDelta = new Vector2(pentSize + 32f, 24f);
                lRT.anchoredPosition = new Vector2(pentStartX + i * pentSpacing, -300f - pentSize / 2f - 4f);
                var lt = lblGO.GetComponent<Text>();
                lt.alignment = TextAnchor.UpperCenter; lt.color = Color.white;
                slotLabels.Add(lt);

                var btn = sGO.AddComponent<Button>();
                btn.targetGraphic = pg;
                btn.interactable = (i == 4); // only slot 5 is interactive
                slotButtons.Add(btn);
                btn.onClick.AddListener(() =>
                {
                    selectedSlot = captured;
                    RefreshOutlines();
                    OnSlotSelectedForReplace?.Invoke(captured);
                });
            }

            // Divider line
            var divGO = new GameObject("Divider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            divGO.transform.SetParent(root, false);
            var dvRT = (RectTransform)divGO.transform;
            dvRT.anchorMin = new Vector2(0f, 1f); dvRT.anchorMax = new Vector2(1f, 1f); dvRT.pivot = new Vector2(0.5f, 0.5f);
            dvRT.sizeDelta = new Vector2(-32f, 2f); dvRT.anchoredPosition = new Vector2(0f, -390f);
            divGO.GetComponent<Image>().color = new Color(0.4f, 0.4f, 0.45f, 0.7f);

            // Available header
            var avHGO = MakeText(root, "AvailHeader", font, 14, FontStyle.Normal, "他クラス + 共通スキル (クリックでスロット 5 と入替)");
            var ahRT = (RectTransform)avHGO.transform;
            ahRT.anchorMin = new Vector2(0f, 1f); ahRT.anchorMax = new Vector2(1f, 1f); ahRT.pivot = new Vector2(0f, 1f);
            ahRT.sizeDelta = new Vector2(-32f, 22f); ahRT.anchoredPosition = new Vector2(16f, -405f);
            avHGO.GetComponent<Text>().alignment = TextAnchor.MiddleLeft;
            avHGO.GetComponent<Text>().color = new Color(0.85f, 0.85f, 0.85f, 1f);

            // Available grid
            var gridGO = new GameObject("AvailGrid", typeof(RectTransform));
            gridGO.transform.SetParent(root, false);
            availableGrid = (RectTransform)gridGO.transform;
            availableGrid.anchorMin = new Vector2(0.5f, 1f); availableGrid.anchorMax = new Vector2(0.5f, 1f); availableGrid.pivot = new Vector2(0.5f, 1f);
            availableGrid.sizeDelta = new Vector2(560f, 360f);
            availableGrid.anchoredPosition = new Vector2(0f, -460f);

            // Close hint at bottom
            var hintGO = MakeText(root, "Hint", font, 12, FontStyle.Italic, "Esc キーでパネルを閉じる");
            var hRT = (RectTransform)hintGO.transform;
            hRT.anchorMin = new Vector2(0f, 0f); hRT.anchorMax = new Vector2(1f, 0f); hRT.pivot = new Vector2(0.5f, 0f);
            hRT.sizeDelta = new Vector2(-16f, 20f); hRT.anchoredPosition = new Vector2(0f, 6f);
            hintGO.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            hintGO.GetComponent<Text>().color = new Color(0.6f, 0.6f, 0.6f, 1f);

            gameObject.SetActive(false);
        }

        private RectTransform _carouselContainer;
        private readonly List<GameObject> outlineHosts = new List<GameObject>();

        private static GameObject MakeText(Transform parent, string name, Font font, int size, FontStyle style, string text)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font; t.fontSize = size; t.fontStyle = style; t.color = Color.white;
            t.raycastTarget = false;
            t.text = text;
            return go;
        }

        public void PopulateCarousel(IList<HeroClassData> classes, int currentIdx)
        {
            if (_carouselContainer == null) return;
            for (int i = _carouselContainer.childCount - 1; i >= 0; i--) Destroy(_carouselContainer.GetChild(i).gameObject);
            carouselFills.Clear();
            if (classes == null) return;
            float btnSize = 80f;
            float gap = 16f;
            for (int i = 0; i < classes.Count; i++)
            {
                int captured = i;
                var c = classes[i];
                if (c == null) continue;
                var bGO = new GameObject("Cls_" + c.className, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                bGO.transform.SetParent(_carouselContainer, false);
                var rt = (RectTransform)bGO.transform;
                rt.anchorMin = new Vector2(0f, 0.5f); rt.anchorMax = new Vector2(0f, 0.5f); rt.pivot = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(btnSize, btnSize + 22f);
                rt.anchoredPosition = new Vector2(i * (btnSize + gap), 0f);
                bGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f); // transparent hit area

                var fGO = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer));
                fGO.transform.SetParent(bGO.transform, false);
                var fRT = (RectTransform)fGO.transform;
                fRT.anchorMin = new Vector2(0.5f, 1f); fRT.anchorMax = new Vector2(0.5f, 1f); fRT.pivot = new Vector2(0.5f, 1f);
                fRT.sizeDelta = new Vector2(btnSize, btnSize);
                fRT.anchoredPosition = new Vector2(0f, 0f);
                var fEll = fGO.AddComponent<EllipseGraphic>();
                fEll.color = c.avatarColor; fEll.raycastTarget = false;
                carouselFills.Add(fEll);

                var nameGO = MakeText(bGO.transform, "Name", Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), 12, FontStyle.Bold, c.className);
                var nRT = (RectTransform)nameGO.transform;
                nRT.anchorMin = new Vector2(0.5f, 0f); nRT.anchorMax = new Vector2(0.5f, 0f); nRT.pivot = new Vector2(0.5f, 0f);
                nRT.sizeDelta = new Vector2(btnSize + 24f, 20f); nRT.anchoredPosition = new Vector2(0f, 0f);
                nameGO.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

                bGO.GetComponent<Button>().onClick.AddListener(() => OnClassSelected?.Invoke(captured));
            }
        }

        public void SetCurrentClass(HeroClassData cls)
        {
            if (cls == null) return;
            if (currentClassFill != null) currentClassFill.color = cls.avatarColor;
            if (currentClassNameText != null) currentClassNameText.text = cls.className;
        }

        /// <summary>
        /// Slot 0..3 use currentClassColor (locked). Slot 4 uses skill.EffectiveIconColor.
        /// Locked slots are darkened by 0.7×.
        /// </summary>
        public void SetSkillSet(SkillData[] skills, Color currentClassColor)
        {
            for (int i = 0; i < slotGraphics.Count; i++)
            {
                SkillData s = (skills != null && i < skills.Length) ? skills[i] : null;
                if (slotGraphics[i] != null)
                {
                    if (i < 4) slotGraphics[i].color = currentClassColor * new Color(0.7f, 0.7f, 0.7f, 1f) + new Color(0f, 0f, 0f, 0.3f);
                    else slotGraphics[i].color = s != null ? s.EffectiveIconColor() : new Color(0.4f, 0.4f, 0.4f, 1f);
                }
                if (slotLabels[i] != null) slotLabels[i].text = s != null ? s.skillName : "-";
            }
        }

        public void PopulateAvailable(List<SkillData> skills)
        {
            for (int i = availableButtons.Count - 1; i >= 0; i--) if (availableButtons[i] != null) Destroy(availableButtons[i]);
            availableButtons.Clear();
            if (availableGrid == null || skills == null) return;

            int columns = 5;
            float size = 56f;
            float gapX = 24f;
            float gapY = 40f;
            // Grid pivot is top-center; start each row centered.
            float rowWidth = columns * size + (columns - 1) * gapX;
            float startX = -rowWidth * 0.5f + size * 0.5f;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            for (int i = 0; i < skills.Count; i++)
            {
                var s = skills[i];
                if (s == null) continue;
                int row = i / columns;
                int col = i % columns;
                var go = new GameObject("Skill_" + s.skillName, typeof(RectTransform), typeof(CanvasRenderer));
                go.transform.SetParent(availableGrid, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = new Vector2(0.5f, 1f); rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = new Vector2(startX + col * (size + gapX), -(row * (size + gapY) + size * 0.5f + 6f));

                var pg = go.AddComponent<PentagonGraphic>();
                pg.color = s.EffectiveIconColor();
                pg.raycastTarget = true;

                var lblGO = MakeText(go.transform, "Lbl", font, 10, FontStyle.Bold, s.skillName);
                var lRT = (RectTransform)lblGO.transform;
                lRT.anchorMin = new Vector2(0.5f, 0f); lRT.anchorMax = new Vector2(0.5f, 0f); lRT.pivot = new Vector2(0.5f, 1f);
                lRT.sizeDelta = new Vector2(size + 24f, 22f); lRT.anchoredPosition = new Vector2(0f, -2f);
                lblGO.GetComponent<Text>().alignment = TextAnchor.UpperCenter;

                var btn = go.AddComponent<Button>();
                btn.targetGraphic = pg;
                var captured = s;
                btn.onClick.AddListener(() => OnAvailableSkillPicked?.Invoke(captured));
                availableButtons.Add(go);
            }
        }

        public void ClearSelectedSlot()
        {
            selectedSlot = -1;
            RefreshOutlines();
        }

        private void RefreshOutlines()
        {
            for (int i = 0; i < outlineHosts.Count; i++)
            {
                if (outlineHosts[i] != null) outlineHosts[i].SetActive(i == selectedSlot);
            }
        }

        public void Show()
        {
            if (IsOpen) return;
            IsOpen = true;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            if (slideRoutine != null) StopCoroutine(slideRoutine);
            slideRoutine = StartCoroutine(SlideTo(0f, 0.18f));
        }

        public void Hide()
        {
            if (!IsOpen) return;
            IsOpen = false;
            ClearSelectedSlot();
            if (slideRoutine != null) StopCoroutine(slideRoutine);
            slideRoutine = StartCoroutine(SlideToAndHide(panelWidth, 0.16f));
        }

        private IEnumerator SlideTo(float targetX, float duration)
        {
            float t = 0f;
            float start = root.anchoredPosition.x;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / duration);
                float eased = 1f - Mathf.Pow(1f - u, 3f);
                root.anchoredPosition = new Vector2(Mathf.Lerp(start, targetX, eased), 0f);
                yield return null;
            }
            root.anchoredPosition = new Vector2(targetX, 0f);
            slideRoutine = null;
        }

        private IEnumerator SlideToAndHide(float targetX, float duration)
        {
            yield return SlideTo(targetX, duration);
            gameObject.SetActive(false);
        }
    }
}
