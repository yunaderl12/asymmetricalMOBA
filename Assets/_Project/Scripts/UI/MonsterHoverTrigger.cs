using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using Game.Monster;

namespace Game.UI
{
    public class MonsterHoverTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
    {
        public MonsterData data;
        public MonsterTooltipUI tooltip;
        public float hoverDelay = 1.0f;

        private Coroutine pending;

        public void OnPointerEnter(PointerEventData e)
        {
            Debug.Log($"[HoverTrigger] enter {(data != null ? data.monsterName : "null")} tooltip={(tooltip != null ? "ok" : "NULL")}");
            if (pending != null) StopCoroutine(pending);
            pending = StartCoroutine(ShowAfterDelay());
        }

        public void OnPointerExit(PointerEventData e)
        {
            Debug.Log($"[HoverTrigger] exit {(data != null ? data.monsterName : "null")}");
            CancelPending();
            if (tooltip != null) tooltip.Hide();
        }

        public void OnPointerDown(PointerEventData e)
        {
            // Hide tooltip on click so it doesn't linger.
            CancelPending();
            if (tooltip != null) tooltip.Hide();
        }

        private void OnDisable()
        {
            CancelPending();
            if (tooltip != null) tooltip.Hide();
        }

        private void CancelPending()
        {
            if (pending != null) { StopCoroutine(pending); pending = null; }
        }

        private static bool warnedMissing;

        private IEnumerator ShowAfterDelay()
        {
            yield return new WaitForSeconds(hoverDelay);
            if (data != null)
            {
                if (tooltip != null) tooltip.Show(data);
                else if (!warnedMissing) { warnedMissing = true; Debug.LogWarning("[MonsterHoverTrigger] tooltip is null; canvas was not resolved."); }
            }
            pending = null;
        }
    }
}
