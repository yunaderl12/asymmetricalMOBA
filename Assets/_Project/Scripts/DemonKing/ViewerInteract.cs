using UnityEngine;

namespace Game.DemonKing
{
    public class ViewerInteract : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            var vs = other.GetComponentInParent<DemonKingViewSwitcher>();
            if (vs == null) return;
            vs.IsAtViewer = true;
            Debug.Log($"[Viewer] {other.name} entered Viewer");
        }

        private void OnTriggerExit(Collider other)
        {
            var vs = other.GetComponentInParent<DemonKingViewSwitcher>();
            if (vs == null) return;
            vs.IsAtViewer = false;
            Debug.Log($"[Viewer] {other.name} exited Viewer");
        }
    }
}
