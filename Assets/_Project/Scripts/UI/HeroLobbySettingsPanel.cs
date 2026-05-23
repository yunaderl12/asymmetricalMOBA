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
        private Image currentClassFill;
        private Text currentClassNameText;
        private List<Image> carouselFills = new List<Image>();
        private List<Image> slotImages = new List<Image>();
        private List<Image> slotOutlines = new List<Image>();
        private List<Text> slotLabels = new List<Text>();
        private RectTransform availableGrid;
        private List<GameObject> availableButtons = new List<GameObject>();
        private float panelWidth = 720f;
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

            // Backdrop with click-to-close
            var bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.02f, 0.05f, 0.92f);
            bg.raycastTarget = true;

            // Title
            var titleGO = MakeText(root, "Title", font, 24, FontStyle.Bold, "設定");
            var tRT = (RectTransform)titleGO.transform;
            tRT.anchorMin = new Vector2(0f, 1f); tRT.anchorMax = new Vector2(1f, 1f); tRT.pivot = new Vector2(0.5f, 1f);
            tRT.sizeDelta = new Vector2(-32f, 40f); tRT.anchoredPosition = new Vector2(0f, -16f);
            titleGO.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            titleGO.GetComponent<Text>().color = new Color(1f, 0.85f, 0.4f, 1f);

            // Current class big oval (left)
            var ccGO = new GameObject("CurrentClass", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            ccGO.transform.SetParent(root, false);
            var ccRT = (RectTransform)ccGO.transform;
            ccRT.anchorMin = new Vector2(0f, 1f); ccRT.anchorMax = new Vector2(0f, 1f); ccRT.pivot = new Vector2(0f, 1f);
            ccRT.sizeDelta = new Vector2(170f, 230f);
            ccRT.anchoredPosition = new Vector2(24f, -72f);
            currentClassFill = ccGO.GetComponent<Image>();
            currentClassFill.color = new Color(1f, 0.15f, 0.15f, 1f);
            currentClassFill.raycastTarget = false;

            var cnGO = MakeText(root, "ClassName", font, 18, FontStyle.Bold, "Swordsman");
            var cnRT = (RectTransform)cnGO.transform;
            cnRT.anchorMin = new Vector2(0f, 1f); cnRT.anchorMax = new Vector2(0f, 1f); cnRT.pivot = new Vector2(0f, 1f);
            cnRT.sizeDelta = new Vector2(170f, 28f); cnRT.anchoredPosition = new Vector2(24f, -306f);
            currentClassNameText = cnGO.GetComponent<Text>();
            currentClassNameText.alignment = TextAnchor.MiddleCenter;
            currentClassNameText.color = Color.white;

            // Carousel header
            var carouselHGO = MakeText(root, "CarouselHeader", font, 14, FontStyle.Normal, "クラスを選択");
            var chRT = (RectTransform)carouselHGO.transform;
            chRT.anchorMin = new Vector2(0f, 1f); chRT.anchorMax = new Vector2(1f, 1f); chRT.pivot = new Vector2(0f, 1f);
            chRT.sizeDelta = new Vector2(-220f, 22f); chRT.anchoredPosition = new Vector2(210f, -76f);
            carouselHGO.GetComponent<Text>().alignment = TextAnchor.MiddleLeft;
            carouselHGO.GetComponent<Text>().color = new Color(0.85f, 0.85f, 0.85f, 1f);

            // Carousel container (right of CurrentClass)
            var carGO = new GameObject("Carousel", typeof(RectTransform));
            carGO.transform.SetParent(root, false);
            var carRT = (RectTransform)carGO.transform;
            carRT.anchorMin = new Vector2(0f, 1f); carRT.anchorMax = new Vector2(1f, 1f); carRT.pivot = new Vector2(0f, 1f);
            carRT.sizeDelta = new Vector2(-220f, 140f);
            carRT.anchoredPosition = new Vector2(210f, -106f);
            // Carousel buttons are created in PopulateCarousel(...)
            _carouselContainer = carRT;

            // Slot header
            var slotHGO = MakeText(root, "SlotHeader", font, 14, FontStyle.Normal, "スキルセット (5)");
            var sHRT = (RectTransform)slotHGO.transform;
            sHRT.anchorMin = new Vector2(0f, 1f); sHRT.anchorMax = new Vector2(1f, 1f); sHRT.pivot = new Vector2(0f, 1f);
            sHRT.sizeDelta = new Vector2(-32f, 22f); sHRT.anchoredPosition = new Vector2(16f, -322f);
            slotHGO.GetComponent<Text>().alignment = TextAnchor.MiddleLeft;
            slotHGO.GetComponent<Text>().color = new Color(0.85f, 0.85f, 0.85f, 1f);

            // Slot row (5 icons)
            float slotSize = 76f;
            float slotSpacing = 90f;
            for (int i = 0; i < 5; i++)
            {
                int captured = i;
                var sGO = new GameObject("Slot_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                sGO.transform.SetParent(root, false);
                var sRT = (RectTransform)sGO.transform;
                sRT.anchorMin = new Vector2(0f, 1f); sRT.anchorMax = new Vector2(0f, 1f); sRT.pivot = new Vector2(0f, 1f);
                sRT.sizeDelta = new Vector2(slotSize, slotSize);
                sRT.anchoredPosition = new Vector2(24f + i * slotSpacing, -356f);
                sRT.localRotation = Quaternion.Euler(0f, 0f, 45f);
                var sImg = sGO.GetComponent<Image>();
                sImg.color = new Color(0.5f, 0.5f, 0.5f, 1f);
                slotImages.Add(sImg);

                // Outline (slightly larger, white, initially disabled)
                var outGO = new GameObject("Outline", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                outGO.transform.SetParent(sGO.transform, false);
                var oRT = (RectTransform)outGO.transform;
                oRT.anchorMin = Vector2.zero; oRT.anchorMax = Vector2.one;
                oRT.offsetMin = new Vector2(-6f, -6f); oRT.offsetMax = new Vector2(6f, 6f);
                var outImg = outGO.GetComponent<Image>();
                outImg.color = new Color(1f, 1f, 1f, 1f); outImg.raycastTarget = false;
                outGO.SetActive(false);
                slotOutlines.Add(outImg);

                // Label (un-rotated overlay using a separate text on top)
                var lblGO = MakeText(root, "SlotLbl_" + i, font, 11, FontStyle.Bold, "");
                var lRT = (RectTransform)lblGO.transform;
                lRT.anchorMin = new Vector2(0f, 1f); lRT.anchorMax = new Vector2(0f, 1f); lRT.pivot = new Vector2(0.5f, 1f);
                lRT.sizeDelta = new Vector2(slotSize + 30f, 30f);
                lRT.anchoredPosition = new Vector2(24f + i * slotSpacing + slotSize * 0.5f, -440f);
                var lt = lblGO.GetComponent<Text>();
                lt.alignment = TextAnchor.UpperCenter; lt.color = Color.white;
                slotLabels.Add(lt);

                sGO.GetComponent<Button>().onClick.AddListener(() =>
                {
                    selectedSlot = captured;
                    RefreshOutlines();
                    OnSlotSelectedForReplace?.Invoke(captured);
                });
            }

            // Available header
            var avHGO = MakeText(root, "AvailHeader", font, 14, FontStyle.Normal, "他クラス + 共通スキル一覧 (クリックでセット枠と入替)");
            var ahRT = (RectTransform)avHGO.transform;
            ahRT.anchorMin = new Vector2(0f, 1f); ahRT.anchorMax = new Vector2(1f, 1f); ahRT.pivot = new Vector2(0f, 1f);
            ahRT.sizeDelta = new Vector2(-32f, 22f); ahRT.anchoredPosition = new Vector2(16f, -480f);
            avHGO.GetComponent<Text>().alignment = TextAnchor.MiddleLeft;
            avHGO.GetComponent<Text>().color = new Color(0.85f, 0.85f, 0.85f, 1f);

            // Available grid container
            var gridGO = new GameObject("AvailGrid", typeof(RectTransform));
            gridGO.transform.SetParent(root, false);
            availableGrid = (RectTransform)gridGO.transform;
            availableGrid.anchorMin = new Vector2(0f, 0f); availableGrid.anchorMax = new Vector2(1f, 1f); availableGrid.pivot = new Vector2(0f, 1f);
            availableGrid.offsetMin = new Vector2(16f, 16f); availableGrid.offsetMax = new Vector2(-16f, -512f);

            // Close hint
            var hintGO = MakeText(root, "Hint", font, 12, FontStyle.Italic, "Esc キーでパネルを閉じる");
            var hRT = (RectTransform)hintGO.transform;
            hRT.anchorMin = new Vector2(0f, 0f); hRT.anchorMax = new Vector2(1f, 0f); hRT.pivot = new Vector2(0.5f, 0f);
            hRT.sizeDelta = new Vector2(-16f, 20f); hRT.anchoredPosition = new Vector2(0f, 4f);
            hintGO.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            hintGO.GetComponent<Text>().color = new Color(0.6f, 0.6f, 0.6f, 1f);

            gameObject.SetActive(false);
        }

        private RectTransform _carouselContainer;

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
            float btnW = 90f;
            float gap = 14f;
            for (int i = 0; i < classes.Count; i++)
            {
                int captured = i;
                var c = classes[i];
                if (c == null) continue;
                var bGO = new GameObject("Cls_" + c.className, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                bGO.transform.SetParent(_carouselContainer, false);
                var rt = (RectTransform)bGO.transform;
                rt.anchorMin = new Vector2(0f, 0.5f); rt.anchorMax = new Vector2(0f, 0.5f); rt.pivot = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(btnW, btnW + 20f);
                rt.anchoredPosition = new Vector2(i * (btnW + gap), 0f);

                // Color fill
                var fGO = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                fGO.transform.SetParent(bGO.transform, false);
                var fRT = (RectTransform)fGO.transform;
                fRT.anchorMin = new Vector2(0.5f, 1f); fRT.anchorMax = new Vector2(0.5f, 1f); fRT.pivot = new Vector2(0.5f, 1f);
                fRT.sizeDelta = new Vector2(btnW, btnW);
                fRT.anchoredPosition = new Vector2(0f, 0f);
                var fImg = fGO.GetComponent<Image>();
                fImg.color = c.avatarColor; fImg.raycastTarget = false;
                carouselFills.Add(fImg);

                var nameGO = MakeText(bGO.transform, "Name", Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), 12, FontStyle.Bold, c.className);
                var nRT = (RectTransform)nameGO.transform;
                nRT.anchorMin = new Vector2(0.5f, 0f); nRT.anchorMax = new Vector2(0.5f, 0f); nRT.pivot = new Vector2(0.5f, 0f);
                nRT.sizeDelta = new Vector2(btnW + 20f, 18f); nRT.anchoredPosition = new Vector2(0f, 2f);
                nameGO.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

                var btnImg = bGO.GetComponent<Image>();
                btnImg.color = new Color(0f, 0f, 0f, 0.01f); // transparent hit area
                bGO.GetComponent<Button>().onClick.AddListener(() => OnClassSelected?.Invoke(captured));
            }
        }

        public void SetCurrentClass(HeroClassData cls)
        {
            if (cls == null) return;
            if (currentClassFill != null) currentClassFill.color = cls.avatarColor;
            if (currentClassNameText != null) currentClassNameText.text = cls.className;
        }

        public void SetSkillSet(SkillData[] skills)
        {
            for (int i = 0; i < slotImages.Count; i++)
            {
                SkillData s = (skills != null && i < skills.Length) ? skills[i] : null;
                if (slotImages[i] != null) slotImages[i].color = s != null ? s.EffectiveIconColor() : new Color(0.3f, 0.3f, 0.3f, 1f);
                if (slotLabels[i] != null) slotLabels[i].text = s != null ? s.skillName : "-";
            }
        }

        public void PopulateAvailable(List<SkillData> skills)
        {
            for (int i = availableButtons.Count - 1; i >= 0; i--) if (availableButtons[i] != null) Destroy(availableButtons[i]);
            availableButtons.Clear();
            if (availableGrid == null || skills == null) return;

            int columns = 5;
            float size = 64f;
            float gapX = 18f;
            float gapY = 36f;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            for (int i = 0; i < skills.Count; i++)
            {
                var s = skills[i];
                if (s == null) continue;
                int row = i / columns;
                int col = i % columns;
                var go = new GameObject("Skill_" + s.skillName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                go.transform.SetParent(availableGrid, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = new Vector2(col * (size + gapX), -(row * (size + gapY)));
                rt.localRotation = Quaternion.Euler(0f, 0f, 45f);
                go.GetComponent<Image>().color = s.EffectiveIconColor();
                var lblGO = MakeText(go.transform, "Lbl", font, 10, FontStyle.Bold, s.skillName);
                var lRT = (RectTransform)lblGO.transform;
                lRT.anchorMin = new Vector2(0.5f, 0f); lRT.anchorMax = new Vector2(0.5f, 0f); lRT.pivot = new Vector2(0.5f, 1f);
                lRT.sizeDelta = new Vector2(size + 24f, 22f); lRT.anchoredPosition = new Vector2(0f, 0f);
                lRT.localRotation = Quaternion.Euler(0f, 0f, -45f); // un-rotate label
                lblGO.GetComponent<Text>().alignment = TextAnchor.UpperCenter;
                var captured = s;
                go.GetComponent<Button>().onClick.AddListener(() => OnAvailableSkillPicked?.Invoke(captured));
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
            for (int i = 0; i < slotOutlines.Count; i++)
            {
                if (slotOutlines[i] != null) slotOutlines[i].gameObject.SetActive(i == selectedSlot);
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
