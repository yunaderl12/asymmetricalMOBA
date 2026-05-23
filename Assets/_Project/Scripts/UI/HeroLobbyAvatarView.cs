using UnityEngine;
using UnityEngine.UI;
using Game.Skill;

namespace Game.UI
{
    public class HeroLobbyAvatarView : MonoBehaviour
    {
        private RectTransform root;
        private Image avatarOval;
        private Image[] skillPentagons;
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

            // Avatar oval (centered, large)
            var ovalGO = new GameObject("AvatarOval", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            ovalGO.transform.SetParent(root, false);
            var oRT = (RectTransform)ovalGO.transform;
            oRT.anchorMin = new Vector2(0.5f, 0.5f); oRT.anchorMax = new Vector2(0.5f, 0.5f); oRT.pivot = new Vector2(0.5f, 0.5f);
            oRT.sizeDelta = new Vector2(280f, 420f);
            oRT.anchoredPosition = new Vector2(0f, 60f);
            avatarOval = ovalGO.GetComponent<Image>();
            avatarOval.color = new Color(1f, 0.15f, 0.15f, 1f);
            avatarOval.raycastTarget = false;
            // Oval look via fill: use builtin UISprite as rounded shape proxy (Unity has no built-in oval, square approximated).
            // For now flat colored square — designer can swap sprite later.

            // Class name text under avatar
            var clsGO = new GameObject("ClassName", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            clsGO.transform.SetParent(root, false);
            var cRT = (RectTransform)clsGO.transform;
            cRT.anchorMin = new Vector2(0.5f, 0.5f); cRT.anchorMax = new Vector2(0.5f, 0.5f); cRT.pivot = new Vector2(0.5f, 1f);
            cRT.sizeDelta = new Vector2(400f, 36f); cRT.anchoredPosition = new Vector2(0f, -160f);
            classNameText = clsGO.GetComponent<Text>();
            classNameText.font = font; classNameText.fontSize = 24; classNameText.fontStyle = FontStyle.Bold;
            classNameText.alignment = TextAnchor.MiddleCenter; classNameText.color = Color.white; classNameText.raycastTarget = false;
            classNameText.text = "Swordsman";

            // Skill pentagons row (5 icons under avatar)
            skillPentagons = new Image[5];
            float iconSize = 64f;
            float spacing = 84f;
            float totalWidth = spacing * 4f;
            for (int i = 0; i < 5; i++)
            {
                var icGO = new GameObject("Pent_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                icGO.transform.SetParent(root, false);
                var iRT = (RectTransform)icGO.transform;
                iRT.anchorMin = new Vector2(0.5f, 0.5f); iRT.anchorMax = new Vector2(0.5f, 0.5f); iRT.pivot = new Vector2(0.5f, 0.5f);
                iRT.sizeDelta = new Vector2(iconSize, iconSize);
                iRT.anchoredPosition = new Vector2(-totalWidth * 0.5f + i * spacing, -240f);
                iRT.localRotation = Quaternion.Euler(0f, 0f, 45f); // diamond
                skillPentagons[i] = icGO.GetComponent<Image>();
                skillPentagons[i].color = new Color(0.5f, 0.5f, 0.5f, 1f);
                skillPentagons[i].raycastTarget = false;
            }

            // User name (bottom)
            var unGO = new GameObject("UserName", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            unGO.transform.SetParent(root, false);
            var uRT = (RectTransform)unGO.transform;
            uRT.anchorMin = new Vector2(0.5f, 0f); uRT.anchorMax = new Vector2(0.5f, 0f); uRT.pivot = new Vector2(0.5f, 0f);
            uRT.sizeDelta = new Vector2(600f, 36f); uRT.anchoredPosition = new Vector2(0f, 36f);
            userNameText = unGO.GetComponent<Text>();
            userNameText.font = font; userNameText.fontSize = 18; userNameText.fontStyle = FontStyle.Bold;
            userNameText.alignment = TextAnchor.MiddleCenter; userNameText.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            userNameText.raycastTarget = false;
            userNameText.text = "ユーザー名";
        }

        public void SetAvatarColor(Color c)
        {
            if (avatarOval != null) avatarOval.color = c;
        }

        public void SetClassName(string s)
        {
            if (classNameText != null) classNameText.text = s ?? "";
        }

        public void SetSkills(SkillData[] skills)
        {
            if (skillPentagons == null || skills == null) return;
            for (int i = 0; i < skillPentagons.Length; i++)
            {
                if (skillPentagons[i] == null) continue;
                if (i < skills.Length && skills[i] != null) skillPentagons[i].color = skills[i].EffectiveIconColor();
                else skillPentagons[i].color = new Color(0.3f, 0.3f, 0.3f, 1f);
            }
        }

        public void SetUserName(string s)
        {
            if (userNameText != null) userNameText.text = s ?? "";
        }
    }
}
