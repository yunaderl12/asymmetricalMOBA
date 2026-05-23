using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

namespace Game.DemonKing
{
    [RequireComponent(typeof(DemonKingController))]
    public class DemonKingViewSwitcher : MonoBehaviour
    {
        public enum ViewMode { Tps, BirdsEye, SpawnerOverhead }

        [SerializeField] private CinemachineCamera tpsCam;
        [SerializeField] private CinemachineCamera birdsEyeCam;
        [SerializeField] private CinemachineCamera spawnerOverheadCam;
        [SerializeField] private int activePriority = 20;
        [SerializeField] private int inactivePriority = 10;

        [Header("Plain camera fallback (used when Cinemachine cams are null)")]
        [SerializeField] private Camera tpsPlainCamera;
        [SerializeField] private Camera birdsEyePlainCamera;
        [SerializeField] private Camera spawnerOverheadPlainCamera;
        [SerializeField] private AudioListener tpsAudio;
        [SerializeField] private AudioListener birdsEyeAudio;
        [SerializeField] private AudioListener spawnerOverheadAudio;

        public bool IsAtViewer { get; set; }
        public bool TpsActive => currentMode == ViewMode.Tps;
        public ViewMode CurrentMode => currentMode;
        public Camera BirdsEyePlainCamera => birdsEyePlainCamera;
        public Camera SpawnerOverheadPlainCamera => spawnerOverheadPlainCamera;

        public event Action<bool> OnViewChanged; // legacy: true=BirdsEye, false=other
        public event Action<ViewMode> OnModeChanged;

        private DemonKingController controller;
        private ViewMode currentMode = ViewMode.Tps;

        private void Awake()
        {
            controller = GetComponent<DemonKingController>();
            // Make sure the UI birds-eye map exists in the scene so OnModeChanged ->
            // BirdsEye actually shows the icon map (and not just the 3D camera).
            Game.UI.BirdsEyeMapUI.EnsureExists();
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

        public void AssignOverheadCamera(Camera cam, AudioListener al)
        {
            if (cam != null && spawnerOverheadPlainCamera == null) spawnerOverheadPlainCamera = cam;
            if (al != null && spawnerOverheadAudio == null) spawnerOverheadAudio = al;
            ApplyState();
        }

        public void EnterSpawnerOverhead(Game.Monster.Spawner sp)
        {
            if (sp == null) return;
            if (spawnerOverheadPlainCamera != null)
            {
                spawnerOverheadPlainCamera.transform.position = sp.transform.position + Vector3.up * 18f;
                spawnerOverheadPlainCamera.transform.rotation = Quaternion.Euler(75f, 0f, 0f);
            }
            currentMode = ViewMode.SpawnerOverhead;
            ApplyState();
            OnModeChanged?.Invoke(currentMode);
            OnViewChanged?.Invoke(false);
            Debug.Log($"[DemonKing] view -> SpawnerOverhead ({sp.name})");
        }

        public void ExitSpawnerOverhead()
        {
            currentMode = ViewMode.BirdsEye;
            ApplyState();
            OnModeChanged?.Invoke(currentMode);
            OnViewChanged?.Invoke(true);
            Debug.Log("[DemonKing] view -> BirdsEye (from SpawnerOverhead)");
        }

        /// <summary>Force TPS view (used by BirdsEyeMapUI when Esc is pressed).</summary>
        public void ReturnToTps()
        {
            if (currentMode == ViewMode.Tps) return;
            currentMode = ViewMode.Tps;
            ApplyState();
            OnModeChanged?.Invoke(currentMode);
            OnViewChanged?.Invoke(false);
            Debug.Log("[DemonKing] view -> TPS (Esc)");
        }

        private void OnViewSwitch(InputAction.CallbackContext _)
        {
            if (currentMode == ViewMode.SpawnerOverhead)
            {
                // V is reserved while in spawner overhead; use Esc / right-click to exit.
                return;
            }
            if (currentMode == ViewMode.Tps)
            {
                if (!IsAtViewer)
                {
                    Debug.Log("[DemonKing] V pressed but not at Core - ignored");
                    return;
                }
                currentMode = ViewMode.BirdsEye;
                ApplyState();
                OnModeChanged?.Invoke(currentMode);
                OnViewChanged?.Invoke(true);
                Debug.Log("[DemonKing] view -> BirdsEye, movement disabled");
            }
            else
            {
                currentMode = ViewMode.Tps;
                ApplyState();
                OnModeChanged?.Invoke(currentMode);
                OnViewChanged?.Invoke(false);
                Debug.Log("[DemonKing] view -> TPS, movement enabled");
            }
        }

        private void ApplyState()
        {
            bool tps = currentMode == ViewMode.Tps;
            bool be = currentMode == ViewMode.BirdsEye;
            bool overhead = currentMode == ViewMode.SpawnerOverhead;

            if (tpsCam != null) tpsCam.Priority = tps ? activePriority : inactivePriority;
            if (birdsEyeCam != null) birdsEyeCam.Priority = be ? activePriority : inactivePriority;
            if (spawnerOverheadCam != null) spawnerOverheadCam.Priority = overhead ? activePriority : inactivePriority;

            ApplyPlainCam(tpsPlainCamera, tpsAudio, tps);
            ApplyPlainCam(birdsEyePlainCamera, birdsEyeAudio, be);
            ApplyPlainCam(spawnerOverheadPlainCamera, spawnerOverheadAudio, overhead);

            if (controller != null) controller.MovementEnabled = tps;
        }

        private void ApplyPlainCam(Camera c, AudioListener a, bool active)
        {
            if (c != null)
            {
                c.enabled = active;
                c.gameObject.tag = active ? "MainCamera" : "Untagged";
            }
            if (a != null) a.enabled = active;
        }
    }
}
