using UnityEngine;
using UnityEngine.InputSystem;
using Game.Combat;
using Game.Skill;

namespace Game.Hero
{
    [RequireComponent(typeof(CharacterController))]
    public class HeroFPSController : MonoBehaviour
    {
        [Header("Camera")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float lookSensitivity = 0.1f;
        [SerializeField] private float pitchMin = -85f;
        [SerializeField] private float pitchMax = 85f;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float jumpPower = 5f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float groundedStickForce = -2f;

        public float MoveSpeed => moveSpeed;
        public void SetMoveSpeed(float value) { moveSpeed = value; }
        public bool LookEnabled { get; set; } = true;
        /// 溜め中など、視点だけ動かせて移動できない状態。
        public bool MovementLocked { get; set; }
        /// スキルが勇者を直接動かしている間（Teleport の飛行中）は、移動も視点も重力も止める。
        public bool Suspended { get; set; }
        public float LookPitch => pitch;
        /// カメラの揺れを含まない照準方向。
        public Vector3 AimForward => transform.rotation * Quaternion.Euler(pitch, 0f, 0f) * Vector3.forward;

        private CharacterController controller;
        private HeroInputActions inputs;
        private CharacterModelAnimator modelAnim; // lazily cached (added at runtime by HeroClassInitializer)

        private float pitch;
        private float verticalVelocity;
        private Vector3 slipVelocity;
        private bool cursorLocked;
        private Vector3 cameraBaseLocalPosition;
        private Vector3 cameraKickPosition;
        private float cameraKickPitch;
        private float cameraKickRoll;
        private bool cameraKickDirty;

        private bool IsAnimLocked()
        {
            if (modelAnim == null) modelAnim = GetComponent<CharacterModelAnimator>();
            return modelAnim != null && modelAnim.IsRespawnAnimPlaying;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            inputs = new HeroInputActions();
            if (cameraTransform != null) cameraBaseLocalPosition = cameraTransform.localPosition;
        }

        /// スキル演出によるカメラの一時的なずれ。照準（AimForward）には影響しない。
        public void SetCameraKick(Vector3 localPosition, float pitchDegrees, float rollDegrees = 0f)
        {
            cameraKickPosition = localPosition;
            cameraKickPitch = pitchDegrees;
            cameraKickRoll = rollDegrees;
            cameraKickDirty = true;
        }

        private void LateUpdate()
        {
            if (!cameraKickDirty || cameraTransform == null) return;
            cameraTransform.localPosition = cameraBaseLocalPosition + cameraKickPosition;
            cameraTransform.localRotation = Quaternion.Euler(pitch + cameraKickPitch, 0f, cameraKickRoll);
            cameraKickDirty = cameraKickPosition != Vector3.zero || cameraKickPitch != 0f || cameraKickRoll != 0f;
        }

        private void OnEnable()
        {
            if (inputs == null) inputs = new HeroInputActions();
            inputs.Hero.Enable();
            inputs.Hero.Pause.performed += OnPause;
            LockCursor(true);
        }

        private void OnDisable()
        {
            if (inputs == null) return;
            inputs.Hero.Pause.performed -= OnPause;
            inputs.Hero.Disable();
            LockCursor(false);
        }

        private void Update()
        {
            if (Game.UI.ConsoleUI.IsConsoleActive || Suspended) return;
            if (IsAnimLocked())
            {
                // Keep gravity so the hero doesn't float, but ignore movement input.
                verticalVelocity += gravity * Time.deltaTime;
                if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = groundedStickForce;
                controller.Move(new Vector3(0f, verticalVelocity * Time.deltaTime, 0f));
                return;
            }
            HandleLook();
            if (MovementLocked || StatusEffectController.IsStunnedObj(gameObject)) HandleStunnedGravity();
            else HandleMove();
            HandleClickToRelock();
        }

        private void HandleLook()
        {
            if (!cursorLocked) return;
            if (!LookEnabled) return;

            var look = inputs.Hero.Look.ReadValue<Vector2>();
            float yawDelta = look.x * lookSensitivity;
            float pitchDelta = -look.y * lookSensitivity;

            transform.Rotate(0f, yawDelta, 0f, Space.World);

            pitch = Mathf.Clamp(pitch + pitchDelta, pitchMin, pitchMax);
            if (cameraTransform != null)
            {
                cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
        }

        private void HandleStunnedGravity()
        {
            verticalVelocity += gravity * Time.deltaTime;
            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = groundedStickForce;
            Vector3 motion = IceField.ApplySlip(transform.position, Vector3.zero, ref slipVelocity);
            motion.y = verticalVelocity;
            controller.Move(motion * Time.deltaTime);
        }

        private void HandleMove()
        {
            Vector2 input = inputs.Hero.Move.ReadValue<Vector2>();
            Vector3 horizontal = (transform.right * input.x + transform.forward * input.y) * moveSpeed;

            if (controller.isGrounded)
            {
                if (verticalVelocity < 0f) verticalVelocity = groundedStickForce;
                if (inputs.Hero.Jump.WasPerformedThisFrame())
                {
                    verticalVelocity = jumpPower;
                }
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
            }

            Vector3 motion = IceField.ApplySlip(transform.position, horizontal, ref slipVelocity);
            motion.y = verticalVelocity;
            controller.Move(motion * Time.deltaTime);
        }

        private void HandleClickToRelock()
        {
            if (cursorLocked) return;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                LockCursor(true);
            }
        }

        private void OnPause(InputAction.CallbackContext _)
        {
            LockCursor(!cursorLocked);
        }

        private void LockCursor(bool locked)
        {
            cursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
