using UnityEngine;
using UnityEngine.UI;
using Game.Combat;
using Game.Core;

namespace Game.UI
{
    /// 画面上中央の戦況。丸い時計（残り時間）、左に「はじまりのまち」、右に「魔王城コア」のHP、時計の下にリボン。
    /// リボンは勇者側なら魔王のLv、魔王側なら次のLvまでの時間。勇者側はコアの下に魔王のHPも出す。
    public class MatchStatusHud : MonoBehaviour
    {
        private const float RebindInterval = 1f;

        public static MatchStatusHud Instance { get; private set; }

        private HudTheme theme;
        private HudStyle.Palette pal;

        private HudBar townBar, coreBar;
        private Text townValue, coreValue, clockLabel, clockTime, ribbonText, dkHpText;
        private GameObject dkHpTab;

        private HealthComponent town, core, dkHealth;
        private LevelComponent dkLevel;
        private float nextRebind;

        private int shownTown = -1, shownCore = -1, shownDkHp = -1, shownDkMax = -1, shownSeconds = -1;
        private string shownRibbon, shownClockLabel;

        public static MatchStatusHud Create(HudTheme theme)
        {
            if (Instance != null) return Instance;
            var root = HudStyle.Node(HudStyle.Canvas.transform, "MatchStatusHud");
            HudStyle.Stretch(root);
            var hud = root.gameObject.AddComponent<MatchStatusHud>();
            hud.theme = theme;
            hud.pal = HudStyle.For(theme);
            hud.Build((RectTransform)root);
            Instance = hud;
            return hud;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Build(RectTransform root)
        {
            var top = new Vector2(0.5f, 1f);
            var topLeft = new Vector2(0f, 1f);

            townBar = BuildWing(root, "Town", new Vector2(-504f, -30f), "はじまりのまち", HudStyle.Town, true, out townValue);
            coreBar = BuildWing(root, "Core", new Vector2(64f, -30f), "魔王城コア", HudStyle.Core, false, out coreValue);

            if (theme == HudTheme.Hero)
            {
                var tab = HudStyle.Place(root, "DemonKingHp", top, topLeft, new Vector2(284f, -106f), new Vector2(212f, 40f));
                HudStyle.DropShadow(tab, HudShape.Kind.RoundRect, pal.Shadow, 12f, 4f);
                HudStyle.Shape(tab, "Felt", HudShape.Kind.RoundRect, pal.Felt, 12f, HudStyle.FeltTexture);
                dkHpText = HudStyle.AddText(HudStyle.Filled(tab, "Text"), "", 20, pal.Light, TextAnchor.MiddleCenter);
                dkHpTab = tab.gameObject;
            }

            var clock = HudStyle.Place(root, "Clock", top, top, new Vector2(0f, -16f), new Vector2(144f, 144f));
            HudStyle.Felt(clock, pal, HudShape.Kind.Circle, 0f, 7f, 6f);
            clockLabel = HudStyle.Label(clock, "Label", top, top, new Vector2(0f, -32f), new Vector2(120f, 26f), "残り", 20, theme == HudTheme.Hero ? pal.Stitch : pal.Light, TextAnchor.MiddleCenter);
            clockTime = HudStyle.Label(clock, "Time", top, top, new Vector2(0f, -58f), new Vector2(140f, 50f), "--:--", 40, theme == HudTheme.Hero ? Color.white : pal.Light, TextAnchor.MiddleCenter, 6f, pal.Outline);

            var ribbon = HudStyle.Place(root, "Ribbon", top, top, new Vector2(0f, -138f), new Vector2(200f, 44f));
            ribbon.localEulerAngles = new Vector3(0f, 0f, -2f);
            HudStyle.DropShadow(ribbon, HudShape.Kind.Ribbon, pal.Shadow, 0f, 4f);
            HudStyle.Shape(ribbon, "Ribbon", HudShape.Kind.Ribbon, pal.Ribbon);
            ribbonText = HudStyle.AddText(HudStyle.Filled(ribbon, "Text"), "", 23, theme == HudTheme.Hero ? Color.white : pal.Light, TextAnchor.MiddleCenter);
        }

        /// やぶれた紙の帯。上の段に名前と数値、下の段にバー。左右対称に置くので、左側は名前と数値の並びも逆にする。
        private HudBar BuildWing(RectTransform root, string name, Vector2 position, string title, Color color, bool left, out Text value)
        {
            var wing = HudStyle.Place(root, name, new Vector2(0.5f, 1f), new Vector2(0f, 1f), position, new Vector2(440f, 76f));
            HudStyle.Paper(wing, pal, HudShape.Kind.Torn, 0f, left ? 3 : 11);

            float padNear = 40f, padFar = 22f; // 時計側を広くあける
            float padLeft = left ? padFar : padNear, padRight = left ? padNear : padFar;
            var titleAnchor = left ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
            var valueAnchor = left ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            var topLeft = new Vector2(0f, 1f);

            var row = HudStyle.Place(wing, "Row", topLeft, topLeft, new Vector2(padLeft, -8f), new Vector2(440f - padLeft - padRight, 30f));
            HudStyle.AddText(HudStyle.Filled(row, "Title"), title, 21, pal.Ink, titleAnchor);
            value = HudStyle.AddText(HudStyle.Filled(row, "Value"), "", 20, pal.Ink, valueAnchor);

            var barRt = HudStyle.Place(wing, "Bar", topLeft, topLeft, new Vector2(padLeft, -42f), new Vector2(440f - padLeft - padRight, 20f));
            return HudBar.Create(barRt, color, theme == HudTheme.Hero ? pal.Outline : pal.Ink, left);
        }

        private void Update()
        {
            if (Time.unscaledTime >= nextRebind)
            {
                nextRebind = Time.unscaledTime + RebindInterval;
                Rebind();
            }
            UpdateHealth(town, townBar, townValue, ref shownTown);
            UpdateHealth(core, coreBar, coreValue, ref shownCore);
            UpdateDemonKingHp();
            UpdateClock();
        }

        private void Rebind()
        {
            if (town == null) town = FindHealth("StartTown");
            if (core == null) core = FindHealth("DemonCastle");
            if (dkHealth == null || dkLevel == null)
            {
                var dk = GameObject.FindWithTag("DemonKing");
                if (dk != null)
                {
                    dkHealth = dk.GetComponent<HealthComponent>();
                    dkLevel = dk.GetComponent<LevelComponent>();
                }
            }
        }

        private static HealthComponent FindHealth(string tag)
        {
            var go = GameObject.FindWithTag(tag);
            if (go == null) return null;
            var hc = go.GetComponent<HealthComponent>();
            return hc != null ? hc : go.GetComponentInChildren<HealthComponent>();
        }

        private static void UpdateHealth(HealthComponent hc, HudBar bar, Text value, ref int shown)
        {
            if (hc == null || hc.CurrentHP == shown) return;
            shown = hc.CurrentHP;
            bar.Set(hc.MaxHP > 0 ? (float)hc.CurrentHP / hc.MaxHP : 0f);
            value.text = HudStyle.Thousands(Mathf.Max(0, hc.CurrentHP)) + " / " + HudStyle.Thousands(hc.MaxHP);
        }

        private void UpdateDemonKingHp()
        {
            if (dkHpTab == null) return;
            bool show = dkHealth != null;
            if (dkHpTab.activeSelf != show) dkHpTab.SetActive(show);
            if (!show || (dkHealth.CurrentHP == shownDkHp && dkHealth.MaxHP == shownDkMax)) return;
            shownDkHp = dkHealth.CurrentHP;
            shownDkMax = dkHealth.MaxHP;
            dkHpText.text = "魔王 HP " + Mathf.Max(0, shownDkHp) + " / " + shownDkMax;
        }

        private void UpdateClock()
        {
            var m = MatchManager.Instance;
            string label, ribbon;
            int seconds = -1;
            if (m == null)
            {
                label = "";
                ribbon = "";
            }
            else
            {
                switch (m.CurrentState)
                {
                    case MatchManager.MatchState.WaitingForStart:
                        label = "開始前"; ribbon = "まもなく開始"; break;
                    case MatchManager.MatchState.DemonPrepPhase:
                        label = "準備"; ribbon = "魔王準備中"; seconds = Mathf.CeilToInt(m.RemainingInMatch); break;
                    case MatchManager.MatchState.Finished:
                        label = "終了"; ribbon = "試合終了"; seconds = 0; break;
                    default:
                        label = "残り"; ribbon = InMatchRibbon(m); seconds = Mathf.CeilToInt(m.RemainingInMatch); break;
                }
            }

            if (label != shownClockLabel) { shownClockLabel = label; clockLabel.text = label; }
            if (ribbon != shownRibbon) { shownRibbon = ribbon; ribbonText.text = ribbon; }
            if (seconds != shownSeconds)
            {
                shownSeconds = seconds;
                clockTime.text = seconds < 0 ? "--:--" : HudStyle.Clock(seconds);
            }
        }

        private string InMatchRibbon(MatchManager m)
        {
            if (dkLevel == null) return "";
            if (theme == HudTheme.Hero) return "魔王 Lv " + dkLevel.CurrentLevel;
            if (dkLevel.IsMaxLevel || dkLevel.SecondsPerLevel <= 0f) return "Lv MAX";
            float next = dkLevel.SecondsPerLevel - Mathf.Repeat(m.ElapsedInMatch, dkLevel.SecondsPerLevel);
            return "次のLv " + HudStyle.ShortClock(next);
        }
    }
}
