using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Game.Monster;

namespace Game.DemonKing
{
    [RequireComponent(typeof(DemonKingViewSwitcher))]
    public class SpawnerClickPicker : MonoBehaviour
    {
        [SerializeField] private Camera birdsEyeCamera;
        [SerializeField] private float pickRange = 200f;
        [SerializeField] private LayerMask hitMask = ~0;

        private DemonKingViewSwitcher viewSwitcher;

        private void Awake()
        {
            viewSwitcher = GetComponent<DemonKingViewSwitcher>();
        }

        private void Update()
        {
            if (viewSwitcher == null) return;
            if (viewSwitcher.CurrentMode != DemonKingViewSwitcher.ViewMode.BirdsEye) return;
            if (Game.UI.ConsoleUI.IsConsoleActive) return;
            if (Mouse.current == null) return;
            if (!Mouse.current.leftButton.wasPressedThisFrame) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            var cam = birdsEyeCamera != null ? birdsEyeCamera : viewSwitcher.BirdsEyePlainCamera;
            if (cam == null) cam = Camera.main;
            if (cam == null) { Debug.LogWarning("[SpawnerClickPicker] no birds-eye camera"); return; }

            var ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out var hit, pickRange, hitMask))
            {
                var sp = hit.collider.GetComponentInParent<Spawner>();
                if (sp != null)
                {
                    Debug.Log($"[SpawnerClickPicker] hit {sp.name}");
                    var popup = Game.UI.SpawnerActionPopup.GetOrCreate();
                    if (popup != null) popup.Show(sp, viewSwitcher);
                }
                else
                {
                    Debug.Log("[SpawnerClickPicker] hit non-spawner: " + hit.collider.name);
                }
            }
            else
            {
                Debug.Log("[SpawnerClickPicker] no spawner under cursor");
            }
        }
    }
}
