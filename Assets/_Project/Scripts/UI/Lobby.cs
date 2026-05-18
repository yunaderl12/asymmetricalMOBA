using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Game.Core;
using Game.Hero;
using Game.Skill;

namespace Game.UI
{
    public class Lobby : MonoBehaviour
    {
        [Header("Class Buttons (4)")]
        [SerializeField] private Button[] classButtons;
        [SerializeField] private HeroClassData[] classDatas;
        [SerializeField] private Image[] classButtonFrames;

        [Header("Common Skill Buttons (3)")]
        [SerializeField] private Button[] commonSkillButtons;
        [SerializeField] private SkillData[] commonSkillDatas;
        [SerializeField] private Image[] commonSkillFrames;

        [Header("Match Start")]
        [SerializeField] private Button startMatchButton;
        [SerializeField] private string matchSceneName = "MainStage";

        [Header("Last Match Stats")]
        [SerializeField] private Text lastStatsText;

        [Header("Colors")]
        [SerializeField] private Color normalFrame = new Color(0.2f, 0.2f, 0.25f, 0.85f);
        [SerializeField] private Color selectedFrame = new Color(1f, 0.85f, 0.2f, 0.95f);

        private int selectedClassIndex = 0;
        private int selectedSkillIndex = 0;

        private void Awake()
        {
            GameSession.GetOrCreate();
        }

        private void Start()
        {
            var session = GameSession.Instance;
            for (int i = 0; i < classButtons.Length; i++)
            {
                int idx = i;
                if (classButtons[i] != null) classButtons[i].onClick.AddListener(() => SelectClass(idx));
            }
            for (int i = 0; i < commonSkillButtons.Length; i++)
            {
                int idx = i;
                if (commonSkillButtons[i] != null) commonSkillButtons[i].onClick.AddListener(() => SelectSkill(idx));
            }

            // Initialize selection from session if it matches a known asset
            if (session != null)
            {
                for (int i = 0; i < classDatas.Length; i++) if (classDatas[i] == session.SelectedHeroClass) { selectedClassIndex = i; break; }
                for (int i = 0; i < commonSkillDatas.Length; i++) if (commonSkillDatas[i] == session.SelectedCommonSkill) { selectedSkillIndex = i; break; }
            }
            RefreshFrames();
            ShowLastStats();

            if (startMatchButton != null) startMatchButton.onClick.AddListener(OnStartClicked);
        }

        private void SelectClass(int idx)
        {
            selectedClassIndex = idx;
            RefreshFrames();
        }

        private void SelectSkill(int idx)
        {
            selectedSkillIndex = idx;
            RefreshFrames();
        }

        private void RefreshFrames()
        {
            if (classButtonFrames != null)
                for (int i = 0; i < classButtonFrames.Length; i++)
                    if (classButtonFrames[i] != null) classButtonFrames[i].color = (i == selectedClassIndex) ? selectedFrame : normalFrame;
            if (commonSkillFrames != null)
                for (int i = 0; i < commonSkillFrames.Length; i++)
                    if (commonSkillFrames[i] != null) commonSkillFrames[i].color = (i == selectedSkillIndex) ? selectedFrame : normalFrame;
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
                if (selectedSkillIndex >= 0 && selectedSkillIndex < commonSkillDatas.Length) session.SelectedCommonSkill = commonSkillDatas[selectedSkillIndex];
            }
            SceneManager.LoadScene(matchSceneName);
        }
    }
}
