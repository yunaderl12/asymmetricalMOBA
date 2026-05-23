using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Game.Core;
using Game.Hero;
using Game.Skill;

namespace Game.UI
{
    public class HeroLobby : MonoBehaviour
    {
        [Header("Classes / Skills (Inspector)")]
        [SerializeField] private List<HeroClassData> allClasses = new List<HeroClassData>();
        [SerializeField] private List<SkillData> commonSkills = new List<SkillData>();

        [Header("Scenes")]
        [SerializeField] private string matchSceneName = "MatchLobby";
        [SerializeField] private string titleSceneName = "Title";

        [Header("Player")]
        [SerializeField] private string userName = "ユーザー名";

        private int selectedClassIndex = 0;
        private readonly SkillData[] currentSkills = new SkillData[5];

        private HeroLobbyAvatarView avatarView;
        private HeroLobbySettingsPanel settingsPanel;
        private GameObject overlayRoot;
        private Text titleText;
        private Button roleButton;
        private Button settingsButton;
        private Button startButton;
        private int selectedSlot = -1;

        private void Awake()
        {
            GameSession.GetOrCreate();
            PopulateSkillOwners();
        }

        private void Start()
        {
            // Restore selection from session
            var session = GameSession.Instance;
            if (session != null && session.SelectedHeroClass != null)
            {
                for (int i = 0; i < allClasses.Count; i++)
                {
                    if (allClasses[i] == session.SelectedHeroClass) { selectedClassIndex = i; break; }
                }
            }

            BuildOverlayUI();
            InitSkillsForClass();
            if (settingsPanel != null)
            {
                settingsPanel.OnClassSelected = OnClassSelected;
                settingsPanel.OnSlotSelectedForReplace = OnSlotSelectedForReplace;
                settingsPanel.OnAvailableSkillPicked = OnAvailableSkillPicked;
                settingsPanel.PopulateCarousel(allClasses, selectedClassIndex);
                settingsPanel.SetCurrentClass(GetCurrentClass());
                settingsPanel.SetSkillSet(currentSkills, GetCurrentClassColor());
                settingsPanel.PopulateAvailable(BuildAvailableSkillList());
            }
            RefreshAvatar();

            // Ensure an EventSystem exists in the scene (needed for buttons).
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
                Debug.Log("[HeroLobby] created EventSystem");
            }
        }

        private void PopulateSkillOwners()
        {
            if (allClasses == null) return;
            foreach (var c in allClasses)
            {
                if (c == null || c.classSkills == null) continue;
                foreach (var s in c.classSkills)
                {
                    if (s != null && s.ownerClass == null) s.ownerClass = c;
                }
            }
        }

        private void BuildOverlayUI()
        {
            // Disable any pre-existing Canvas in the scene so the V2 overlay is the only
            // visible/interactive UI. Old scene UI tends to leak click handlers and
            // overlap the new layout.
            foreach (var existingCanvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (existingCanvas == null) continue;
                if (existingCanvas.name == "HeroLobbyV2Canvas") continue;
                Debug.Log($"[HeroLobby] disabling legacy canvas '{existingCanvas.name}'");
                existingCanvas.gameObject.SetActive(false);
            }

            // Create a dedicated V2 canvas at sortingOrder 10.
            var canvasGO = new GameObject("HeroLobbyV2Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            overlayRoot = new GameObject("HeroLobbyV2", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            overlayRoot.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)overlayRoot.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var bgImg = overlayRoot.GetComponent<Image>();
            bgImg.color = new Color(0.06f, 0.06f, 0.1f, 1f);
            bgImg.raycastTarget = true;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Title (top-left)
            var titleGO = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            titleGO.transform.SetParent(overlayRoot.transform, false);
            var tRT = (RectTransform)titleGO.transform;
            tRT.anchorMin = new Vector2(0f, 1f); tRT.anchorMax = new Vector2(0f, 1f); tRT.pivot = new Vector2(0f, 1f);
            tRT.sizeDelta = new Vector2(400f, 40f); tRT.anchoredPosition = new Vector2(24f, -16f);
            titleText = titleGO.GetComponent<Text>();
            titleText.font = font; titleText.fontSize = 24; titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleLeft; titleText.color = Color.white; titleText.text = "勇者ロビー";
            titleText.raycastTarget = false;

            // TopRight stack (no background image so child raycasts work cleanly)
            var stackGO = new GameObject("TopRightStack", typeof(RectTransform));
            stackGO.transform.SetParent(overlayRoot.transform, false);
            var stRT = (RectTransform)stackGO.transform;
            stRT.anchorMin = new Vector2(1f, 1f); stRT.anchorMax = new Vector2(1f, 1f); stRT.pivot = new Vector2(1f, 1f);
            stRT.sizeDelta = new Vector2(80f, 160f);
            stRT.anchoredPosition = new Vector2(-8f, -8f);

            roleButton = CreateTopRightButton(stRT, font, "人", new Vector2(-8f, -8f), OnRoleClicked);
            settingsButton = CreateTopRightButton(stRT, font, "設", new Vector2(-8f, -88f), OnSettingsClicked);

            // Match start (bottom-right)
            var btnGO = new GameObject("MatchStart", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(overlayRoot.transform, false);
            var bRT = (RectTransform)btnGO.transform;
            bRT.anchorMin = new Vector2(1f, 0f); bRT.anchorMax = new Vector2(1f, 0f); bRT.pivot = new Vector2(1f, 0f);
            bRT.sizeDelta = new Vector2(280f, 60f); bRT.anchoredPosition = new Vector2(-24f, 24f);
            var bImg = btnGO.GetComponent<Image>(); bImg.color = new Color(0.95f, 0.95f, 0.95f, 1f); bImg.raycastTarget = true;
            var bLbl = MakeText(btnGO.transform, "Lbl", font, 20, FontStyle.Bold, "ENTER ⏎ マッチ開始");
            var bLR = (RectTransform)bLbl.transform; bLR.anchorMin = Vector2.zero; bLR.anchorMax = Vector2.one; bLR.offsetMin = Vector2.zero; bLR.offsetMax = Vector2.zero;
            var bT = bLbl.GetComponent<Text>(); bT.alignment = TextAnchor.MiddleCenter; bT.color = new Color(0.1f, 0.1f, 0.15f, 1f);
            startButton = btnGO.GetComponent<Button>();
            startButton.targetGraphic = bImg;
            startButton.onClick.AddListener(OnStartClicked);

            // Avatar view fills the whole overlay (its own children use absolute positioning)
            var avatarHolder = new GameObject("AvatarHolder", typeof(RectTransform));
            avatarHolder.transform.SetParent(overlayRoot.transform, false);
            var aRT = (RectTransform)avatarHolder.transform;
            aRT.anchorMin = Vector2.zero; aRT.anchorMax = Vector2.one;
            aRT.offsetMin = Vector2.zero; aRT.offsetMax = Vector2.zero;
            avatarView = HeroLobbyAvatarView.Create(aRT);
            avatarView.SetUserName(userName);

            // Settings panel (right side)
            settingsPanel = HeroLobbySettingsPanel.Create((RectTransform)overlayRoot.transform);
        }

        private Button CreateTopRightButton(Transform parent, Font font, string glyph, Vector2 anchoredPosFromTopRight, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Btn_" + glyph, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(1f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(64f, 64f);
            rt.anchoredPosition = anchoredPosFromTopRight;
            var img = go.GetComponent<Image>();
            img.color = new Color(0.95f, 0.95f, 0.95f, 1f);
            img.raycastTarget = true;

            var lblGO = MakeText(go.transform, "Lbl", font, 28, FontStyle.Bold, glyph);
            var lRT = (RectTransform)lblGO.transform;
            lRT.anchorMin = Vector2.zero; lRT.anchorMax = Vector2.one; lRT.offsetMin = Vector2.zero; lRT.offsetMax = Vector2.zero;
            var lt = lblGO.GetComponent<Text>();
            lt.alignment = TextAnchor.MiddleCenter; lt.color = new Color(0.12f, 0.12f, 0.18f, 1f);

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            return btn;
        }

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

        private HeroClassData GetCurrentClass()
        {
            if (allClasses == null || allClasses.Count == 0) return null;
            int i = Mathf.Clamp(selectedClassIndex, 0, allClasses.Count - 1);
            return allClasses[i];
        }

        private Color GetCurrentClassColor()
        {
            var c = GetCurrentClass();
            return c != null ? c.avatarColor : new Color(0.4f, 0.4f, 0.4f, 1f);
        }

        private void InitSkillsForClass()
        {
            var cls = GetCurrentClass();
            for (int i = 0; i < currentSkills.Length; i++) currentSkills[i] = null;
            if (cls != null && cls.classSkills != null)
            {
                for (int i = 0; i < 4 && i < cls.classSkills.Count; i++) currentSkills[i] = cls.classSkills[i];
            }
            var session = GameSession.Instance;
            SkillData freeSlot = session != null ? session.SelectedCommonSkill : null;
            if (freeSlot == null && commonSkills != null && commonSkills.Count > 0) freeSlot = commonSkills[0];
            currentSkills[4] = freeSlot;
        }

        private void OnClassSelected(int idx)
        {
            if (idx < 0 || idx >= allClasses.Count) return;
            if (selectedClassIndex == idx) return;
            selectedClassIndex = idx;
            InitSkillsForClass();
            RefreshAvatar();
            if (settingsPanel != null)
            {
                settingsPanel.SetCurrentClass(GetCurrentClass());
                settingsPanel.SetSkillSet(currentSkills, GetCurrentClassColor());
                settingsPanel.PopulateAvailable(BuildAvailableSkillList());
                settingsPanel.ClearSelectedSlot();
            }
        }

        private void OnSlotSelectedForReplace(int slotIdx)
        {
            if (slotIdx < 4)
            {
                Debug.Log("[HeroLobby] slots 1-4 are locked to current class");
                selectedSlot = -1;
                if (settingsPanel != null) settingsPanel.ClearSelectedSlot();
                return;
            }
            selectedSlot = slotIdx;
        }

        private void OnAvailableSkillPicked(SkillData s)
        {
            if (s == null) return;
            if (selectedSlot != 4)
            {
                Debug.Log("[HeroLobby] no slot selected (only slot 5 is changeable)");
                return;
            }
            currentSkills[4] = s;
            RefreshAvatar();
            if (settingsPanel != null)
            {
                settingsPanel.SetSkillSet(currentSkills, GetCurrentClassColor());
                settingsPanel.ClearSelectedSlot();
            }
            selectedSlot = -1;
        }

        public List<SkillData> BuildAvailableSkillList()
        {
            var list = new List<SkillData>();
            var current = GetCurrentClass();
            if (allClasses != null)
            {
                foreach (var c in allClasses)
                {
                    if (c == null || c == current) continue;
                    if (c.classSkills == null) continue;
                    foreach (var s in c.classSkills) if (s != null) list.Add(s);
                }
            }
            if (commonSkills != null) foreach (var s in commonSkills) if (s != null) list.Add(s);
            return list;
        }

        private void RefreshAvatar()
        {
            var cls = GetCurrentClass();
            if (avatarView == null) return;
            if (cls != null)
            {
                avatarView.SetAvatarColor(cls.avatarColor);
                avatarView.SetClassName(cls.className);
            }
            avatarView.SetSkills(currentSkills, GetCurrentClassColor());
        }

        private void OnRoleClicked()
        {
            Debug.Log($"[HeroLobby] role button clicked, settingsPanel={(settingsPanel != null ? "ok" : "null")}, isOpen={(settingsPanel != null && settingsPanel.IsOpen)}");
            if (settingsPanel == null) return;
            if (settingsPanel.IsOpen) settingsPanel.Hide();
            else settingsPanel.Show();
        }

        private void OnSettingsClicked()
        {
            Debug.Log("[HeroLobby] settings not yet implemented");
        }

        private void Update()
        {
            if (Game.UI.ConsoleUI.IsConsoleActive) return;
            if (Keyboard.current == null) return;
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (settingsPanel != null && settingsPanel.IsOpen) settingsPanel.Hide();
            }
            else if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)
            {
                if (settingsPanel == null || !settingsPanel.IsOpen) OnStartClicked();
            }
        }

        private void OnStartClicked()
        {
            var session = GameSession.Instance;
            if (session != null)
            {
                var cls = GetCurrentClass();
                if (cls != null) session.SelectedHeroClass = cls;
                session.SelectedCommonSkill = currentSkills[4];
                Debug.Log($"[HeroLobby] start: class={(cls != null ? cls.className : "?")}, slot5={(currentSkills[4] != null ? currentSkills[4].skillName : "?")}");
            }
            SceneManager.LoadScene(matchSceneName);
        }
    }
}
