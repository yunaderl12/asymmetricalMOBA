using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Game.Core;
using Game.Hero;
using Game.Skill;

namespace Game.UI
{
    /// スキルポイントのメモ。レベルアップの演出（LevelUpBurst）が終わるころ、右下（スキルカードの上）に
    /// ぽんと貼り出されて、スキルの割り当てが終わるまでそこに残る。
    public class SkillLevelUpUI : MonoBehaviour
    {
        // 右下に置いたときの位置（右下の角から）と大きさ。
        private static readonly Vector2 DockPosition = new Vector2(-40f, 252f);
        private static readonly Vector2 NoteSize = new Vector2(300f, 78f);
        // レベルアップの演出が消え始めるころに貼り出す。
        private const float PopDelay = LevelUpBurst.Seconds - 0.45f;
        private const float PopInSeconds = 0.32f;

        private RectTransform note;
        private float popStart = -1f;
        // すでに右下に貼ってある（ポイントが残っている間に次のレベルアップが来たら、貼ったままにする）。
        private bool docked;

        [SerializeField] private GameObject root;
        [SerializeField] private Text pointsText;
        [SerializeField] private Text hintText;

        private LevelComponent level;
        private SkillRuntime runtime;

        private static readonly GameKey[] DigitKeys = { GameKey.Slot1, GameKey.Slot2, GameKey.Slot3, GameKey.Slot4, GameKey.Slot5 };

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
            if (hintText != null) hintText.text = SlotHint();
            KeyBindings.Changed += OnKeysChanged;
            Refresh();
        }

        /// 少し傾いた紙にテープ。上の行に「スキルアップ +n」、下の行に割り当て方。
        private void BuildNote()
        {
            var pal = HudStyle.HeroPalette;
            note = HudStyle.Place(HudStyle.Canvas.transform, "SkillLevelUpNote", new Vector2(1f, 0f), new Vector2(1f, 0f), DockPosition, NoteSize);
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
            KeyBindings.Changed -= OnKeysChanged;
            if (level != null) level.OnLevelChanged -= OnLevelChanged;
            if (runtime != null) runtime.OnLevelUpPointsChanged -= OnPointsChanged;
        }

        private void OnLevelChanged(int newLevel)
        {
            if (newLevel <= 1) return;
            if (runtime != null) runtime.AddLevelUpPoint();
            Pop(newLevel);
        }

        /// レベルアップの演出のあとに、右下へ貼り出す。
        public void Pop(int newLevel)
        {
            if (note == null) return;
            popStart = Time.unscaledTime + PopDelay;
            Refresh();
        }

        private bool Popping => popStart >= 0f;

        private void Animate()
        {
            if (note == null) return;
            float t = Time.unscaledTime - popStart;
            if (docked)
            {
                popStart = -1f;
                return;
            }
            if (t < 0f)
            {
                note.localScale = Vector3.zero;
                return;
            }
            float k = Mathf.Clamp01(t / PopInSeconds);
            // 下から滑り込みながら、少し行きすぎて戻る。
            float s = k - 1f;
            float back = 1f + 2.7f * s * s * s + 1.7f * s * s;
            note.localScale = new Vector3(back, back, 1f);
            note.anchoredPosition = DockPosition + new Vector2(0f, -36f * (1f - k) * (1f - k));
            note.localEulerAngles = new Vector3(0f, 0f, -1.2f + 6f * (1f - k));
            if (k >= 1f)
            {
                popStart = -1f;
                docked = true;
                note.localScale = Vector3.one;
                note.anchoredPosition = DockPosition;
                note.localEulerAngles = new Vector3(0f, 0f, -1.2f);
                Refresh();
            }
        }

        private void OnKeysChanged()
        {
            if (hintText != null) hintText.text = SlotHint();
        }

        // 初期のキーなら「1〜5キー」、変えてあれば今のキーを並べる。
        private static string SlotHint()
        {
            string keys = "";
            bool defaults = true;
            for (int i = 0; i < DigitKeys.Length; i++)
            {
                var entry = KeyBindings.Get(DigitKeys[i]);
                if (entry.Path != entry.DefaultPath) defaults = false;
                keys += (i > 0 ? "・" : "") + KeyBindings.DisplayName(DigitKeys[i]);
            }
            return defaults ? "1〜5キーで割り当て" : keys + " で割り当て";
        }

        private void OnPointsChanged(int newCount)
        {
            Refresh();
        }

        private void Refresh()
        {
            int pts = runtime != null ? runtime.LevelUpPointsAvailable : 0;
            if (pts <= 0) docked = false;
            if (root != null) root.SetActive(pts > 0 || Popping);
            if (pointsText != null) pointsText.text = "スキルアップ +" + pts;
        }

        private void Update()
        {
            if (Popping) Animate();
            if (runtime == null) return;
            if (runtime.LevelUpPointsAvailable <= 0) return;
            if (Keyboard.current == null) return;
            if (InGameMenu.BlocksGameInput) return;
            for (int i = 0; i < DigitKeys.Length; i++)
            {
                if (KeyBindings.WasPressed(DigitKeys[i]))
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
