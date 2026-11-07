using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    /// 試合の勝敗の画面（試合の最後とリザルトの両方で使う）。HUD と同じ布と紙の手作り風。
    ///  - 勇者チームの勝利: 紫の布のリボンに金の縫い目、後ろで金色の光の筋が回り、金の星が舞い落ちる。
    ///  - 魔王軍の勝利: 黒い布に暗い赤の縫い目、赤黒い光の筋、火の粉が舞い上がる。
    /// 自分の側が勝ったか負けたかを、リボンの上の札に出す。
    public class VictoryScreen : MonoBehaviour
    {
        private const int Particles = 36;

        private static Texture2D raysTexture;
        private static Sprite raysSprite, starSprite;

        private RectTransform rays;
        private RectTransform card;
        private CanvasGroup group;
        private bool heroesWon;
        private float shownAt;
        private readonly List<(RectTransform rt, Vector2 velocity, float spin, float sway)> bits = new List<(RectTransform, Vector2, float, float)>();

        public RectTransform Content { get; private set; }

        public static string Title(MatchManager.Winner w) =>
            w == MatchManager.Winner.Heroes ? "勇者チームの勝利！" : w == MatchManager.Winner.DemonKing ? "魔王軍の勝利" : "引き分け";

        public static string Subtitle(MatchManager.Winner w, MatchManager.EndCause cause)
        {
            switch (cause)
            {
                case MatchManager.EndCause.DemonKingDefeated: return "魔王は討たれ、世界に光が戻った";
                case MatchManager.EndCause.CastleCoreDestroyed: return "魔王城のコアは砕かれ、闇の力は消え去った";
                case MatchManager.EndCause.TownCoreDestroyed: return "はじまりのまちは闇に呑まれた";
                case MatchManager.EndCause.TimeUp: return "時は満ち、世界は魔王のものとなった";
            }
            return w == MatchManager.Winner.Heroes ? "魔王は討たれた" : "はじまりのまちは闇に呑まれた";
        }

        /// 画面いっぱいの勝敗の画面を作る。sortingOrder はほかの UI より手前にする値。
        public static VictoryScreen Create(MatchManager.Winner winner, MatchManager.EndCause cause, int sortingOrder = 120, bool dimBackground = true)
        {
            var go = new GameObject("VictoryScreen", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var screen = go.AddComponent<VictoryScreen>();
            screen.Build(winner, cause, dimBackground);
            return screen;
        }

        private void Build(MatchManager.Winner winner, MatchManager.EndCause cause, bool dim)
        {
            heroesWon = winner == MatchManager.Winner.Heroes;
            var p = heroesWon ? HudStyle.HeroPalette : HudStyle.DemonPalette;
            Color glow = heroesWon ? new Color(1f, 0.84f, 0.42f, 1f) : new Color(0.85f, 0.12f, 0.12f, 1f);
            var root = (RectTransform)transform;
            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            if (dim)
            {
                var shade = HudStyle.Filled(root, "Shade");
                var img = HudStyle.AddGraphic<Image>(shade);
                img.color = heroesWon ? new Color(0.06f, 0.03f, 0.12f, 0.6f) : new Color(0.05f, 0f, 0.02f, 0.72f);
                img.raycastTarget = false;
            }

            rays = HudStyle.Place(root, "Rays", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1900f, 1900f));
            var raysImage = HudStyle.AddGraphic<Image>(rays);
            raysImage.sprite = RaysSprite();
            raysImage.color = new Color(glow.r, glow.g, glow.b, heroesWon ? 0.42f : 0.5f);
            raysImage.raycastTarget = false;

            var bitsRoot = HudStyle.Filled(root, "Bits");
            for (int i = 0; i < Particles; i++)
            {
                var rt = HudStyle.Place(bitsRoot, "Bit", new Vector2(Random.value, heroesWon ? 1.05f : -0.05f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * Random.Range(10f, 24f));
                rt.anchoredPosition = new Vector2(0f, (heroesWon ? 1f : -1f) * Random.Range(0f, 1100f) * -1f);
                var img = HudStyle.AddGraphic<Image>(rt);
                img.sprite = StarSprite();
                img.color = heroesWon ? Color.Lerp(glow, Color.white, Random.value * 0.5f) : Color.Lerp(glow, new Color(1f, 0.55f, 0.15f), Random.value);
                img.raycastTarget = false;
                Vector2 v = new Vector2(Random.Range(-20f, 20f), (heroesWon ? -1f : 1f) * Random.Range(70f, 160f));
                bits.Add((rt, v, Random.Range(-160f, 160f), Random.Range(0f, 6.28f)));
            }

            card = HudStyle.Place(root, "Card", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1200f, 420f));
            Content = card;

            // 自分の側の勝ち負けの札
            var role = GameSession.Instance != null ? GameSession.Instance.CurrentRole : GameSession.PlayerRole.Hero;
            bool mine = role == GameSession.PlayerRole.DemonKing ? winner == MatchManager.Winner.DemonKing : winner == MatchManager.Winner.Heroes;
            var tag = HudStyle.Place(card, "Tag", new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(300f, 76f));
            tag.localRotation = Quaternion.Euler(0f, 0f, 3f);
            var tagPaper = HudStyle.Paper(tag, p, HudShape.Kind.Torn, 0f, 7);
            tagPaper.color = mine ? p.Paper : new Color(0.72f, 0.7f, 0.72f);
            HudStyle.AddText(HudStyle.Filled(tag, "Text"), winner == MatchManager.Winner.Draw ? "引き分け" : mine ? "勝利" : "敗北", 46,
                mine ? (heroesWon ? HudStyle.Hex(0x7a4a00) : p.Ribbon) : HudStyle.Hex(0x3a3540), TextAnchor.MiddleCenter);

            // 大きなリボン
            var ribbon = HudStyle.Place(card, "Ribbon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(1180f, 170f));
            HudStyle.DropShadow(ribbon, HudShape.Kind.Ribbon, p.Shadow, 0f, 9f);
            var felt = HudStyle.Shape(ribbon, "Felt", HudShape.Kind.Ribbon, p.Felt, 0f, HudStyle.FeltTexture);
            felt.notch = 46f;
            var stitch = HudStyle.Stitch(ribbon, heroesWon ? glow : p.Stitch, false, 0f, 12f);
            HudStyle.AddText(HudStyle.Filled(ribbon, "Title"), Title(winner), 84, heroesWon ? HudStyle.Hex(0xfff2c4) : HudStyle.Hex(0xf2d9cf),
                TextAnchor.MiddleCenter, 5f, p.Outline);

            // 一言を書いた紙
            var note = HudStyle.Place(card, "Note", new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 70f), new Vector2(860f, 84f));
            note.localRotation = Quaternion.Euler(0f, 0f, -1.2f);
            HudStyle.Paper(note, p, HudShape.Kind.Torn, 0f, 3);
            HudStyle.AddText(HudStyle.Filled(note, "Text"), Subtitle(winner, cause), 34, p.Ink, TextAnchor.MiddleCenter);

            shownAt = Time.unscaledTime;
            card.localScale = Vector3.one * 1.5f;
        }

        private void Update()
        {
            float t = Time.unscaledTime - shownAt;
            float dt = Time.unscaledDeltaTime;
            group.alpha = Mathf.Clamp01(t / 0.35f);
            // ぽんと弾んで落ち着く
            float k = Mathf.Clamp01(t / 0.55f);
            float s = k < 1f ? 1f + 0.5f * Mathf.Pow(1f - k, 3f) * Mathf.Cos(k * 9f) : 1f;
            if (card != null) card.localScale = Vector3.one * s;
            if (rays != null)
            {
                rays.localRotation = Quaternion.Euler(0f, 0f, -t * 9f);
                rays.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(t * 1.7f));
            }
            for (int i = 0; i < bits.Count; i++)
            {
                var (rt, v, spin, sway) = bits[i];
                Vector2 pos = rt.anchoredPosition + new Vector2(v.x + Mathf.Sin(t * 1.6f + sway) * 30f, v.y) * dt;
                if (heroesWon && pos.y < -1200f) pos.y = 40f;
                if (!heroesWon && pos.y > 1200f) pos.y = -40f;
                rt.anchoredPosition = pos;
                rt.localRotation = Quaternion.Euler(0f, 0f, rt.localEulerAngles.z + spin * dt);
            }
        }

        // ---- 実行時に作る絵 ----

        private static Sprite RaysSprite()
        {
            if (raysSprite != null) return raysSprite;
            const int n = 256;
            raysTexture = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "VictoryRays", wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                    float a = Mathf.Atan2(dy, dx);
                    float wedge = Mathf.Cos(a * 16f) * 0.5f + 0.5f;
                    wedge = Mathf.SmoothStep(0.35f, 0.75f, wedge);
                    float fade = Mathf.Clamp01(1f - r) * Mathf.Clamp01(r * 5f);
                    float core = Mathf.Clamp01(1f - r * 2.2f) * 0.6f;
                    px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(wedge * fade + core));
                }
            }
            raysTexture.SetPixels(px);
            raysTexture.Apply(true);
            raysSprite = Sprite.Create(raysTexture, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            return raysSprite;
        }

        /// 4つの角のある星（きらめき）。
        public static Sprite StarSprite()
        {
            if (starSprite != null) return starSprite;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "VictoryStar", wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = Mathf.Abs((x + 0.5f) / n - 0.5f) * 2f, dy = Mathf.Abs((y + 0.5f) / n - 0.5f) * 2f;
                    float star = Mathf.Pow(dx, 0.5f) + Mathf.Pow(dy, 0.5f);
                    float a = Mathf.Clamp01((1f - star) * 6f);
                    float glow = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)) * 0.35f;
                    px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a + glow));
                }
            }
            tex.SetPixels(px);
            tex.Apply(true);
            starSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            return starSprite;
        }
    }
}
