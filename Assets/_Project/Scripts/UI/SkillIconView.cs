using UnityEngine;
using UnityEngine.UI;
using Game.Skill;

namespace Game.UI
{
    /// <summary>
    /// Puts SkillData.icon inside a DiamondGraphic as a child Image. The diamond is widest above its middle, so the
    /// icon sits on the largest circle that fits inside (centre 42.7% from the top, radius 35% of the size); icon art
    /// keeps within radius 46/100 of its own box, which gives a box of 76% of the diamond.
    /// </summary>
    public static class SkillIconView
    {
        private const string ChildName = "SkillIcon";
        private const float BoxFraction = 0.76f;
        private const float CentreFromBottom = 0.573f;

        public static void Set(RectTransform diamond, SkillData skill)
        {
            if (diamond == null) return;
            var child = diamond.Find(ChildName);
            Image image;
            if (child != null)
            {
                image = child.GetComponent<Image>();
            }
            else
            {
                var go = new GameObject(ChildName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(diamond, false);
                var rt = (RectTransform)go.transform;
                float half = BoxFraction / 2f;
                rt.anchorMin = new Vector2(0.5f - half, CentreFromBottom - half);
                rt.anchorMax = new Vector2(0.5f + half, CentreFromBottom + half);
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                rt.SetAsFirstSibling(); // labels parented to the diamond stay on top
                image = go.GetComponent<Image>();
                image.preserveAspect = true;
                image.raycastTarget = false;
            }
            image.sprite = skill != null ? skill.icon : null;
            image.enabled = image.sprite != null;
        }
    }
}
