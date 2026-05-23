using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Game.Monster;

namespace Game.UI
{
    public class MonsterTooltipUI : MonoBehaviour
    {
        private RectTransform rt;
        private Text nameText;
        private Text costText;
        private Text descText;
        private Canvas parentCanvas;
        private RectTransform canvasRT;

        public static MonsterTooltipUI Create(Canvas parentCanvas)
        {
            if (parentCanvas == null) return null;
            var go = new GameObject("MonsterTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
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
            cT.color = new Color(0.85f, 0.85f, 0.85f, 1f); cT.raycastTarget = false;

            var descGO = new GameObject("Desc", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            descGO.transform.SetParent(go.transform, false);
            var dRT = descGO.GetComponent<RectTransform>();
            dRT.anchorMin = new Vector2(0f, 0f); dRT.anchorMax = new Vector2(1f, 1f); dRT.pivot = new Vector2(0.5f, 0.5f);
            dRT.offsetMin = new Vector2(8f, 6f); dRT.offsetMax = new Vector2(-8f, -58f);
            var dT = descGO.GetComponent<Text>();
            dT.font = font; dT.fontSize = 13; dT.alignment = TextAnchor.UpperLeft;
            dT.color = Color.white; dT.horizontalOverflow = HorizontalWrapMode.Wrap; dT.verticalOverflow = VerticalWrapMode.Overflow;
            dT.raycastTarget = false;

            var tt = go.AddComponent<MonsterTooltipUI>();
            tt.rt = rect;
            tt.nameText = nT;
            tt.costText = cT;
            tt.descText = dT;
            tt.parentCanvas = parentCanvas;
            tt.canvasRT = parentCanvas.GetComponent<RectTransform>();
            go.transform.SetAsLastSibling();
            go.SetActive(false);
            return tt;
        }

        public void Show(MonsterData md)
        {
            if (md == null) { Hide(); return; }
            if (nameText != null) nameText.text = md.monsterName;
            if (costText != null) costText.text = "Cost " + md.cost + "   HP " + md.hp + "   Atk " + md.atk + "   Spd " + md.spd;
            if (descText != null) descText.text = string.IsNullOrEmpty(md.description) ? "(説明なし)" : md.description;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            UpdatePosition();
            Debug.Log($"[Tooltip] Show {md.monsterName} at {rt.position} parent={(parentCanvas != null ? parentCanvas.name : "null")}");
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
            // If mouse position is somehow zero (e.g. cursor off-window), pin to screen center as a fallback.
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
                // Camera-space canvas: convert screen to local within canvas rect.
                Vector2 local;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, new Vector2(x, y), parentCanvas.worldCamera, out local))
                    rt.anchoredPosition = local;
            }
            else
            {
                // Overlay: world coords == screen pixels.
                rt.position = new Vector3(x, y, 0f);
            }
        }
    }
}
