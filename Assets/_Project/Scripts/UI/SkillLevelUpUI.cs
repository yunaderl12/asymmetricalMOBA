using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Game.Core;
using Game.Hero;
using Game.Skill;

namespace Game.UI
{
    /// レベルアップのメモ。レベルが上がると、まず画面の真ん中に大きく出て（上に「レベルアップ！ Lv n」のリボン）、
    /// 数秒たつと右下（スキルカードの上）へ縮みながら移動して、スキルの割り当てが終わるまでそこに残る。
    public class SkillLevelUpUI : MonoBehaviour
    {
        // 右下に置いたときの位置（右下の角から）と大きさ。
        private static readonly Vector2 DockPosition = new Vector2(-40f, 252f);
        private static readonly Vector2 NoteSize = new Vector2(300f, 78f);
        private const float CenterScale = 1.6f;
        private const float PopInSeconds = 0.3f;
        private const float HoldSeconds = 1.7f; // 出てくる0.3秒と合わせて、真ん中に2秒出してから右下へ動かす
        private const float MoveSeconds = 0.6f;

        private RectTransform note;
        private RectTransform banner;
        private Text bannerText;
        private CanvasGroup bannerGroup;
        private float popStart = -1f;

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
            note = HudStyle.Place(HudStyle.Canvas.transform, "SkillLevelUpNote", new Vector2(1f, 0f), new Vector2(1f, 0f), DockPosition, NoteSize);
            note.localEulerAngles = new Vector3(0f, 0f, -1.2f);
            HudStyle.Paper(note, pal, HudShape.Kind.RoundRect, 2f, 0, 4f);
            var tape = HudStyle.Place(note, "Tape", new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 20f));
            tape.localEulerAngles = new Vector3(0f, 0f, 3f);
            HudStyle.Shape(tape, "Tape", HudShape.Kind.RoundRect, new Color(0.91f, 0.36f, 0.47f, 0.8f), 0f);
            var topLeft = new Vector2(0f, 1f);
            pointsText = HudStyle.Label(note, "Points", topLeft, topLeft, new Vector2(14f, -8f), new Vector2(272f, 32f), "", 23, pal.Ribbon, TextAnchor.MiddleLeft);
            hintText = HudStyle.Label(note, "Hint", topLeft, topLeft, new Vector2(14f, -42f), new Vector2(272f, 28f), "", 20, pal.Ink, TextAnchor.MiddleLeft);

            // 真ん中に出ているあいだだけ見せるリボン。
            banner = HudStyle.Place(note, "Banner", new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(290f, 46f));
            var ribbon = HudStyle.Shape(banner, "Ribbon", HudShape.Kind.Ribbon, pal.Ribbon);
            ribbon.notch = 16f;
            bannerText = HudStyle.AddText(HudStyle.Filled(banner, "Text"), "", 24, Color.white, TextAnchor.MiddleCenter);
            bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0f;
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
            Pop(newLevel);
        }

        /// 真ん中に大きく出してから右下へ動かす。
        public void Pop(int newLevel)
        {
            if (note == null) return;
            bannerText.text = "レベルアップ！ Lv " + newLevel;
            popStart = Time.unscaledTime;
            root.SetActive(true);
            Animate();
        }

        private bool Popping => popStart >= 0f;

        private void Animate()
        {
            if (note == null) return;
            if (!Popping)
            {
                note.anchoredPosition = DockPosition;
                note.localScale = Vector3.one;
                bannerGroup.alpha = 0f;
                return;
            }
            float t = Time.unscaledTime - popStart;
            float scale, move, bannerAlpha;
            if (t < PopInSeconds)
            {
                // ぽんと膨らんで少し戻る。
                float k = t / PopInSeconds;
                float back = 1f + 2.2f * Mathf.Pow(k - 1f, 3f) + 1.2f * Mathf.Pow(k - 1f, 2f);
                scale = CenterScale * Mathf.LerpUnclamped(0.5f, 1f, back);
                move = 0f;
                bannerAlpha = k;
            }
            else if (t < PopInSeconds + HoldSeconds)
            {
                scale = CenterScale;
                move = 0f;
                bannerAlpha = 1f;
            }
            else
            {
                float k = Mathf.Clamp01((t - PopInSeconds - HoldSeconds) / MoveSeconds);
                move = k * k * (3f - 2f * k);
                scale = Mathf.Lerp(CenterScale, 1f, move);
                bannerAlpha = 1f - move;
                if (k >= 1f) popStart = -1f;
            }
            note.localScale = new Vector3(scale, scale, 1f);
            note.anchoredPosition = Vector2.Lerp(CenterPosition(scale), DockPosition, move);
            bannerGroup.alpha = bannerAlpha;
            if (!Popping) Refresh();
        }

        // 真ん中（少し上）に置くときの anchoredPosition。メモは右下の角が基準。
        private Vector2 CenterPosition(float scale)
        {
            var canvas = (RectTransform)HudStyle.Canvas.transform;
            Vector2 size = canvas.rect.size;
            return new Vector2(-size.x * 0.5f + NoteSize.x * scale * 0.5f, size.y * 0.5f + 60f - NoteSize.y * scale * 0.5f);
        }

        private void OnPointsChanged(int newCount)
        {
            Refresh();
        }

        private void Refresh()
        {
            int pts = runtime != null ? runtime.LevelUpPointsAvailable : 0;
            if (root != null) root.SetActive(pts > 0 || Popping);
            if (pointsText != null) pointsText.text = "スキルアップ +" + pts;
        }

        private void Update()
        {
            if (Popping) Animate();
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
