using UnityEngine;
using UnityEngine.InputSystem;

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

        private CharacterController controller;
        private HeroInputActions inputs;

        private float pitch;
        private float verticalVelocity;
        private bool cursorLocked;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            inputs = new HeroInputActions();
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
            if (Game.UI.ConsoleUI.IsConsoleActive) return;
            HandleLook();
            HandleMove();
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

            Vector3 motion = horizontal;
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
