using UnityEngine;
using UnityEngine.UI;
using Game.Skill;

namespace Game.UI
{
    public class HeroLobbyAvatarView : MonoBehaviour
    {
        private RectTransform root;
        private EllipseGraphic avatarOval;
        private DiamondGraphic[] skillPentagons;
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

            // AvatarOval: anchor (0.5, 1), pivot (0.5, 1), pos (0, -130), size 260x360
            // Use EllipseGraphic so the shape is an actual oval, not a solid rectangle.
            var ovalGO = new GameObject("AvatarOval", typeof(RectTransform), typeof(CanvasRenderer));
            ovalGO.transform.SetParent(root, false);
            var oRT = (RectTransform)ovalGO.transform;
            oRT.anchorMin = new Vector2(0.5f, 1f); oRT.anchorMax = new Vector2(0.5f, 1f); oRT.pivot = new Vector2(0.5f, 1f);
            oRT.sizeDelta = new Vector2(260f, 360f);
            oRT.anchoredPosition = new Vector2(0f, -130f);
            avatarOval = ovalGO.AddComponent<EllipseGraphic>();
            avatarOval.color = new Color(1f, 0.15f, 0.15f, 1f);
            avatarOval.raycastTarget = false;

            // NamePlate: child of AvatarOval, sits INSIDE the lower portion of the oval
            // anchor (0.5,0) bottom-center, pivot (0.5,0) bottom-center, pos (0, 36) so
            // the plate sits ~36px above the oval's bottom (visually overlapping the avatar)
            var npGO = new GameObject("NamePlate", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            npGO.transform.SetParent(ovalGO.transform, false);
            var npRT = (RectTransform)npGO.transform;
            npRT.anchorMin = new Vector2(0.5f, 0f); npRT.anchorMax = new Vector2(0.5f, 0f); npRT.pivot = new Vector2(0.5f, 0f);
            npRT.sizeDelta = new Vector2(220f, 28f);
            npRT.anchoredPosition = new Vector2(0f, 36f);
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
            userNameText.font = font; userNameText.fontSize = 14; userNameText.fontStyle = FontStyle.Bold;
            userNameText.alignment = TextAnchor.MiddleCenter; userNameText.color = Color.white;
            userNameText.raycastTarget = false; userNameText.text = "ユーザー名";

            // ClassName: sits just below the oval's bottom edge
            // anchor (0.5,0) bottom-center of oval, pivot (0.5,1) top-center of text,
            // pos (0, -12) so the text starts 12px below the oval.
            var cnGO = new GameObject("ClassName", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            cnGO.transform.SetParent(ovalGO.transform, false);
            var cnRT = (RectTransform)cnGO.transform;
            cnRT.anchorMin = new Vector2(0.5f, 0f); cnRT.anchorMax = new Vector2(0.5f, 0f); cnRT.pivot = new Vector2(0.5f, 1f);
            cnRT.sizeDelta = new Vector2(300f, 28f);
            cnRT.anchoredPosition = new Vector2(0f, -12f);
            classNameText = cnGO.GetComponent<Text>();
            classNameText.font = font; classNameText.fontSize = 16; classNameText.fontStyle = FontStyle.Bold;
            classNameText.alignment = TextAnchor.MiddleCenter; classNameText.color = Color.white;
            classNameText.raycastTarget = false; classNameText.text = "Swordsman";

            // SkillPentagonHolder: anchor (0.5,1), pos (0,-560), size 220x220 — square so 5 rotated diamonds fit
            var holderGO = new GameObject("SkillPentagonHolder", typeof(RectTransform));
            holderGO.transform.SetParent(root, false);
            var hRT = (RectTransform)holderGO.transform;
            hRT.anchorMin = new Vector2(0.5f, 1f); hRT.anchorMax = new Vector2(0.5f, 1f); hRT.pivot = new Vector2(0.5f, 0.5f);
            hRT.sizeDelta = new Vector2(220f, 220f);
            hRT.anchoredPosition = new Vector2(0f, -580f);

            // 5 diamonds, all sharing the same position & pivot (bottom-center).
            // Rotating each by i*72° around that shared bottom vertex produces a 5-petal pentagon.
            skillPentagons = new DiamondGraphic[5];
            float diamondSize = 80f;
            for (int i = 0; i < 5; i++)
            {
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
        }

        public void SetAvatarColor(Color c)
        {
            if (avatarOval != null) avatarOval.color = c;
        }

        public void SetClassName(string s)
        {
            if (classNameText != null) classNameText.text = s ?? "";
        }

        /// <summary>
        /// Color slots 0..3 with currentClassColor, slot 4 with skill.EffectiveIconColor.
        /// </summary>
        public void SetSkills(SkillData[] skills, Color currentClassColor)
        {
            if (skillPentagons == null || skills == null) return;
            for (int i = 0; i < skillPentagons.Length; i++)
            {
                if (skillPentagons[i] == null) continue;
                if (i < 4)
                {
                    skillPentagons[i].color = currentClassColor;
                }
                else
                {
                    var s = i < skills.Length ? skills[i] : null;
                    skillPentagons[i].color = s != null ? s.EffectiveIconColor() : new Color(0.4f, 0.4f, 0.4f, 1f);
                }
            }
        }

        public void SetUserName(string s)
        {
            if (userNameText != null) userNameText.text = s ?? "";
        }
    }
}
