using UnityEngine;
using UnityEngine.UI;
using Game.Hero;

namespace Game.UI
{
    /// ロビーで自分の勇者を見せる場所。丸い紙の敷物の上に選んだジョブの3Dモデルを立て、下に名札（ジョブ名とユーザー名）を出す。
    /// モデルが読めないときはジョブの肖像のシールを大きく出す。
    public class HeroShowcase : MonoBehaviour
    {
        private RectTransform root;
        private RawImage model;
        private Image portrait;
        private RectTransform portraitRt;
        private Text classText;
        private Text userText;
        private CharacterAvatarPreview preview;
        private Vector2 homePosition;
        private float slideTarget;

        /// 舞台の (x, y) に width x height の場所を取る。モデルは上、敷物と名札は下。
        public static HeroShowcase Create(Transform stage, float x, float y, float width, float height, HudStyle.Palette p)
        {
            var rt = LobbyStyle.At(stage, "HeroShowcase", x, y, width, height);
            var view = rt.gameObject.AddComponent<HeroShowcase>();
            view.Build(rt, p);
            return view;
        }

        private void Build(RectTransform rt, HudStyle.Palette p)
        {
            root = rt;
            homePosition = rt.anchoredPosition;
            float w = rt.sizeDelta.x, h = rt.sizeDelta.y;

            LobbyStyle.Glow(rt, w * 0.5f, h * 0.48f, w * 1.1f, h * 1.05f, LobbyStyle.Spotlight);

            // 敷物
            float rugW = w * 0.95f, rugH = rugW * 0.23f;
            var rug = LobbyStyle.AtCenter(rt, "Rug", w * 0.5f, h - 150f, rugW, rugH);
            HudStyle.DropShadow(rug, HudShape.Kind.Circle, p.Shadow, 0f, 10f);
            HudStyle.Shape(rug, "Paper", HudShape.Kind.Circle, LobbyStyle.Rug, 0f, HudStyle.PaperTexture);
            var stitch = HudStyle.Stitch(rug, LobbyStyle.RugStitch, true, 0f, 14f);
            stitch.thickness = 4f;

            // モデル（CharacterAvatarPreview の絵は 640x768）
            // モデルの足は絵の下から2割ほど上にあるので、足が敷物の真ん中に乗るように下げる。
            float modelH = (h - 150f) * 0.88f;
            float modelW = modelH * 640f / 768f;
            float modelY = (h - 150f) - modelH * 0.82f;
            var modelRt = LobbyStyle.At(rt, "Model", (w - modelW) * 0.5f, modelY, modelW, modelH);
            model = HudStyle.AddGraphic<RawImage>(modelRt);
            model.raycastTarget = false;
            model.enabled = false;

            float pSize = Mathf.Min(w * 0.62f, modelH * 0.8f);
            portraitRt = LobbyStyle.At(rt, "Portrait", (w - pSize) * 0.5f, h - 150f - pSize - 10f, pSize, pSize);
            portrait = LobbyStyle.Sticker(portraitRt, null, p.Felt, p);
            portraitRt.gameObject.SetActive(false);

            // 名札
            var plate = LobbyStyle.AtCenter(rt, "NamePlate", w * 0.5f, h - 56f, 360f, 112f);
            LobbyStyle.FeltPlate(plate, p, 18f, 7f, 5f);
            classText = HudStyle.Label(plate, "Class", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(340f, 60f),
                "", 48, Color.white, TextAnchor.MiddleCenter, 7f, p.Outline);
            userText = HudStyle.Label(plate, "User", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(340f, 30f),
                "", 21, p.Light, TextAnchor.MiddleCenter);

            preview = new GameObject("CharacterAvatarPreview_Auto").AddComponent<CharacterAvatarPreview>();
        }

        private void OnDestroy()
        {
            if (preview != null) Destroy(preview.gameObject);
        }

        public void Show(HeroClassData cls, string userName)
        {
            userText.text = userName ?? "";
            if (cls == null) return;
            classText.text = HudStyle.ClassLabel(cls.className);
            bool shown = preview != null && preview.ShowModel(cls.modelPath, cls.modelMaterialPath);
            model.texture = shown ? preview.Texture : null;
            model.enabled = shown;
            portraitRt.gameObject.SetActive(!shown);
            if (!shown) HudStyle.SetPortrait(portrait, cls.icon != null ? cls.icon : cls.portrait, cls.avatarColor);
        }

        /// 横にずらす（編成パネルを開いたとき、パネルに隠れないように）。0 で元の場所。
        public void SlideBy(float dx)
        {
            slideTarget = dx;
        }

        private void Update()
        {
            var target = homePosition + new Vector2(slideTarget, 0f);
            root.anchoredPosition = Vector2.Lerp(root.anchoredPosition, target, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
        }
    }
}
