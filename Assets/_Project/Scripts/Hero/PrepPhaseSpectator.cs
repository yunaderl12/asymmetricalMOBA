using UnityEngine;
using UnityEngine.InputSystem;
using Game.Core;
using Game.Combat;
using Game.Skill;

namespace Game.Hero
{
    public class PrepPhaseSpectator : MonoBehaviour
    {
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float flySpeed = 14f;
        [SerializeField] private float lookSensitivity = 0.12f;

        private HeroFPSController fps;
        private HeroAttack attack;
        private SkillExecutor executor;

        private bool active;
        private Vector3 cameraOriginalLocalPos;
        private Quaternion cameraOriginalLocalRot;
        private float pitch;
        private float yaw;

        private void Awake()
        {
            fps = GetComponent<HeroFPSController>();
            attack = GetComponent<HeroAttack>();
            executor = GetComponent<SkillExecutor>();
            if (cameraTransform == null)
            {
                var t = transform.Find("Camera");
                if (t != null) cameraTransform = t;
            }
        }

        private void Start()
        {
            if (MatchManager.Instance != null)
            {
                MatchManager.Instance.OnStateChanged += OnStateChanged;
                if (MatchManager.Instance.CurrentState == MatchManager.MatchState.DemonPrepPhase) Begin();
            }
        }

        private void OnDestroy()
        {
            if (MatchManager.Instance != null) MatchManager.Instance.OnStateChanged -= OnStateChanged;
            if (active) End();
        }

        private void OnStateChanged(MatchManager.MatchState s)
        {
            if (s == MatchManager.MatchState.DemonPrepPhase) Begin();
            else if (active) End();
        }

        private void Begin()
        {
            if (active || cameraTransform == null) return;
            active = true;
            cameraOriginalLocalPos = cameraTransform.localPosition;
            cameraOriginalLocalRot = cameraTransform.localRotation;
            Vector3 e = cameraTransform.eulerAngles;
            pitch = e.x; yaw = e.y;
            cameraTransform.SetParent(null, true);

            if (fps != null) fps.enabled = false;
            if (attack != null) attack.enabled = false;
            if (executor != null) executor.enabled = false;
            Debug.Log("[PrepSpectator] Begin: detached camera; hero body locked");
        }

        private void End()
        {
            if (!active) return;
            active = false;
            if (cameraTransform != null)
            {
                cameraTransform.SetParent(transform, false);
                cameraTransform.localPosition = cameraOriginalLocalPos;
                cameraTransform.localRotation = cameraOriginalLocalRot;
            }
            if (fps != null) fps.enabled = true;
            if (attack != null) attack.enabled = true;
            if (executor != null) executor.enabled = true;
            Debug.Log("[PrepSpectator] End: camera re-attached; hero unlocked");
        }

        private void Update()
        {
            if (!active || cameraTransform == null) return;

            if (Mouse.current != null)
            {
                Vector2 d = Mouse.current.delta.ReadValue();
                yaw += d.x * lookSensitivity;
                pitch = Mathf.Clamp(pitch - d.y * lookSensitivity, -85f, 85f);
                cameraTransform.eulerAngles = new Vector3(pitch, yaw, 0f);
            }

            if (Keyboard.current != null)
            {
                Vector3 dir = Vector3.zero;
                var fwd = cameraTransform.forward;
                var right = cameraTransform.right;
                if (Keyboard.current.wKey.isPressed) dir += fwd;
                if (Keyboard.current.sKey.isPressed) dir -= fwd;
                if (Keyboard.current.dKey.isPressed) dir += right;
                if (Keyboard.current.aKey.isPressed) dir -= right;
                if (Keyboard.current.spaceKey.isPressed) dir += Vector3.up;
                if (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.cKey.isPressed) dir -= Vector3.up;
                if (dir.sqrMagnitude > 0.01f)
                    cameraTransform.position += dir.normalized * flySpeed * Time.deltaTime;
            }
        }
    }
}
