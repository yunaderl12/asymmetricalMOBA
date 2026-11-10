using UnityEngine;
using UnityEngine.UI;
using Game.DemonKing;

namespace Game.UI
{
    /// 魔王のフィールド魔法の札（画面の下の真ん中、1〜5 キー）。TPS でも俯瞰でも見えるよう、俯瞰の地図より手前のキャンバスに出す。
    /// クールタイム中は札が暗くなって残り秒数が出る。狙っているあいだは札が持ち上がり、上に操作の案内が出る。
    public class FieldSpellBar : MonoBehaviour
    {
        private const float CardWidth = 96f;
        private const float CardHeight = 118f;
        private const float Gap = 10f;
        private const float RefusalSeconds = 1.6f;

        private class Card
        {
            public RectTransform Root;
            public RectTransform Shade;
            public Text Seconds;
            public HudShape Glow;
            public float Lift;
        }

        private readonly HudStyle.Palette pal = HudStyle.DemonPalette;
        private FieldSpellCaster caster;
        private Card[] cards;
        private GameObject hint;
        private Text hintText;
        private string shownHint;

        public static FieldSpellBar Create(FieldSpellCaster caster)
        {
            var canvasGo = new GameObject("FieldSpellCanvas", typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 8; // 俯瞰の地図（7）より手前
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var root = HudStyle.Filled(canvasGo.transform, "FieldSpellBar");
            var bar = root.gameObject.AddComponent<FieldSpellBar>();
            bar.caster = caster;
            bar.Build(root);
            return bar;
        }

        private void Build(RectTransform root)
        {
            int count = caster.Slots.Length;
            float width = count * CardWidth + (count - 1) * Gap;
            var row = HudStyle.Place(root, "Cards", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(width, CardHeight));
            var top = new Vector2(0.5f, 1f);
            cards = new Card[count];
            for (int i = 0; i < count; i++)
            {
                var s = caster.Slots[i];
                var card = HudStyle.Place(row, "Spell_" + i, Vector2.zero, new Vector2(0.5f, 0f), new Vector2(i * (CardWidth + Gap) + CardWidth * 0.5f, 0f), new Vector2(CardWidth, CardHeight));
                card.localEulerAngles = new Vector3(0f, 0f, i % 2 == 0 ? 1.2f : -1.2f);
                var glow = HudStyle.Shape(card, "Glow", HudShape.Kind.RoundRect, new Color(1f, 0.79f, 0.24f, 0f), 14f);
                HudStyle.Stretch(glow.rectTransform, 6f);
                HudStyle.Paper(card, pal, HudShape.Kind.RoundRect, 10f, 40 + i, 4f);
                var icon = HudStyle.Place(card, "Icon", top, top, new Vector2(0f, -12f), new Vector2(62f, 62f));
                if (s != null)
                {
                    HudStyle.Shape(icon, "Felt", HudShape.Kind.Circle, s.color, 0f, HudStyle.FeltTexture);
                    if (s.icon != null)
                    {
                        var face = HudStyle.Node(icon, "Picture");
                        HudStyle.Stretch(face, -5f);
                        var image = HudStyle.AddGraphic<Image>(face);
                        image.sprite = s.icon;
                        image.preserveAspect = true;
                        image.raycastTarget = false;
                    }
                    HudStyle.Stitch(icon, pal.Light, true, 0f, s.icon != null ? 3f : 4f);
                    if (s.icon == null) HudStyle.AddText(HudStyle.Filled(icon, "Glyph"), s.glyph, 30, Color.white, TextAnchor.MiddleCenter, 4f, pal.Outline);
                }
                HudStyle.Label(card, "Name", top, top, new Vector2(0f, -78f), new Vector2(CardWidth - 6f, 28f), s != null ? s.spellName : "空き", s != null && s.spellName.Length > 3 ? 16 : 19, pal.Ink, TextAnchor.MiddleCenter);
                var key = HudStyle.Place(card, "Key", new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(8f, -6f), new Vector2(30f, 30f));
                HudStyle.Shape(key, "Cap", HudShape.Kind.RoundRect, pal.Ribbon, 7f);
                HudStyle.AddText(HudStyle.Filled(key, "Text"), (i + 1).ToString(), 18, pal.Light, TextAnchor.MiddleCenter);

                var shade = HudStyle.Shape(card, "Cooldown", HudShape.Kind.RoundRect, new Color(0.05f, 0.02f, 0.04f, 0.72f), 10f);
                shade.raycastTarget = false;
                var shadeRt = shade.rectTransform;
                shadeRt.anchorMin = Vector2.zero;
                shadeRt.anchorMax = Vector2.one;
                shadeRt.pivot = new Vector2(0.5f, 0f);
                var seconds = HudStyle.Label(card, "Seconds", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(CardWidth, 50f), "", 34, pal.Light, TextAnchor.MiddleCenter, 4f, pal.Outline);
                cards[i] = new Card { Root = card, Shade = shadeRt, Seconds = seconds, Glow = glow };
            }

            var hintRt = HudStyle.Place(root, "Hint", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(620f, 48f));
            HudStyle.DropShadow(hintRt, HudShape.Kind.Ribbon, pal.Shadow, 0f, 4f);
            HudStyle.Shape(hintRt, "Ribbon", HudShape.Kind.Ribbon, pal.Ribbon);
            hintText = HudStyle.AddText(HudStyle.Filled(hintRt, "Text"), "", 22, pal.Light, TextAnchor.MiddleCenter);
            hint = hintRt.gameObject;
            hint.SetActive(false);
        }

        private void Update()
        {
            if (caster == null)
            {
                Destroy(transform.parent.gameObject);
                return;
            }
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < cards.Length; i++)
            {
                var c = cards[i];
                float ratio = caster.CooldownRatio(i);
                float left = caster.CooldownLeft(i);
                c.Shade.gameObject.SetActive(ratio > 0f);
                c.Shade.anchorMax = new Vector2(1f, ratio);
                c.Seconds.text = left > 0f ? Mathf.CeilToInt(left).ToString() : "";
                bool aiming = caster.Aiming == i;
                c.Lift = Mathf.MoveTowards(c.Lift, aiming ? 1f : 0f, dt * 6f);
                c.Root.anchoredPosition = new Vector2(c.Root.anchoredPosition.x, 18f * c.Lift);
                var g = c.Glow.color;
                g.a = aiming ? 0.6f + 0.3f * Mathf.Sin(Time.unscaledTime * 8f) : 0f;
                c.Glow.color = g;
            }

            string text = null;
            if (caster.Aiming >= 0)
            {
                var s = caster.Slots[caster.Aiming];
                string where = string.IsNullOrEmpty(caster.AimNote) ? "" : "（" + caster.AimNote + "）";
                text = s.spellName + where + "　左クリックで発動 ／ 右クリックでやめる";
            }
            else if (Time.unscaledTime - caster.RefusedAt < RefusalSeconds) text = caster.Refusal;
            hint.SetActive(text != null);
            if (text != null && text != shownHint)
            {
                shownHint = text;
                hintText.text = text;
            }
        }
    }
}
