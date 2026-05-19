using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Game.Core;
using Game.Hero;
using Game.Skill;

namespace Game.UI
{
    public class HeroLobby : MonoBehaviour
    {
        [Header("Class Buttons (4)")]
        [SerializeField] private Button[] classButtons;
        [SerializeField] private HeroClassData[] classDatas;
        [SerializeField] private Image[] classButtonFrames;

        [Header("All Hero Classes")]
        [SerializeField] private List<HeroClassData> allClasses = new List<HeroClassData>();
        [SerializeField] private List<SkillData> commonSkills = new List<SkillData>();

        [Header("Skill Slots (5)")]
        [SerializeField] private Image[] slotBackgrounds;
        [SerializeField] private Text[] slotNameTexts;
        [SerializeField] private Text[] slotCostTexts;
        [SerializeField] private Button slot5ChangeButton;
        [SerializeField] private Color lockedSlotColor = new Color(0.25f, 0.25f, 0.3f, 0.9f);
        [SerializeField] private Color freeSlotColor = new Color(0.9f, 0.75f, 0.2f, 0.95f);

        [Header("Skill Picker")]
        [SerializeField] private SkillPickerUI skillPicker;

        [Header("Match Start")]
        [SerializeField] private Button startMatchButton;
        [SerializeField] private string matchSceneName = "MatchLobby";

        [Header("Navigation")]
        [SerializeField] private Button backToTitleButton;
        [SerializeField] private string titleSceneName = "Title";

        [Header("Last Match Stats")]
        [SerializeField] private Text lastStatsText;

        [Header("Colors")]
        [SerializeField] private Color normalFrame = new Color(0.2f, 0.2f, 0.25f, 0.85f);
        [SerializeField] private Color selectedFrame = new Color(1f, 0.85f, 0.2f, 0.95f);

        private int selectedClassIndex = 0;
        private SkillData freeSlotSkill;

        private void Awake()
        {
            GameSession.GetOrCreate();
            if (allClasses == null || allClasses.Count == 0) Debug.LogWarning("[HeroLobby] allClasses is empty - assign in inspector");
            if (commonSkills == null || commonSkills.Count == 0) Debug.LogWarning("[HeroLobby] commonSkills is empty - assign in inspector");
        }

        private void Start()
        {
            var session = GameSession.Instance;
            for (int i = 0; i < classButtons.Length; i++)
            {
                int idx = i;
                if (classButtons[i] != null) classButtons[i].onClick.AddListener(() => SelectClass(idx));
            }

            if (session != null)
            {
                for (int i = 0; i < classDatas.Length; i++) if (classDatas[i] == session.SelectedHeroClass) { selectedClassIndex = i; break; }
                freeSlotSkill = session.SelectedCommonSkill;
            }
            EnsureFreeSlotDefault();
            ValidateFreeSlotAgainstClass();

            if (slot5ChangeButton != null) slot5ChangeButton.onClick.AddListener(OpenPicker);
            if (startMatchButton != null) startMatchButton.onClick.AddListener(OnStartClicked);
            if (backToTitleButton != null) backToTitleButton.onClick.AddListener(OnBackToTitleClicked);

            RefreshAll();
            ShowLastStats();
        }

        private void OnBackToTitleClicked()
        {
            SceneManager.LoadScene(titleSceneName);
        }

        private void SelectClass(int idx)
        {
            if (selectedClassIndex == idx) return;
            selectedClassIndex = idx;
            ValidateFreeSlotAgainstClass();
            RefreshAll();
        }

        private void EnsureFreeSlotDefault()
        {
            if (freeSlotSkill != null) return;
            if (commonSkills != null && commonSkills.Count > 0) freeSlotSkill = commonSkills[0];
        }

        private void ValidateFreeSlotAgainstClass()
        {
            var candidates = BuildFreeSlotCandidates();
            if (freeSlotSkill == null || !candidates.Contains(freeSlotSkill))
            {
                var prev = freeSlotSkill;
                freeSlotSkill = commonSkills != null && commonSkills.Count > 0 ? commonSkills[0] : null;
                if (prev != null) Debug.Log($"[HeroLobby] free slot reset (was {prev.skillName}, no longer in candidates) -> {(freeSlotSkill != null ? freeSlotSkill.skillName : "null")}");
            }
        }

        public List<SkillData> BuildFreeSlotCandidates()
        {
            var list = new List<SkillData>();
            var currentClass = classDatas != null && selectedClassIndex >= 0 && selectedClassIndex < classDatas.Length ? classDatas[selectedClassIndex] : null;
            if (allClasses != null)
            {
                foreach (var c in allClasses)
                {
                    if (c == null || c == currentClass) continue;
                    if (c.classSkills == null) continue;
                    foreach (var s in c.classSkills) if (s != null) list.Add(s);
                }
            }
            if (commonSkills != null) foreach (var s in commonSkills) if (s != null) list.Add(s);
            return list;
        }

        private void OpenPicker()
        {
            if (skillPicker == null) { Debug.LogWarning("[HeroLobby] skillPicker not assigned"); return; }
            var candidates = BuildFreeSlotCandidates();
            skillPicker.Show(candidates, OnSkillPicked);
        }

        private void OnSkillPicked(SkillData picked)
        {
            if (picked == null) return;
            freeSlotSkill = picked;
            RefreshAll();
        }

        private void RefreshAll()
        {
            RefreshClassFrames();
            RefreshSlotDisplay();
        }

        private void RefreshClassFrames()
        {
            if (classButtonFrames == null) return;
            for (int i = 0; i < classButtonFrames.Length; i++)
                if (classButtonFrames[i] != null) classButtonFrames[i].color = (i == selectedClassIndex) ? selectedFrame : normalFrame;
        }

        private void RefreshSlotDisplay()
        {
            var cls = classDatas != null && selectedClassIndex < classDatas.Length ? classDatas[selectedClassIndex] : null;
            for (int i = 0; i < 5; i++)
            {
                SkillData s = null;
                if (i < 4)
                {
                    if (cls != null && cls.classSkills != null && i < cls.classSkills.Count) s = cls.classSkills[i];
                }
                else
                {
                    s = freeSlotSkill;
                }
                if (slotBackgrounds != null && i < slotBackgrounds.Length && slotBackgrounds[i] != null)
                    slotBackgrounds[i].color = i < 4 ? lockedSlotColor : freeSlotColor;
                if (slotNameTexts != null && i < slotNameTexts.Length && slotNameTexts[i] != null)
                    slotNameTexts[i].text = s != null ? s.skillName : "-";
                if (slotCostTexts != null && i < slotCostTexts.Length && slotCostTexts[i] != null)
                    slotCostTexts[i].text = s != null ? $"SP {s.spCost}  CD {s.cooldown:F0}s" : "";
            }
        }

        private void ShowLastStats()
        {
            if (lastStatsText == null) return;
            var session = GameSession.Instance;
            if (session == null || session.LastMatchStats == null) { lastStatsText.text = ""; return; }
            var s = session.LastMatchStats;
            lastStatsText.text = $"前回戦績  K {s.kills}  D {s.deaths}  A {s.assists}";
        }

        private void OnStartClicked()
        {
            var session = GameSession.Instance;
            if (session != null)
            {
                if (selectedClassIndex >= 0 && selectedClassIndex < classDatas.Length) session.SelectedHeroClass = classDatas[selectedClassIndex];
                session.SelectedCommonSkill = freeSlotSkill;
            }
            SceneManager.LoadScene(matchSceneName);
        }
    }
}
