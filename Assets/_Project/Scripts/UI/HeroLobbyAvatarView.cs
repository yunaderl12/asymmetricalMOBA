using UnityEngine;
using UnityEngine.UI;
using Game.Hero;
using Game.Skill;

namespace Game.UI
{
    public class HeroLobbyAvatarView : MonoBehaviour
    {
        private RectTransform root;
        private EllipseGraphic avatarOval;
        private RawImage avatar3D; // shown instead of avatarOval when SetAvatar3D(tex) provides a texture
        private DiamondGraphic[] skillPentagons;
        private Text[] skillNameTexts;
        private Image[] skillIcons;
        private Text userNameText;
        private Text classNameText;

        public static HeroLobbyAvatarView Create(RectTransform parent)
        {
            var go = new GameObject("AvatarView", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<HeroLobbyAvatarView>();
            view.Build();
            return view;
        }

        private void Build()
        {
            root = (RectTransform)transform;
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero; root.offsetMax = Vector2.zero;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // AvatarOval: anchored to the LEFT half of the screen (anchor 0.25, 1).
            // Fills most of the left half vertically so the 3D model reads at a glance.
            var ovalGO = new GameObject("AvatarOval", typeof(RectTransform), typeof(CanvasRenderer));
            ovalGO.transform.SetParent(root, false);
            var oRT = (RectTransform)ovalGO.transform;
            oRT.anchorMin = new Vector2(0.25f, 1f); oRT.anchorMax = new Vector2(0.25f, 1f); oRT.pivot = new Vector2(0.5f, 1f);
            // Sized so the class name and the skill pentagon below still fit in 1080.
            oRT.sizeDelta = new Vector2(600f, 720f);
            oRT.anchoredPosition = new Vector2(0f, -60f);
            avatarOval = ovalGO.AddComponent<EllipseGraphic>();
            avatarOval.color = new Color(1f, 0.15f, 0.15f, 1f);
            avatarOval.raycastTarget = false;

            // RawImage overlay for an optional 3D render (via CharacterAvatarPreview).
            // Sized slightly larger than the oval so the model isn't cropped to the oval
            // shape, but anchored to the same area so it visually replaces the avatar.
            var imgGO = new GameObject("Avatar3D", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            imgGO.transform.SetParent(ovalGO.transform, false);
            var iRT = (RectTransform)imgGO.transform;
            iRT.anchorMin = new Vector2(0f, 0f); iRT.anchorMax = new Vector2(1f, 1f);
            iRT.offsetMin = new Vector2(-80f, -40f); iRT.offsetMax = new Vector2(80f, 40f);
            avatar3D = imgGO.GetComponent<RawImage>();
            avatar3D.raycastTarget = false;
            avatar3D.gameObject.SetActive(false);

            // NamePlate: child of AvatarOval, sits INSIDE the lower portion of the oval
            // anchor (0.5,0) bottom-center, pivot (0.5,0) bottom-center, pos (0, 36) so
            // the plate sits ~36px above the oval's bottom (visually overlapping the avatar)
            var npGO = new GameObject("NamePlate", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            npGO.transform.SetParent(ovalGO.transform, false);
            var npRT = (RectTransform)npGO.transform;
            npRT.anchorMin = new Vector2(0.5f, 0f); npRT.anchorMax = new Vector2(0.5f, 0f); npRT.pivot = new Vector2(0.5f, 0f);
            npRT.sizeDelta = new Vector2(360f, 40f);
            npRT.anchoredPosition = new Vector2(0f, 60f);
            var npImg = npGO.GetComponent<Image>();
            npImg.color = new Color(0f, 0f, 0f, 0.9f);
            npImg.raycastTarget = false;

            // UserName: full fill of NamePlate
            var unGO = new GameObject("UserName", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            unGO.transform.SetParent(npGO.transform, false);
            var unRT = (RectTransform)unGO.transform;
            unRT.anchorMin = Vector2.zero; unRT.anchorMax = Vector2.one;
            unRT.offsetMin = Vector2.zero; unRT.offsetMax = Vector2.zero;
            userNameText = unGO.GetComponent<Text>();
            userNameText.font = font; userNameText.fontSize = 20; userNameText.fontStyle = FontStyle.Bold;
            userNameText.alignment = TextAnchor.MiddleCenter; userNameText.color = Color.white;
            userNameText.raycastTarget = false; userNameText.text = "ユーザー名";

            // ClassName: sits just below the oval's bottom edge
            // anchor (0.5,0) bottom-center of oval, pivot (0.5,1) top-center of text,
            // pos (0, -12) so the text starts 12px below the oval.
            var cnGO = new GameObject("ClassName", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            cnGO.transform.SetParent(ovalGO.transform, false);
            var cnRT = (RectTransform)cnGO.transform;
            cnRT.anchorMin = new Vector2(0.5f, 0f); cnRT.anchorMax = new Vector2(0.5f, 0f); cnRT.pivot = new Vector2(0.5f, 1f);
            cnRT.sizeDelta = new Vector2(420f, 36f);
            cnRT.anchoredPosition = new Vector2(0f, -16f);
            classNameText = cnGO.GetComponent<Text>();
            classNameText.font = font; classNameText.fontSize = 24; classNameText.fontStyle = FontStyle.Bold;
            classNameText.alignment = TextAnchor.MiddleCenter; classNameText.color = Color.white;
            classNameText.raycastTarget = false; classNameText.text = "Swordsman";

            // SkillPentagonHolder: anchored to the LEFT half too, sitting below the bigger avatar.
            var holderGO = new GameObject("SkillPentagonHolder", typeof(RectTransform));
            holderGO.transform.SetParent(root, false);
            var hRT = (RectTransform)holderGO.transform;
            hRT.anchorMin = new Vector2(0.25f, 1f); hRT.anchorMax = new Vector2(0.25f, 1f); hRT.pivot = new Vector2(0.5f, 0.5f);
            hRT.sizeDelta = new Vector2(280f, 280f);
            hRT.anchoredPosition = new Vector2(0f, -955f);

            // 5 diamonds, all sharing the same position & pivot (bottom-center).
            // Rotating each by i*72° around that shared bottom vertex produces a 5-petal pentagon.
            // Petal i is skill slot i; its name sits just past the petal tip.
            skillPentagons = new DiamondGraphic[5];
            skillNameTexts = new Text[5];
            float diamondSize = 80f;
            for (int i = 0; i < 5; i++)
            {
                float rad = i * 72f * Mathf.Deg2Rad;
                var tipDir = new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad));
                var nGO = new GameObject("SkillName_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                nGO.transform.SetParent(holderGO.transform, false);
                var nRT = (RectTransform)nGO.transform;
                nRT.anchorMin = new Vector2(0.5f, 0.5f); nRT.anchorMax = new Vector2(0.5f, 0.5f);
                nRT.pivot = new Vector2(0.5f - tipDir.x * 0.5f, 0.5f - tipDir.y * 0.5f); // grow away from the pentagon
                nRT.sizeDelta = new Vector2(140f, 22f);
                nRT.anchoredPosition = tipDir * (diamondSize + 6f);
                var nT = nGO.GetComponent<Text>();
                nT.font = font; nT.fontSize = 16; nT.fontStyle = FontStyle.Bold;
                nT.alignment = tipDir.x < -0.3f ? TextAnchor.MiddleRight : tipDir.x > 0.3f ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
                nT.color = Color.white; nT.raycastTarget = false;
                nGO.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
                skillNameTexts[i] = nT;

                var pGO = new GameObject("Diamond_" + i, typeof(RectTransform), typeof(CanvasRenderer));
                pGO.transform.SetParent(holderGO.transform, false);
                var pRT = (RectTransform)pGO.transform;
                pRT.anchorMin = new Vector2(0.5f, 0.5f); pRT.anchorMax = new Vector2(0.5f, 0.5f);
                pRT.pivot = new Vector2(0.5f, 0f); // bottom-center -> rotation pivots at the diamond's lowest point
                pRT.sizeDelta = new Vector2(diamondSize, diamondSize);
                pRT.anchoredPosition = Vector2.zero;
                pRT.localRotation = Quaternion.Euler(0f, 0f, i * 72f);
                var pg = pGO.AddComponent<DiamondGraphic>();
                pg.color = new Color(0.5f, 0.5f, 0.5f, 1f);
                pg.raycastTarget = false;
                skillPentagons[i] = pg;
            }

            // Illustrations go after every diamond (petals overlap) and stay upright, unlike the rotated petals.
            skillIcons = new Image[5];
            for (int i = 0; i < 5; i++)
            {
                float rad = i * 72f * Mathf.Deg2Rad;
                var iGO = new GameObject("SkillIcon_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iGO.transform.SetParent(holderGO.transform, false);
                var iconRT = (RectTransform)iGO.transform;
                iconRT.anchorMin = new Vector2(0.5f, 0.5f); iconRT.anchorMax = new Vector2(0.5f, 0.5f);
                iconRT.sizeDelta = new Vector2(52f, 52f);
                iconRT.anchoredPosition = new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad)) * (diamondSize * 0.573f);
                skillIcons[i] = iGO.GetComponent<Image>();
                skillIcons[i].preserveAspect = true;
                skillIcons[i].raycastTarget = false;
                skillIcons[i].enabled = false;
            }
        }

        /// Shows the loadout's class name, model (flat oval if it has none) and skills.
        public void ShowLoadout(HeroSkillLoadout loadout, CharacterAvatarPreview preview)
        {
            var cls = loadout.CurrentClass;
            if (cls != null)
            {
                SetAvatarColor(cls.avatarColor);
                SetClassName(cls.className);
                bool shown = preview != null && preview.ShowModel(cls.modelPath, cls.modelMaterialPath);
                SetAvatar3D(shown ? preview.Texture : null);
            }
            SetSkills(loadout.Skills, loadout.ClassColor);
        }

        public void SetAvatarColor(Color c)
        {
            if (avatarOval != null) avatarOval.color = c;
        }

        /// <summary>
        /// Show a live 3D preview (e.g. from CharacterAvatarPreview.Texture). Pass null to
        /// fall back to the flat-coloured EllipseGraphic.
        /// </summary>
        public void SetAvatar3D(Texture tex)
        {
            if (avatar3D == null) return;
            if (tex != null)
            {
                avatar3D.texture = tex;
                avatar3D.gameObject.SetActive(true);
                if (avatarOval != null) avatarOval.enabled = false; // hide flat oval
            }
            else
            {
                avatar3D.texture = null;
                avatar3D.gameObject.SetActive(false);
                if (avatarOval != null) avatarOval.enabled = true;
            }
        }

        public void SetClassName(string s)
        {
            if (classNameText != null) classNameText.text = s ?? "";
        }

        /// <summary>
        /// Color slots 0..3 with currentClassColor, slot 4 with skill.EffectiveIconColor,
        /// and label every petal with its skill name.
        /// </summary>
        public void SetSkills(SkillData[] skills, Color currentClassColor)
        {
            if (skillPentagons == null || skills == null) return;
            for (int i = 0; i < skillPentagons.Length; i++)
            {
                var s = i < skills.Length ? skills[i] : null;
                if (skillPentagons[i] != null)
                {
                    if (i < 4) skillPentagons[i].color = currentClassColor;
                    else skillPentagons[i].color = s != null ? s.EffectiveIconColor() : new Color(0.4f, 0.4f, 0.4f, 1f);
                }
                if (skillNameTexts != null && skillNameTexts[i] != null) skillNameTexts[i].text = s != null ? s.skillName : "-";
                if (skillIcons != null && skillIcons[i] != null)
                {
                    skillIcons[i].sprite = s != null ? s.icon : null;
                    skillIcons[i].enabled = skillIcons[i].sprite != null;
                }
            }
        }

        public void SetUserName(string s)
        {
            if (userNameText != null) userNameText.text = s ?? "";
        }
    }
}
