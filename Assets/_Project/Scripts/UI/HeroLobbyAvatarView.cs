using UnityEngine;
using UnityEngine.UI;
using Game.Skill;

namespace Game.UI
{
    public class HeroLobbyAvatarView : MonoBehaviour
    {
        private RectTransform root;
        private Image avatarOval;
        private PentagonGraphic[] skillPentagons;
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
            var ovalGO = new GameObject("AvatarOval", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            ovalGO.transform.SetParent(root, false);
            var oRT = (RectTransform)ovalGO.transform;
            oRT.anchorMin = new Vector2(0.5f, 1f); oRT.anchorMax = new Vector2(0.5f, 1f); oRT.pivot = new Vector2(0.5f, 1f);
            oRT.sizeDelta = new Vector2(260f, 360f);
            oRT.anchoredPosition = new Vector2(0f, -130f);
            avatarOval = ovalGO.GetComponent<Image>();
            avatarOval.color = new Color(1f, 0.15f, 0.15f, 1f);
            avatarOval.raycastTarget = false;

            // NamePlate: child of AvatarOval, anchor (0.5,0), pivot (0.5,1), pos (0,0), size 200x28
            var npGO = new GameObject("NamePlate", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            npGO.transform.SetParent(ovalGO.transform, false);
            var npRT = (RectTransform)npGO.transform;
            npRT.anchorMin = new Vector2(0.5f, 0f); npRT.anchorMax = new Vector2(0.5f, 0f); npRT.pivot = new Vector2(0.5f, 1f);
            npRT.sizeDelta = new Vector2(200f, 28f);
            npRT.anchoredPosition = new Vector2(0f, 0f);
            var npImg = npGO.GetComponent<Image>();
            npImg.color = new Color(0f, 0f, 0f, 0.85f);
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

            // ClassName: child of AvatarOval, anchor (0.5,0), pivot (0.5,1), pos (0,-32) (28 nameplate + 4 gap roughly)
            var cnGO = new GameObject("ClassName", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            cnGO.transform.SetParent(ovalGO.transform, false);
            var cnRT = (RectTransform)cnGO.transform;
            cnRT.anchorMin = new Vector2(0.5f, 0f); cnRT.anchorMax = new Vector2(0.5f, 0f); cnRT.pivot = new Vector2(0.5f, 1f);
            cnRT.sizeDelta = new Vector2(300f, 28f);
            cnRT.anchoredPosition = new Vector2(0f, -16f - 28f);
            classNameText = cnGO.GetComponent<Text>();
            classNameText.font = font; classNameText.fontSize = 16; classNameText.fontStyle = FontStyle.Bold;
            classNameText.alignment = TextAnchor.MiddleCenter; classNameText.color = Color.white;
            classNameText.raycastTarget = false; classNameText.text = "Swordsman";

            // PentagonHolder: anchor (0.5,1), pos (0,-540), size (440,60)
            var holderGO = new GameObject("PentagonHolder", typeof(RectTransform));
            holderGO.transform.SetParent(root, false);
            var hRT = (RectTransform)holderGO.transform;
            hRT.anchorMin = new Vector2(0.5f, 1f); hRT.anchorMax = new Vector2(0.5f, 1f); hRT.pivot = new Vector2(0.5f, 0.5f);
            hRT.sizeDelta = new Vector2(440f, 60f);
            hRT.anchoredPosition = new Vector2(0f, -540f);

            // 5 pentagons at x = -176, -88, 0, 88, 176, size 48x48
            skillPentagons = new PentagonGraphic[5];
            float[] xs = { -176f, -88f, 0f, 88f, 176f };
            for (int i = 0; i < 5; i++)
            {
                var pGO = new GameObject("Pent_" + i, typeof(RectTransform));
                pGO.transform.SetParent(holderGO.transform, false);
                var pRT = (RectTransform)pGO.transform;
                pRT.anchorMin = new Vector2(0.5f, 0.5f); pRT.anchorMax = new Vector2(0.5f, 0.5f); pRT.pivot = new Vector2(0.5f, 0.5f);
                pRT.sizeDelta = new Vector2(48f, 48f);
                pRT.anchoredPosition = new Vector2(xs[i], 0f);
                var pg = pGO.AddComponent<PentagonGraphic>();
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
