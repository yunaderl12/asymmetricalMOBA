using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using Game.Skill;

namespace Game.UI
{
    /// <summary>
    /// Attach to any UI element that represents a skill. After hoverDelay seconds
    /// of continuous hover the SkillTooltipUI is shown; exit/click hides it.
    /// </summary>
    public class SkillHoverTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
    {
        public SkillData data;
        public SkillTooltipUI tooltip;
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
            if (data != null && tooltip != null) tooltip.Show(data);
            pending = null;
        }
    }
}
