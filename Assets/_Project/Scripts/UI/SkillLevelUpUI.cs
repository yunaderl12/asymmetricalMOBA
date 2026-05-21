using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Game.Core;
using Game.Hero;
using Game.Skill;

namespace Game.UI
{
    public class SkillLevelUpUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text pointsText;
        [SerializeField] private Text hintText;

        private LevelComponent level;
        private SkillRuntime runtime;

        private static readonly Key[] DigitKeys = new Key[] { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 };

        private void Awake()
        {
            if (root != null) root.SetActive(false);
        }

        private void Start()
        {
            var hero = HumanHeroMarker.Instance != null ? HumanHeroMarker.Instance.gameObject : GameObject.FindWithTag("Player");
            if (hero == null) { Debug.LogWarning("[SkillLevelUpUI] human Hero not found"); return; }
            level = hero.GetComponent<LevelComponent>();
            runtime = hero.GetComponent<SkillRuntime>();
            if (level != null) level.OnLevelChanged += OnLevelChanged;
            if (runtime != null) runtime.OnLevelUpPointsChanged += OnPointsChanged;
            if (hintText != null) hintText.text = "1〜5キーで割り当て";
            Refresh();
        }

        private void OnDestroy()
        {
            if (level != null) level.OnLevelChanged -= OnLevelChanged;
            if (runtime != null) runtime.OnLevelUpPointsChanged -= OnPointsChanged;
        }

        private void OnLevelChanged(int newLevel)
        {
            if (newLevel <= 1) return;
            if (runtime != null) runtime.AddLevelUpPoint();
        }

        private void OnPointsChanged(int newCount)
        {
            Refresh();
        }

        private void Refresh()
        {
            int pts = runtime != null ? runtime.LevelUpPointsAvailable : 0;
            if (root != null) root.SetActive(pts > 0);
            if (pointsText != null) pointsText.text = "スキルアップ可能 +" + pts;
        }

        private void Update()
        {
            if (runtime == null) return;
            if (runtime.LevelUpPointsAvailable <= 0) return;
            if (Keyboard.current == null) return;
            if (ConsoleUI.IsConsoleActive) return;
            for (int i = 0; i < DigitKeys.Length; i++)
            {
                if (Keyboard.current[DigitKeys[i]].wasPressedThisFrame)
                {
                    if (runtime.SpendLevelUpPoint(i))
                    {
                        Debug.Log($"[SkillLevelUp] slot {i + 1} leveled up");
                    }
                    else
                    {
                        Debug.Log($"[SkillLevelUp] slot {i + 1} cannot level up (maxed or empty)");
                    }
                    break;
                }
            }
        }
    }
}
