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
            if (pending != null) StopCoroutine(pending);
            pending = StartCoroutine(ShowAfterDelay());
        }

        public void OnPointerExit(PointerEventData e)
        {
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

        private IEnumerator ShowAfterDelay()
        {
            yield return new WaitForSeconds(hoverDelay);
            if (tooltip != null && data != null) tooltip.Show(data);
            pending = null;
        }
    }
}
