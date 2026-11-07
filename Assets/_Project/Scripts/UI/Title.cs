using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Game.Core;
using Game.Hero;

namespace Game.UI
{
    /// タイトル画面。紙のロゴと「勇者プレイ」「魔王プレイ」の大きな布のカード。
    public class Title : MonoBehaviour
    {
        [SerializeField] private string heroLobbyScene = "HeroLobby";
        [SerializeField] private string demonLobbyScene = "DemonLobby";
        [Tooltip("勇者プレイのカードに並べるジョブ（肖像を使う）。")]
        [SerializeField] private List<HeroClassData> heroClasses = new List<HeroClassData>();

        private void Awake()
        {
            GameSession.GetOrCreate();
        }

        private void Start()
        {
            Build();
        }

        private void Build()
        {
            var p = HudStyle.HeroPalette;
            var d = HudStyle.DemonPalette;
            var stage = LobbyStyle.CreateScreen("TitleCanvas", p, out var background);
            LobbyStyle.Glow(stage, 960f, 380f, 1500f, 900f, new Color(1f, 0.886f, 0.627f, 0.18f));
            LobbyStyle.Vignette(background, new Color(0.08f, 0.04f, 0.16f, 0.75f));

            // ロゴ
            var logo = LobbyStyle.At(stage, "Logo", 510f, 96f, 900f, 300f);
            var paper = HudStyle.Paper(logo, p, HudShape.Kind.Torn, 0f, 11, 8f);
            paper.raycastTarget = false;
            LobbyStyle.Tape(logo, 70f, -14f, 120f, 34f, LobbyStyle.TapeTeal, -6f);
            LobbyStyle.Tape(logo, 710f, -12f, 120f, 34f, LobbyStyle.TapeYellow, 5f);
            LobbyStyle.Text(logo, "Line1", 0f, 40f, 900f, 90f, "Asymmetrical", 66, p.Felt, TextAnchor.MiddleCenter);
            LobbyStyle.Text(logo, "Line2", 0f, 128f, 820f, 140f, "MOBA", 120, p.Ribbon, TextAnchor.MiddleCenter);
            LobbyStyle.Text(logo, "Kari", 660f, 186f, 160f, 60f, "（仮）", 38, p.Felt, TextAnchor.MiddleLeft);

            var tag = LobbyStyle.At(stage, "Tagline", 700f, 404f, 520f, 62f);
            LobbyStyle.Tilt(tag, -1.5f);
            HudStyle.DropShadow(tag, HudShape.Kind.Ribbon, p.Shadow, 0f, 5f);
            HudStyle.Shape(tag, "Ribbon", HudShape.Kind.Ribbon, p.Ribbon).notch = 22f;
            HudStyle.AddText(HudStyle.Filled(tag, "Text"), "勇者4人 vs 魔王1人", 28, Color.white, TextAnchor.MiddleCenter);

            // 勇者プレイ
            var hero = RoleCard(stage, "HeroButton", 380f, p, "勇者プレイ", "4人で魔王城のコアを壊す", -1f, OnHeroClicked);
            var classes = heroClasses.FindAll(c => c != null);
            int count = Mathf.Min(4, classes.Count);
            for (int i = 0; i < count; i++)
            {
                var c = classes[i];
                var face = LobbyStyle.At(hero, "Face_" + i, 270f - count * 56f + i * 112f, -54f, 118f, 118f);
                LobbyStyle.Sticker(face, c.icon != null ? c.icon : c.portrait, c.avatarColor, p);
            }

            // 魔王プレイ
            var demon = RoleCard(stage, "DemonButton", 1000f, d, "魔王プレイ", "魔物を操り、30分守り抜く", 1f, OnDemonClicked);
            var emblem = LobbyStyle.At(demon, "Emblem", 204f, -64f, 132f, 132f);
            DemonEmblem(emblem, d);

            LobbyStyle.Text(stage, "Build", 40f, 1020f, 300f, 30f, "開発版", 20, new Color(p.Light.r, p.Light.g, p.Light.b, 0.6f));
        }

        private static RectTransform RoleCard(Transform stage, string name, float x, HudStyle.Palette p, string title, string sub, float tilt, System.Action onClick)
        {
            var rt = LobbyStyle.At(stage, name, x, 560f, 540f, 330f);
            LobbyStyle.Tilt(rt, tilt);
            var ring = LobbyStyle.Ring(rt, HudShape.Kind.RoundRect, 28f, 7f);
            ring.enabled = false;
            var body = HudStyle.Felt(rt, p, HudShape.Kind.RoundRect, 28f, 9f, 10f);
            LobbyStyle.Text(rt, "Title", 0f, 104f, 540f, 90f, title, 68, Color.white, TextAnchor.MiddleCenter, 8f, p.Outline);
            LobbyStyle.Text(rt, "Sub", 0f, 200f, 540f, 40f, sub, 24, p.Light, TextAnchor.MiddleCenter);
            var button = LobbyStyle.MakeButton(rt, body, onClick);
            var hover = rt.gameObject.AddComponent<HoverRing>();
            hover.ring = ring;
            button.name = name;
            return rt;
        }

        /// 魔王の印（試合中HUDの魔王の肖像と同じ）。
        public static void DemonEmblem(RectTransform rt, HudStyle.Palette p)
        {
            HudStyle.DropShadow(rt, HudShape.Kind.Circle, p.Shadow, 0f, 4f);
            HudStyle.Shape(rt, "Border", HudShape.Kind.Circle, p.Light);
            var inner = HudStyle.Shape(rt, "Face", HudShape.Kind.Circle, HudStyle.Hex(0x2a0f18), 0f, HudStyle.FeltTexture);
            HudStyle.Stretch(inner.rectTransform, -Mathf.Max(3f, rt.sizeDelta.x * 0.04f));
            HudStyle.Stitch(rt, p.Stitch, true, 0f, rt.sizeDelta.x * 0.09f);
            HudStyle.AddText(HudStyle.Filled(rt, "Mark"), "魔", Mathf.RoundToInt(rt.sizeDelta.x * 0.47f), HudStyle.Hex(0xc8423c), TextAnchor.MiddleCenter, 5f, p.Outline);
        }

        private void OnHeroClicked()
        {
            if (GameSession.Instance != null) GameSession.Instance.CurrentRole = GameSession.PlayerRole.Hero;
            SceneManager.LoadScene(heroLobbyScene);
        }

        private void OnDemonClicked()
        {
            if (GameSession.Instance != null) GameSession.Instance.CurrentRole = GameSession.PlayerRole.DemonKing;
            SceneManager.LoadScene(demonLobbyScene);
        }
    }

    /// マウスを乗せている間だけ金色のふちを出す。
    public class HoverRing : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
    {
        public Graphic ring;
        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) { if (ring != null) ring.enabled = true; }
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { if (ring != null) ring.enabled = false; }
        private void OnDisable() { if (ring != null) ring.enabled = false; }
    }
}
