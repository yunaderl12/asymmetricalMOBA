using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

namespace Game.DemonKing
{
    [RequireComponent(typeof(DemonKingController))]
    public class DemonKingViewSwitcher : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera tpsCam;
        [SerializeField] private CinemachineCamera birdsEyeCam;
        [SerializeField] private int activePriority = 20;
        [SerializeField] private int inactivePriority = 10;

        public bool IsAtViewer { get; set; }

        private DemonKingController controller;
        private bool tpsActive = true;

        private void Awake()
        {
            controller = GetComponent<DemonKingController>();
        }

        private void OnEnable()
        {
            var inputs = controller.Inputs;
            inputs.DemonKing.ViewSwitch.performed += OnViewSwitch;
            ApplyState();
        }

        private void OnDisable()
        {
            if (controller != null && controller.Inputs != null)
                controller.Inputs.DemonKing.ViewSwitch.performed -= OnViewSwitch;
        }

        private void OnViewSwitch(InputAction.CallbackContext _)
        {
            if (tpsActive)
            {
                if (!IsAtViewer)
                {
                    Debug.Log("[DemonKing] V pressed but not at Viewer - ignored");
                    return;
                }
                tpsActive = false;
                ApplyState();
                Debug.Log("[DemonKing] view -> BirdsEye, movement disabled");
            }
            else
            {
                tpsActive = true;
                ApplyState();
                Debug.Log("[DemonKing] view -> TPS, movement enabled");
            }
        }

        private void ApplyState()
        {
            if (tpsCam != null) tpsCam.Priority = tpsActive ? activePriority : inactivePriority;
            if (birdsEyeCam != null) birdsEyeCam.Priority = tpsActive ? inactivePriority : activePriority;
            if (controller != null) controller.MovementEnabled = tpsActive;
        }
    }
}
