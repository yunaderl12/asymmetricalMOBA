using UnityEngine;
using UnityEngine.InputSystem;
using Game.Combat;

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
        [SerializeField] private Transform pitchTarget;
        [SerializeField] private float pitchMin = -70f;
        [SerializeField] private float pitchMax = 70f;

        private float pitch;

        private CharacterController controller;
        private Vector3 slipVelocity;
        private StatModifierContainer mods;
        private DemonKingInputActions inputs;
        private float verticalVelocity;
        private bool cursorLocked;

        public bool MovementEnabled { get; set; } = true;
        public float EffectiveMoveSpeed => moveSpeed * (mods != null ? mods.TotalSpdMultiplier : 1f);

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
            mods = GetComponent<StatModifierContainer>();
            if (inputs == null) inputs = new DemonKingInputActions();
        }

        private void Start()
        {
            Game.Hero.CharacterModelAnimator.AttachDemonKingModel(gameObject);
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
            if (Game.UI.ConsoleUI.IsConsoleActive) return;
            HandleClickToRelock();
            if (!MovementEnabled) return;
            HandleRotation();
            if (StatusEffectController.IsStunnedObj(gameObject)) return;
            HandleMove();
        }

        private void HandleRotation()
        {
            if (!cursorLocked) return;
            var look = inputs.DemonKing.Look.ReadValue<Vector2>();

            // Yaw: rotate body around world Y
            transform.Rotate(0f, look.x * lookSensitivity, 0f, Space.World);

            // Pitch: rotate pitchTarget (ShoulderTarget) - Cinemachine TPS follows its orientation
            if (pitchTarget != null)
            {
                pitch = Mathf.Clamp(pitch + -look.y * lookSensitivity, pitchMin, pitchMax);
                pitchTarget.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
        }

        private void HandleMove()
        {
            var input = inputs.DemonKing.Move.ReadValue<Vector2>();
            var horizontal = (transform.right * input.x + transform.forward * input.y) * EffectiveMoveSpeed;

            if (controller.isGrounded)
            {
                if (verticalVelocity < 0f) verticalVelocity = groundedStickForce;
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
            }

            var motion = Game.Skill.IceField.ApplySlip(transform.position, horizontal, ref slipVelocity);
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
