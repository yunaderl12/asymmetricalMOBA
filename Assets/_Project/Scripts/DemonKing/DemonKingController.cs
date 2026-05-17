using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.DemonKing
{
    [RequireComponent(typeof(CharacterController))]
    public class DemonKingController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 4.5f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float groundedStickForce = -2f;

        [Header("Look")]
        [SerializeField] private float lookSensitivity = 0.2f;

        private CharacterController controller;
        private DemonKingInputActions inputs;
        private float verticalVelocity;
        private bool cursorLocked;

        public bool MovementEnabled { get; set; } = true;

        public DemonKingInputActions Inputs
        {
            get
            {
                if (inputs == null) inputs = new DemonKingInputActions();
                return inputs;
            }
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (inputs == null) inputs = new DemonKingInputActions();
        }

        private void OnEnable()
        {
            if (inputs == null) inputs = new DemonKingInputActions();
            inputs.DemonKing.Enable();
            LockCursor(true);
        }

        private void OnDisable()
        {
            if (inputs == null) return;
            inputs.DemonKing.Disable();
            LockCursor(false);
        }

        private void Update()
        {
            HandleClickToRelock();
            if (!MovementEnabled) return;
            HandleRotation();
            HandleMove();
        }

        private void HandleRotation()
        {
            if (!cursorLocked) return;
            var look = inputs.DemonKing.Look.ReadValue<Vector2>();
            transform.Rotate(0f, look.x * lookSensitivity, 0f, Space.World);
        }

        private void HandleMove()
        {
            var input = inputs.DemonKing.Move.ReadValue<Vector2>();
            var horizontal = (transform.right * input.x + transform.forward * input.y) * moveSpeed;

            if (controller.isGrounded)
            {
                if (verticalVelocity < 0f) verticalVelocity = groundedStickForce;
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
            }

            var motion = horizontal;
            motion.y = verticalVelocity;
            controller.Move(motion * Time.deltaTime);
        }

        private void HandleClickToRelock()
        {
            if (cursorLocked) return;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) LockCursor(true);
        }

        private void LockCursor(bool locked)
        {
            cursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
