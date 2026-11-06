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
            // Not for the demon king side of the match.
            if (Game.Core.GameSession.Instance != null && Game.Core.GameSession.Instance.CurrentRole == Game.Core.GameSession.PlayerRole.DemonKing)
            {
                if (root != null) root.SetActive(false);
                enabled = false;
                return;
            }

            var hero = HumanHeroMarker.Instance != null ? HumanHeroMarker.Instance.gameObject : GameObject.FindWithTag("Player");
            if (hero == null) { Debug.LogWarning("[SkillLevelUpUI] human Hero not found"); return; }
            // シーンに置いた古いパネルの代わりに、スキルカードの上へ紙のメモを貼る。
            if (root != null) root.SetActive(false);
            BuildNote();

            level = hero.GetComponent<LevelComponent>();
            runtime = hero.GetComponent<SkillRuntime>();
            if (level != null) level.OnLevelChanged += OnLevelChanged;
            if (runtime != null) runtime.OnLevelUpPointsChanged += OnPointsChanged;
            if (hintText != null) hintText.text = "1〜5キーで割り当て";
            Refresh();
        }

        /// 少し傾いた紙にテープ。上の行に「スキルアップ +n」、下の行に割り当て方。
        private void BuildNote()
        {
            var pal = HudStyle.HeroPalette;
            var note = HudStyle.Place(HudStyle.Canvas.transform, "SkillLevelUpNote", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 252f), new Vector2(300f, 78f));
            note.localEulerAngles = new Vector3(0f, 0f, -1.2f);
            HudStyle.Paper(note, pal, HudShape.Kind.RoundRect, 2f, 0, 4f);
            var tape = HudStyle.Place(note, "Tape", new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 20f));
            tape.localEulerAngles = new Vector3(0f, 0f, 3f);
            HudStyle.Shape(tape, "Tape", HudShape.Kind.RoundRect, new Color(0.91f, 0.36f, 0.47f, 0.8f), 0f);
            var topLeft = new Vector2(0f, 1f);
            pointsText = HudStyle.Label(note, "Points", topLeft, topLeft, new Vector2(14f, -8f), new Vector2(272f, 32f), "", 23, pal.Ribbon, TextAnchor.MiddleLeft);
            hintText = HudStyle.Label(note, "Hint", topLeft, topLeft, new Vector2(14f, -42f), new Vector2(272f, 28f), "", 20, pal.Ink, TextAnchor.MiddleLeft);
            root = note.gameObject;
            root.SetActive(false);
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
            if (pointsText != null) pointsText.text = "スキルアップ +" + pts;
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
