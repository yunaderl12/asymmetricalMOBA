using UnityEngine;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    public enum HudTheme { Hero, Demon }

    /// 試合中HUDの色・文字・材質と、部品を組み立てる関数。
    /// 見た目の決まりは docs/game-design.md の「UIデザインの方針」（布と紙の手作り風。勇者は紫、魔王は黒と暗い赤）。
    public static class HudStyle
    {
        public struct Palette
        {
            public Color Felt, Stitch, Paper, Ribbon, Outline, Light, Track, Shadow, Ink;
        }

        public static readonly Color Hp = Hex(0xef5a55);
        public static readonly Color HpLow = Hex(0xff3b3b);
        public static readonly Color MpPreview = Hex(0xe6ddff);
        public static readonly Color Exp = Hex(0x76dcb8);
        public static readonly Color Mp = Hex(0xa98bff);
        public static readonly Color Town = Hex(0x2aa893);
        public static readonly Color Core = Hex(0x9a45d6);
        public static readonly Color Gold = Hex(0xffc93c);
        public static readonly Color Danger = Hex(0xb3202c);
        public static readonly Color CostPill = Hex(0xffd968);
        public static readonly Color CostText = Hex(0x6a4a00);
        /// スキルカードの消費MPの札。MPバーと同じ紫系。
        public static readonly Color MpPill = Hex(0xd9ccff);
        public static readonly Color MpPillText = Hex(0x3b2470);
        public static readonly Color Brown = Hex(0x6a4a2e);
        public static readonly Color DownFelt = Hex(0x4a4552);

        public static readonly Palette HeroPalette = new Palette
        {
            Felt = Hex(0x3a2a5e), Stitch = Hex(0xf3e3be), Paper = Hex(0xf7e9c8), Ribbon = Hex(0xc9303a),
            Outline = Hex(0x2a1b3d), Light = Hex(0xf7e9c8), Track = Hex(0x1e1433),
            Shadow = new Color(0.12f, 0.06f, 0.2f, 0.45f), Ink = Hex(0x2b1b2e),
        };

        public static readonly Palette DemonPalette = new Palette
        {
            Felt = Hex(0x1b1116), Stitch = Hex(0xa8322e), Paper = Hex(0xeadfc8), Ribbon = Hex(0x8e1b22),
            Outline = Hex(0x140b10), Light = Hex(0xeadfc8), Track = Hex(0x000000),
            Shadow = new Color(0f, 0f, 0f, 0.6f), Ink = Hex(0x1b1116),
        };

        public static Palette For(HudTheme theme) => theme == HudTheme.Demon ? DemonPalette : HeroPalette;

        public static HudTheme CurrentTheme =>
            GameSession.Instance != null && GameSession.Instance.CurrentRole == GameSession.PlayerRole.DemonKing ? HudTheme.Demon : HudTheme.Hero;

        public static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f, 1f);

        // ---- 文字と材質 ----

        private static Font font;
        private static Texture2D felt, paper, hatch;

        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.Load<Font>("Fonts/MochiyPopOne-Regular");
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public static Texture2D FeltTexture => felt != null ? felt : (felt = LoadTile("UI/HudFelt"));
        public static Texture2D PaperTexture => paper != null ? paper : (paper = LoadTile("UI/HudPaper"));

        /// MPの消費予告に使う斜線。白と透明の縞で、上に塗る色を掛け合わせる。
        public static Texture2D HatchTexture
        {
            get
            {
                if (hatch != null) return hatch;
                const int size = 20;
                hatch = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, name = "HudHatch" };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                        pixels[y * size + x] = (x + y) % 10 < 5 ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                hatch.SetPixels32(pixels);
                hatch.Apply();
                return hatch;
            }
        }

        private static Texture2D LoadTile(string path)
        {
            var tex = Resources.Load<Texture2D>(path);
            if (tex != null) tex.wrapMode = TextureWrapMode.Repeat;
            return tex;
        }

        // ---- 置き場所 ----

        private static Canvas canvas;

        /// 試合中HUDの専用キャンバス。ほかのUI（リスポーン、リザルト、コンソールなど）より奥に描く。
        public static Canvas Canvas
        {
            get
            {
                if (canvas != null) return canvas;
                var go = new GameObject("HudCanvas", typeof(Canvas), typeof(CanvasScaler));
                canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = -1;
                var scaler = go.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                return canvas;
            }
        }

        public static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Place(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var rt = Node(parent, name);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        /// 親いっぱいに広げる。outset だけ外へはみ出す。
        public static void Stretch(RectTransform rt, float outset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(-outset, -outset);
            rt.offsetMax = new Vector2(outset, outset);
        }

        // ---- 部品 ----

        /// 描画部品を付ける。Unity 6 の UGUI では Graphic を付けても CanvasRenderer が自動で付かないので、先に付ける。
        public static T AddGraphic<T>(RectTransform rt) where T : Graphic
        {
            if (rt.GetComponent<CanvasRenderer>() == null) rt.gameObject.AddComponent<CanvasRenderer>();
            return rt.gameObject.AddComponent<T>();
        }

        /// 親いっぱいに広がる空のノード（文字を親の範囲に収めるときなど）。
        public static RectTransform Filled(Transform parent, string name)
        {
            var rt = Node(parent, name);
            Stretch(rt);
            return rt;
        }

        public static HudShape Shape(Transform parent, string name, HudShape.Kind kind, Color color, float radius = 12f, Texture texture = null)
        {
            var rt = Node(parent, name);
            Stretch(rt);
            var shape = AddGraphic<HudShape>(rt);
            shape.kind = kind;
            shape.radius = radius;
            shape.texture = texture;
            shape.color = color;
            shape.raycastTarget = false;
            return shape;
        }

        /// 下へずらした影。部品が板のように少し浮いて見える。
        public static HudShape DropShadow(Transform parent, HudShape.Kind kind, Color color, float radius, float depth = 5f, int seed = 0)
        {
            var shadow = Shape(parent, "Shadow", kind, color, radius);
            shadow.seed = seed;
            shadow.rectTransform.offsetMin = new Vector2(0f, -depth);
            shadow.rectTransform.offsetMax = new Vector2(0f, -depth);
            return shadow;
        }

        /// 影・布・縫い目の順に重ねたフェルト。返り値は布の本体（色を変えるとき用）。
        public static HudShape Felt(Transform parent, Palette p, HudShape.Kind kind, float radius, float stitchInset = 7f, float shadowDepth = 5f)
        {
            if (shadowDepth > 0f) DropShadow(parent, kind, p.Shadow, radius, shadowDepth);
            var body = Shape(parent, "Felt", kind, p.Felt, radius, FeltTexture);
            if (stitchInset > 0f) Stitch(parent, p.Stitch, kind == HudShape.Kind.Circle, Mathf.Max(0f, radius - stitchInset + 1f), stitchInset);
            return body;
        }

        public static HudStitch Stitch(Transform parent, Color color, bool circle, float radius, float inset)
        {
            var rt = Node(parent, "Stitch");
            Stretch(rt, -inset);
            var stitch = AddGraphic<HudStitch>(rt);
            stitch.circle = circle;
            stitch.radius = radius;
            stitch.color = color;
            stitch.raycastTarget = false;
            return stitch;
        }

        /// 影つきの紙。Torn にするとふちがやぶれる。
        public static HudShape Paper(Transform parent, Palette p, HudShape.Kind kind, float radius = 0f, int seed = 0, float shadowDepth = 5f)
        {
            if (shadowDepth > 0f) DropShadow(parent, kind, p.Shadow, radius, shadowDepth, seed);
            var body = Shape(parent, "Paper", kind, p.Paper, radius, PaperTexture);
            body.seed = seed;
            return body;
        }

        public static Text AddText(RectTransform rt, string text, int size, Color color, TextAnchor align, float outline = 0f, Color outlineColor = default)
        {
            var t = AddGraphic<Text>(rt);
            t.font = Font;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = text;
            if (outline > 0f)
            {
                var o = rt.gameObject.AddComponent<ThickOutline>();
                o.effectColor = outlineColor;
                o.size = outline;
            }
            return t;
        }

        public static Text Label(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size,
            string text, int fontSize, Color color, TextAnchor align, float outline = 0f, Color outlineColor = default)
        {
            return AddText(Place(parent, name, anchor, pivot, position, size), text, fontSize, color, align, outline, outlineColor);
        }

        /// 白い縁のついた丸い肖像。sprite がなければ色だけの丸。
        public static Image Portrait(Transform parent, Sprite sprite, Color fallback, float border, Color shadow)
        {
            DropShadow(parent, HudShape.Kind.Circle, shadow, 0f, 4f);
            Shape(parent, "Border", HudShape.Kind.Circle, Color.white);
            var plain = Shape(parent, "Fallback", HudShape.Kind.Circle, fallback);
            Stretch(plain.rectTransform, -border);
            var rt = Node(parent, "Face");
            Stretch(rt, -border);
            var image = AddGraphic<Image>(rt);
            image.preserveAspect = true;
            image.raycastTarget = false;
            SetPortrait(image, sprite, fallback);
            return image;
        }

        /// 肖像の絵を差し替える。絵がなければ同じ場所の色だけの丸を出す。tint は倒れたときの灰色など。
        public static void SetPortrait(Image face, Sprite sprite, Color fallback, Color? tint = null)
        {
            face.sprite = sprite;
            face.enabled = sprite != null;
            face.color = tint ?? Color.white;
            var plain = face.transform.parent.Find("Fallback");
            if (plain != null) plain.GetComponent<HudShape>().color = tint.HasValue ? fallback * tint.Value : fallback;
        }

        /// 勇者のジョブ。人間は HeroClassInitializer、BOT は BotSkillUser が持っている。
        public static Game.Hero.HeroClassData ClassOf(GameObject hero)
        {
            if (hero == null) return null;
            var init = hero.GetComponent<Game.Hero.HeroClassInitializer>();
            if (init != null && init.ClassData != null) return init.ClassData;
            var bot = hero.GetComponent<Game.Bot.BotSkillUser>();
            return bot != null ? bot.ClassData : null;
        }

        // ---- 表示用の言葉 ----

        public static string ClassLabel(string className)
        {
            switch (className)
            {
                case "Swordsman": return "剣士";
                case "Warrior": return "重戦士";
                case "Mage": return "魔法使い";
                case "Priest": return "僧侶";
                default: return string.IsNullOrEmpty(className) ? "勇者" : className;
            }
        }

        /// BOT の GameObject 名（BotHero_2 など）は「ボット2」と短く出す。
        public static string HeroName(GameObject hero)
        {
            if (hero == null) return "";
            string n = hero.name.Replace("(Clone)", "").Trim();
            if (n.IndexOf("Bot", System.StringComparison.OrdinalIgnoreCase) < 0) return n;
            string digits = "";
            foreach (char c in n) if (char.IsDigit(c)) digits += c;
            return "ボット" + digits;
        }

        /// 塔の短い名前。Spawner_NW なら「北西」。知らない名前はそのまま出す。
        public static string TowerLabel(string spawnerName)
        {
            string key = spawnerName != null && spawnerName.StartsWith("Spawner_") ? spawnerName.Substring(8) : spawnerName;
            switch (key)
            {
                case "NW": return "北西";
                case "NE": return "北東";
                case "Center": return "中央";
                case "SW": return "南西";
                case "SE": return "南東";
                case "N": return "北";
                case "S": return "南";
                case "E": return "東";
                case "W": return "西";
                case "ForestN": return "森北";
                case "ForestE": return "森東";
                case "MountainW": return "山西";
                case "MountainS": return "山南";
                default: return key;
            }
        }

        /// 塔の並び順。地図の北から（北西・北東・中央・南西・南東）。知らない名前は後ろに名前順。
        public static int CompareTowers(Game.Monster.Spawner a, Game.Monster.Spawner b)
        {
            int oa = TowerOrder(a != null ? a.name : null), ob = TowerOrder(b != null ? b.name : null);
            if (oa != ob) return oa.CompareTo(ob);
            return string.CompareOrdinal(a != null ? a.name : "", b != null ? b.name : "");
        }

        private static int TowerOrder(string spawnerName)
        {
            // 魔王ロビーの地図と同じく 山・森・砂漠 の順。砂漠の中は方角の順。
            switch (TowerLabel(spawnerName))
            {
                case "山西": return 0;
                case "山南": return 1;
                case "森北": return 2;
                case "森東": return 3;
                case "北西": return 10;
                case "北": return 11;
                case "北東": return 12;
                case "西": return 13;
                case "中央": return 14;
                case "東": return 15;
                case "南西": return 16;
                case "南": return 17;
                case "南東": return 18;
                default: return 99;
            }
        }

        public static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
        }

        /// 分を0埋めしない短い時計（1:20、0:45）。
        public static string ShortClock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        public static string Thousands(int value) => value.ToString("#,0");
    }
}
