using UnityEngine;

namespace Game.DemonKing
{
    public class CoreInteract : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            var vs = other.GetComponentInParent<DemonKingViewSwitcher>();
            if (vs == null) return;
            vs.IsAtViewer = true;
            Debug.Log($"[Core] {other.name} entered DemonCastle_Core");
        }

        private void OnTriggerExit(Collider other)
        {
            var vs = other.GetComponentInParent<DemonKingViewSwitcher>();
            if (vs == null) return;
            vs.IsAtViewer = false;
            Debug.Log($"[Core] {other.name} exited DemonCastle_Core");
        }
    }
}
