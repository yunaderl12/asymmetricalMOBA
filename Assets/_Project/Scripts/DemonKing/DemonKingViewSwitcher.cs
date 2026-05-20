using System;
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

        [Header("Plain camera fallback (used when Cinemachine cams are null)")]
        [SerializeField] private Camera tpsPlainCamera;
        [SerializeField] private Camera birdsEyePlainCamera;
        [SerializeField] private AudioListener tpsAudio;
        [SerializeField] private AudioListener birdsEyeAudio;

        public bool IsAtViewer { get; set; }
        public bool TpsActive => tpsActive;
        public event Action<bool> OnViewChanged; // true=BirdsEye, false=TPS

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
                    Debug.Log("[DemonKing] V pressed but not at Core - ignored");
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
            OnViewChanged?.Invoke(!tpsActive);
        }

        private void ApplyState()
        {
            if (tpsCam != null) tpsCam.Priority = tpsActive ? activePriority : inactivePriority;
            if (birdsEyeCam != null) birdsEyeCam.Priority = tpsActive ? inactivePriority : activePriority;

            if (tpsPlainCamera != null) { tpsPlainCamera.enabled = tpsActive; tpsPlainCamera.gameObject.tag = tpsActive ? "MainCamera" : "Untagged"; }
            if (birdsEyePlainCamera != null) { birdsEyePlainCamera.enabled = !tpsActive; birdsEyePlainCamera.gameObject.tag = !tpsActive ? "MainCamera" : "Untagged"; }
            if (tpsAudio != null) tpsAudio.enabled = tpsActive;
            if (birdsEyeAudio != null) birdsEyeAudio.enabled = !tpsActive;

            if (controller != null) controller.MovementEnabled = tpsActive;
        }
    }
}
