using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Game.Skill;

namespace Game.UI
{
    /// <summary>
    /// Mouse-following tooltip showing a skill's name, MP cost and description.
    /// Built procedurally; create once with Create(canvas), then call Show(skill) / Hide().
    /// </summary>
    public class SkillTooltipUI : MonoBehaviour
    {
        private RectTransform rt;
        private Text nameText;
        private Text costText;
        private Text descText;
        private Canvas parentCanvas;
        private RectTransform canvasRT;

        public static SkillTooltipUI Create(Canvas parentCanvas)
        {
            if (parentCanvas == null) return null;
            var go = new GameObject("SkillTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parentCanvas.transform, false);
            var img = go.GetComponent<Image>();
            img.color = new Color(0.05f, 0.05f, 0.08f, 0.95f);
            img.raycastTarget = false;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f); rect.anchorMax = new Vector2(0f, 0f); rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(280f, 110f);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var nameGO = new GameObject("Name", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            nameGO.transform.SetParent(go.transform, false);
            var nRT = nameGO.GetComponent<RectTransform>();
            nRT.anchorMin = new Vector2(0f, 1f); nRT.anchorMax = new Vector2(1f, 1f); nRT.pivot = new Vector2(0.5f, 1f);
            nRT.sizeDelta = new Vector2(-16f, 26f);
            nRT.anchoredPosition = new Vector2(0f, -6f);
            var nT = nameGO.GetComponent<Text>();
            nT.font = font; nT.fontSize = 18; nT.fontStyle = FontStyle.Bold; nT.alignment = TextAnchor.MiddleLeft;
            nT.color = new Color(1f, 0.9f, 0.4f, 1f); nT.raycastTarget = false;

            var costGO = new GameObject("Cost", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            costGO.transform.SetParent(go.transform, false);
            var cRT = costGO.GetComponent<RectTransform>();
            cRT.anchorMin = new Vector2(0f, 1f); cRT.anchorMax = new Vector2(1f, 1f); cRT.pivot = new Vector2(0.5f, 1f);
            cRT.sizeDelta = new Vector2(-16f, 22f);
            cRT.anchoredPosition = new Vector2(0f, -32f);
            var cT = costGO.GetComponent<Text>();
            cT.font = font; cT.fontSize = 14; cT.alignment = TextAnchor.MiddleLeft;
            cT.color = new Color(0.55f, 0.75f, 1f, 1f); cT.raycastTarget = false;

            var descGO = new GameObject("Desc", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            descGO.transform.SetParent(go.transform, false);
            var dRT = descGO.GetComponent<RectTransform>();
            dRT.anchorMin = new Vector2(0f, 0f); dRT.anchorMax = new Vector2(1f, 1f); dRT.pivot = new Vector2(0.5f, 0.5f);
            dRT.offsetMin = new Vector2(8f, 6f); dRT.offsetMax = new Vector2(-8f, -58f);
            var dT = descGO.GetComponent<Text>();
            dT.font = font; dT.fontSize = 13; dT.alignment = TextAnchor.UpperLeft;
            dT.color = Color.white; dT.horizontalOverflow = HorizontalWrapMode.Wrap; dT.verticalOverflow = VerticalWrapMode.Overflow;
            dT.raycastTarget = false;

            var tt = go.AddComponent<SkillTooltipUI>();
            tt.rt = rect;
            tt.nameText = nT;
            tt.costText = cT;
            tt.descText = dT;
            tt.parentCanvas = parentCanvas;
            tt.canvasRT = parentCanvas.GetComponent<RectTransform>();
            go.SetActive(false);
            return tt;
        }

        public void Show(SkillData s)
        {
            if (s == null) { Hide(); return; }
            if (nameText != null) nameText.text = s.skillName;
            if (costText != null) costText.text = $"SP {s.spCost}   CT {s.cooldown:0.#}s";
            if (descText != null) descText.text = string.IsNullOrEmpty(s.description) ? "(説明なし)" : s.description;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            UpdatePosition();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!gameObject.activeSelf) return;
            UpdatePosition();
        }

        private void UpdatePosition()
        {
            if (rt == null) return;
            Vector2 mousePos;
            if (Mouse.current != null) mousePos = Mouse.current.position.ReadValue();
            else mousePos = (Vector2)Input.mousePosition;
            if (mousePos.x <= 0f && mousePos.y <= 0f) mousePos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            float canvasScale = parentCanvas != null ? parentCanvas.scaleFactor : 1f;
            Vector2 sizePx = rt.sizeDelta * canvasScale;
            float offX = 18f;
            float offY = 18f;
            float x = mousePos.x + offX;
            float y = mousePos.y - offY;
            if (x + sizePx.x > Screen.width) x = mousePos.x - offX - sizePx.x;
            if (y - sizePx.y < 0f) y = mousePos.y + offY + sizePx.y;

            if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay && parentCanvas.worldCamera != null)
            {
                Vector2 local;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, new Vector2(x, y), parentCanvas.worldCamera, out local))
                    rt.anchoredPosition = local;
            }
            else
            {
                rt.position = new Vector3(x, y, 0f);
            }
        }
    }
}
