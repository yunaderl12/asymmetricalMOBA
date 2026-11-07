using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    /// ロビー画面（タイトル・勇者ロビー・マッチロビー・魔王ロビー）の土台と部品。
    /// 試合中HUDと同じ布と紙（HudStyle）で組む。座標はデザイン案と同じ 1920x1080 の「舞台」の左上から測る（y は下向き）。
    public static class LobbyStyle
    {
        public const float StageWidth = 1920f;
        public const float StageHeight = 1080f;

        public static readonly Color TapeTeal = new Color(90f / 255f, 190f / 255f, 190f / 255f, 0.8f);
        public static readonly Color TapePink = new Color(233f / 255f, 92f / 255f, 120f / 255f, 0.8f);
        public static readonly Color TapeYellow = new Color(245f / 255f, 184f / 255f, 41f / 255f, 0.8f);
        public static readonly Color TapeBlue = new Color(120f / 255f, 120f / 255f, 220f / 255f, 0.8f);
        public static readonly Color[] Tapes = { TapeTeal, TapePink, TapeYellow, TapeBlue };

        public static readonly Color Rug = HudStyle.Hex(0xe9d6ab);
        public static readonly Color RugStitch = HudStyle.Hex(0xb99a62);
        public static readonly Color SoftPaper = HudStyle.Hex(0xfff6df);
        public static readonly Color LockedPaper = HudStyle.Hex(0xefe0bb);
        public static readonly Color Note = HudStyle.Hex(0xfff3a0);
        public static readonly Color Divider = HudStyle.Hex(0xc9b48a);
        public static readonly Color SubInk = HudStyle.Brown;
        public static readonly Color Spotlight = new Color(1f, 0.886f, 0.627f, 0.28f);

        private static Texture2D glow, vignette;

        // ---- 画面の土台 ----

        /// ロビー用のキャンバスを作り、シーンに置いてある古いキャンバスを隠す。返り値は 1920x1080 の舞台。
        /// 背景の布は画面いっぱいに広げ、舞台は真ん中に置く。
        public static RectTransform CreateScreen(string name, HudStyle.Palette p, out RectTransform background)
        {
            // シーンに置いた古いUIだけ隠す。消えずに残るもの（コンソールなど）は別のシーンにいるので触らない。
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var old in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (old != null && old.isRootCanvas && old.name != name && old.gameObject.scene == scene) old.gameObject.SetActive(false);
            }
            EnsureEventSystem();

            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(StageWidth, StageHeight);
            scaler.matchWidthOrHeight = 0.5f;

            background = HudStyle.Node(go.transform, "Background");
            HudStyle.Stretch(background);
            var felt = HudStyle.Shape(background, "Felt", HudShape.Kind.RoundRect, p.Felt, 0f, HudStyle.FeltTexture);
            felt.raycastTarget = true; // 後ろのものを押せないようにする

            var stage = HudStyle.Node(go.transform, "Stage");
            stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(0.5f, 0.5f);
            stage.sizeDelta = new Vector2(StageWidth, StageHeight);
            stage.anchoredPosition = Vector2.zero;
            return stage;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null || Object.FindFirstObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        }

        /// 舞台の左上から (x, y) の位置に w x h の箱を置く。
        public static RectTransform At(Transform parent, string name, float x, float y, float w, float h)
        {
            var topLeft = new Vector2(0f, 1f);
            return HudStyle.Place(parent, name, topLeft, topLeft, new Vector2(x, -y), new Vector2(w, h));
        }

        /// 中心 (cx, cy) を舞台の左上から測って置く。
        public static RectTransform AtCenter(Transform parent, string name, float cx, float cy, float w, float h)
        {
            return HudStyle.Place(parent, name, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(cx, -cy), new Vector2(w, h));
        }

        public static void Tilt(RectTransform rt, float degrees)
        {
            rt.localRotation = Quaternion.Euler(0f, 0f, degrees);
        }

        /// 柔らかい光のだまり（スポットライト）。舞台の座標で中心と大きさを指定する。
        public static RawImage Glow(Transform parent, float cx, float cy, float w, float h, Color color)
        {
            var rt = AtCenter(parent, "Glow", cx, cy, w, h);
            var image = HudStyle.AddGraphic<RawImage>(rt);
            image.texture = GlowTexture;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// 画面の四隅を暗くする。
        public static RawImage Vignette(RectTransform background, Color color)
        {
            var rt = HudStyle.Filled(background, "Vignette");
            var image = HudStyle.AddGraphic<RawImage>(rt);
            image.texture = VignetteTexture;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Texture2D GlowTexture => glow != null ? glow : (glow = RadialTexture("LobbyGlow", false));
        private static Texture2D VignetteTexture => vignette != null ? vignette : (vignette = RadialTexture("LobbyVignette", true));

        private static Texture2D RadialTexture(string name, bool invert)
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = invert ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.45f, d)) : 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(d));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        // ---- 部品 ----

        public static Text Text(Transform parent, string name, float x, float y, float w, float h, string text, int size, Color color,
            TextAnchor align = TextAnchor.MiddleLeft, float outline = 0f, Color outlineColor = default)
        {
            return HudStyle.AddText(At(parent, name, x, y, w, h), text, size, color, align, outline, outlineColor);
        }

        /// 折り返す文章。
        public static Text Paragraph(Transform parent, string name, float x, float y, float w, float h, string text, int size, Color color)
        {
            var t = Text(parent, name, x, y, w, h, text, size, color, TextAnchor.UpperLeft);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.lineSpacing = 1.15f;
            return t;
        }

        /// 左上の画面名のリボン。
        public static Text ScreenTitle(Transform stage, string text, HudStyle.Palette p, float width = 330f)
        {
            var rt = At(stage, "ScreenTitle", 40f, 36f, width, 64f);
            HudStyle.DropShadow(rt, HudShape.Kind.Ribbon, p.Shadow, 0f, 5f);
            var ribbon = HudStyle.Shape(rt, "Ribbon", HudShape.Kind.Ribbon, p.Ribbon);
            ribbon.notch = 22f;
            return HudStyle.AddText(HudStyle.Filled(rt, "Text"), text, 30, p.Light, TextAnchor.MiddleCenter);
        }

        /// 紙のシート。tapes 本のマスキングテープを上のふちに貼る。
        public static HudShape PaperSheet(RectTransform rt, HudStyle.Palette p, float radius = 6f, int tapes = 2, int seed = 0, float shadow = 7f)
        {
            var paper = HudStyle.Paper(rt, p, HudShape.Kind.RoundRect, radius, seed, shadow);
            float w = rt.sizeDelta.x;
            if (tapes >= 1) Tape(rt, 60f, -14f, 110f, 30f, Tapes[(seed + 1) % Tapes.Length], -4f);
            if (tapes >= 2) Tape(rt, w - 170f, -12f, 110f, 30f, Tapes[seed % Tapes.Length], 3f);
            return paper;
        }

        /// マスキングテープ。親の左上から測る。
        public static HudShape Tape(Transform parent, float x, float y, float w, float h, Color color, float degrees)
        {
            var rt = At(parent, "Tape", x, y, w, h);
            Tilt(rt, degrees);
            var tape = HudStyle.Shape(rt, "Tape", HudShape.Kind.RoundRect, color, 1f);
            return tape;
        }

        /// 布の板。縫い目と影つき。返り値は布の本体。
        public static HudShape FeltPlate(RectTransform rt, HudStyle.Palette p, float radius = 16f, float stitchInset = 6f, float shadow = 5f)
        {
            return HudStyle.Felt(rt, p, HudShape.Kind.RoundRect, radius, stitchInset, shadow);
        }

        /// 選ばれている印の金色のふち。対象の後ろに少し大きく敷く。
        public static HudShape Ring(RectTransform target, HudShape.Kind kind, float radius, float outset = 5f)
        {
            var ring = HudStyle.Shape(target, "Ring", kind, HudStyle.Gold, radius + outset);
            HudStyle.Stretch(ring.rectTransform, outset);
            ring.rectTransform.SetAsFirstSibling();
            return ring;
        }

        /// ボタンにする。押せる面（target）に当たり判定をつけ、マウスを乗せると少し大きくなる。
        public static Button MakeButton(RectTransform rt, Graphic target, System.Action onClick)
        {
            target.raycastTarget = true;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = target;
            button.transition = Selectable.Transition.None;
            var nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            if (onClick != null) button.onClick.AddListener(() => onClick());
            rt.gameObject.AddComponent<LobbyPress>();
            return button;
        }

        /// 当たり判定だけの透明な面を足して、箱全体をボタンにする。
        public static Button MakeHitButton(RectTransform rt, System.Action onClick)
        {
            var hit = HudStyle.Shape(rt, "Hit", HudShape.Kind.RoundRect, new Color(1f, 1f, 1f, 0f), 0f);
            return MakeButton(rt, hit, onClick);
        }

        /// 布の角丸ボタン。
        public static Button FeltButton(RectTransform rt, HudStyle.Palette p, string text, int fontSize, System.Action onClick, float radius = 16f)
        {
            var body = FeltPlate(rt, p, radius, 6f, 5f);
            HudStyle.AddText(HudStyle.Filled(rt, "Label"), text, fontSize, p.Light, TextAnchor.MiddleCenter, 5f, p.Outline);
            return MakeButton(rt, body, onClick);
        }

        /// 紙の小さなボタン。
        public static Button PaperButton(RectTransform rt, HudStyle.Palette p, string text, int fontSize, System.Action onClick)
        {
            var body = HudStyle.Paper(rt, p, HudShape.Kind.RoundRect, 10f, 3, 4f);
            HudStyle.AddText(HudStyle.Filled(rt, "Label"), text, fontSize, p.Ink, TextAnchor.MiddleCenter);
            return MakeButton(rt, body, onClick);
        }

        /// 大きなリボンのボタン（マッチ開始・準備完了）。sub は下の小さな文字（なければ空）。
        public static Button RibbonButton(RectTransform rt, HudStyle.Palette p, Color color, string text, int fontSize, System.Action onClick,
            out Text label, out Text sub, string subText = null)
        {
            HudStyle.DropShadow(rt, HudShape.Kind.Ribbon, p.Shadow, 0f, 7f);
            var ribbon = HudStyle.Shape(rt, "Ribbon", HudShape.Kind.Ribbon, color);
            ribbon.notch = 26f;
            bool hasSub = !string.IsNullOrEmpty(subText);
            var labelRt = HudStyle.Filled(rt, "Label");
            if (hasSub) labelRt.offsetMin = new Vector2(0f, rt.sizeDelta.y * 0.28f);
            label = HudStyle.AddText(labelRt, text, fontSize, Color.white, TextAnchor.MiddleCenter, 6f, p.Outline);
            sub = null;
            if (hasSub)
            {
                var subRt = HudStyle.Filled(rt, "Sub");
                subRt.offsetMax = new Vector2(0f, -rt.sizeDelta.y * 0.66f);
                sub = HudStyle.AddText(subRt, subText, 20, Color.white, TextAnchor.MiddleCenter);
            }
            return MakeButton(rt, ribbon, onClick);
        }

        /// キーの札（Enter など）。
        public static void KeyCap(RectTransform rt, HudStyle.Palette p, string key)
        {
            HudStyle.Paper(rt, p, HudShape.Kind.RoundRect, 10f, 5, 4f);
            HudStyle.AddText(HudStyle.Filled(rt, "Key"), key, 22, p.Ink, TextAnchor.MiddleCenter);
        }

        /// 丸い布の札に1文字。魔物・パッシブなど絵のないものの印。
        public static Text FeltToken(RectTransform rt, Color fill, Color stitch, string glyph, int fontSize, HudStyle.Palette p, float shadow = 4f)
        {
            if (shadow > 0f) HudStyle.DropShadow(rt, HudShape.Kind.Circle, p.Shadow, 0f, shadow);
            HudStyle.Shape(rt, "Felt", HudShape.Kind.Circle, fill, 0f, HudStyle.FeltTexture);
            HudStyle.Stitch(rt, stitch, true, 0f, Mathf.Max(4f, rt.sizeDelta.x * 0.06f));
            return HudStyle.AddText(HudStyle.Filled(rt, "Glyph"), glyph, fontSize, Color.white, TextAnchor.MiddleCenter, 4f, p.Outline);
        }

        /// 白いふちの丸い肖像（シール）。
        public static Image Sticker(RectTransform rt, Sprite sprite, Color fallback, HudStyle.Palette p)
        {
            return HudStyle.Portrait(rt, sprite, fallback, Mathf.Max(3f, rt.sizeDelta.x * 0.05f), p.Shadow);
        }

        /// 試合中HUDと同じ形の丸い時計。value に時刻を入れる。
        public static Text Clock(Transform stage, HudStyle.Palette p, string caption)
        {
            var rt = AtCenter(stage, "Clock", StageWidth * 0.5f, 92f, 144f, 144f);
            HudStyle.DropShadow(rt, HudShape.Kind.Circle, p.Shadow, 0f, 6f);
            var ring = HudStyle.Shape(rt, "Rim", HudShape.Kind.Circle, p.Track);
            HudStyle.Stretch(ring.rectTransform, 4f);
            HudStyle.Felt(rt, p, HudShape.Kind.Circle, 0f, 7f, 0f);
            HudStyle.Label(rt, "Caption", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 26f), new Vector2(130f, 24f),
                caption, 18, p.Light, TextAnchor.MiddleCenter);
            return HudStyle.Label(rt, "Time", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(140f, 48f),
                "", 38, Color.white, TextAnchor.MiddleCenter, 6f, p.Outline);
        }

        /// 点線の区切り。
        public static void DashedLine(Transform parent, float x, float y, float w, Color color)
        {
            var rt = At(parent, "Dashes", x, y, w, 3f);
            for (float dx = 0f; dx < w; dx += 22f)
            {
                var d = HudStyle.Place(rt, "Dash", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(dx, 0f), new Vector2(Mathf.Min(12f, w - dx), 3f));
                HudStyle.Shape(d, "Dash", HudShape.Kind.RoundRect, color, 1.5f);
            }
        }

        /// 子どもをすべて消す。
        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) Object.Destroy(t.GetChild(i).gameObject);
        }

        /// BOT の名前（BotHero2、BotDemonKing）を「ボット2」「ボット魔王」と短く出す。
        public static string BotLabel(string botName)
        {
            if (string.IsNullOrEmpty(botName)) return "ボット";
            if (botName.Contains("DemonKing")) return "ボット魔王";
            string digits = "";
            foreach (char c in botName) if (char.IsDigit(c)) digits += c;
            return botName.StartsWith("Bot") ? "ボット" + digits : botName;
        }
    }

    /// ロビーのボタンの手ごたえ。マウスを乗せると少し大きく、押すと少し縮む。
    public class LobbyPress : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public float hoverScale = 1.04f;
        public float pressScale = 0.97f;

        private bool hover, down;
        private Vector3 baseScale = Vector3.one;

        private void Awake() { baseScale = transform.localScale; }

        private void OnDisable()
        {
            hover = down = false;
            transform.localScale = baseScale;
        }

        public void OnPointerEnter(PointerEventData e) { hover = true; }
        public void OnPointerExit(PointerEventData e) { hover = false; down = false; }
        public void OnPointerDown(PointerEventData e) { down = true; }
        public void OnPointerUp(PointerEventData e) { down = false; }

        private void Update()
        {
            var selectable = GetComponent<Selectable>();
            bool on = selectable == null || selectable.interactable;
            float target = !on ? 1f : down ? pressScale : hover ? hoverScale : 1f;
            float s = Mathf.MoveTowards(transform.localScale.x / Mathf.Max(0.0001f, baseScale.x), target, Time.unscaledDeltaTime * 1.5f);
            transform.localScale = baseScale * s;
        }
    }
}
