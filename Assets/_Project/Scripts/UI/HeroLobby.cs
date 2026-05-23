using System.Collections.Generic;
using UnityEngine;
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
        private Button gearButton;
        private Button startButton;

        private void Awake()
        {
            GameSession.GetOrCreate();
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
                settingsPanel.SetSkillSet(currentSkills);
                settingsPanel.PopulateAvailable(BuildAvailableSkillList());
            }
            RefreshAvatar();
        }

        private void BuildOverlayUI()
        {
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null) { Debug.LogWarning("[HeroLobby] no Canvas in scene"); return; }

            // Hide all existing UI children to use new fullscreen design
            for (int i = 0; i < canvas.transform.childCount; i++)
            {
                var child = canvas.transform.GetChild(i);
                if (child.name.StartsWith("HeroLobbyV2")) continue;
                child.gameObject.SetActive(false);
            }

            overlayRoot = new GameObject("HeroLobbyV2", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            overlayRoot.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)overlayRoot.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            overlayRoot.GetComponent<Image>().color = new Color(0.06f, 0.06f, 0.1f, 1f);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // TopBar - title left
            var titleGO = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            titleGO.transform.SetParent(overlayRoot.transform, false);
            var tRT = (RectTransform)titleGO.transform;
            tRT.anchorMin = new Vector2(0f, 1f); tRT.anchorMax = new Vector2(0f, 1f); tRT.pivot = new Vector2(0f, 1f);
            tRT.sizeDelta = new Vector2(400f, 40f); tRT.anchoredPosition = new Vector2(24f, -16f);
            titleText = titleGO.GetComponent<Text>();
            titleText.font = font; titleText.fontSize = 24; titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleLeft; titleText.color = Color.white; titleText.text = "勇者ロビー";
            titleText.raycastTarget = false;

            // Gear (top-right)
            var gearGO = new GameObject("Gear", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            gearGO.transform.SetParent(overlayRoot.transform, false);
            var gRT = (RectTransform)gearGO.transform;
            gRT.anchorMin = new Vector2(1f, 1f); gRT.anchorMax = new Vector2(1f, 1f); gRT.pivot = new Vector2(1f, 1f);
            gRT.sizeDelta = new Vector2(56f, 56f); gRT.anchoredPosition = new Vector2(-16f, -16f);
            gearGO.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f, 0.95f);
            var gLbl = MakeText(gearGO.transform, "Lbl", font, 26, FontStyle.Bold, "⚙");
            var gLR = (RectTransform)gLbl.transform; gLR.anchorMin = Vector2.zero; gLR.anchorMax = Vector2.one; gLR.offsetMin = Vector2.zero; gLR.offsetMax = Vector2.zero;
            gLbl.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            gearButton = gearGO.GetComponent<Button>();
            gearButton.onClick.AddListener(ToggleSettings);

            // Match start (bottom-right)
            var btnGO = new GameObject("MatchStart", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(overlayRoot.transform, false);
            var bRT = (RectTransform)btnGO.transform;
            bRT.anchorMin = new Vector2(1f, 0f); bRT.anchorMax = new Vector2(1f, 0f); bRT.pivot = new Vector2(1f, 0f);
            bRT.sizeDelta = new Vector2(280f, 60f); bRT.anchoredPosition = new Vector2(-24f, 24f);
            btnGO.GetComponent<Image>().color = new Color(0.95f, 0.95f, 0.95f, 1f);
            var bLbl = MakeText(btnGO.transform, "Lbl", font, 20, FontStyle.Bold, "ENTER ⏎ マッチ開始");
            var bLR = (RectTransform)bLbl.transform; bLR.anchorMin = Vector2.zero; bLR.anchorMax = Vector2.one; bLR.offsetMin = Vector2.zero; bLR.offsetMax = Vector2.zero;
            var bT = bLbl.GetComponent<Text>(); bT.alignment = TextAnchor.MiddleCenter; bT.color = new Color(0.1f, 0.1f, 0.15f, 1f);
            startButton = btnGO.GetComponent<Button>();
            startButton.onClick.AddListener(OnStartClicked);

            // Avatar view (middle/lower)
            var avatarHolder = new GameObject("AvatarHolder", typeof(RectTransform));
            avatarHolder.transform.SetParent(overlayRoot.transform, false);
            var aRT = (RectTransform)avatarHolder.transform;
            aRT.anchorMin = new Vector2(0f, 0f); aRT.anchorMax = new Vector2(1f, 1f);
            aRT.offsetMin = new Vector2(0f, 100f); aRT.offsetMax = new Vector2(0f, -60f);
            avatarView = HeroLobbyAvatarView.Create(aRT);
            avatarView.SetUserName(userName);

            // Settings panel (right side, off-screen initially)
            settingsPanel = HeroLobbySettingsPanel.Create((RectTransform)overlayRoot.transform);
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

        private void InitSkillsForClass()
        {
            var cls = GetCurrentClass();
            for (int i = 0; i < currentSkills.Length; i++) currentSkills[i] = null;
            if (cls != null && cls.classSkills != null)
            {
                for (int i = 0; i < 4 && i < cls.classSkills.Count; i++) currentSkills[i] = cls.classSkills[i];
            }
            // Slot 5 from session selection or fallback to first common
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
                settingsPanel.SetSkillSet(currentSkills);
                settingsPanel.PopulateAvailable(BuildAvailableSkillList());
                settingsPanel.ClearSelectedSlot();
            }
        }

        private int selectedSlot = -1;
        private void OnSlotSelectedForReplace(int slotIdx)
        {
            selectedSlot = slotIdx;
        }

        private void OnAvailableSkillPicked(SkillData s)
        {
            if (s == null) return;
            if (selectedSlot < 0)
            {
                Debug.Log("[HeroLobby] no slot selected; click a skill set slot first");
                return;
            }
            currentSkills[selectedSlot] = s;
            RefreshAvatar();
            if (settingsPanel != null)
            {
                settingsPanel.SetSkillSet(currentSkills);
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
            avatarView.SetSkills(currentSkills);
        }

        private void ToggleSettings()
        {
            if (settingsPanel == null) return;
            if (settingsPanel.IsOpen) settingsPanel.Hide();
            else settingsPanel.Show();
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
