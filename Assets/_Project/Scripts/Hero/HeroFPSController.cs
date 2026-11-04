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

        [Header("Run Feel (camera)")]
        [Tooltip("Metres per step while running")]
        [SerializeField] private float stepLength = 1.7f;
        [SerializeField] private float dashStepLength = 2.2f;
        [SerializeField] private float stepDip = 0.045f;
        [SerializeField] private float stepSway = 0.018f;
        [SerializeField] private float stepRollDegrees = 0.45f;
        [Tooltip("How fast the camera falls behind when a dash starts (m/s)")]
        [SerializeField] private float dashStartKick = 5f;
        [SerializeField] private float dashStopKick = 2f;
        [SerializeField] private float dashLagSmoothing = 0.2f;
        [SerializeField] private float dashLagBack = 0.32f;
        [SerializeField] private float dashLagForward = 0.14f;

        public float MoveSpeed => moveSpeed;
        public void SetMoveSpeed(float value) { moveSpeed = value; }
        public bool LookEnabled { get; set; } = true;
        /// 溜め中など、視点だけ動かせて移動できない状態。
        public bool MovementLocked { get; set; }
        /// スキルが勇者を直接動かしている間（Teleport の飛行中）は、移動も視点も重力も止める。
        public bool Suspended { get; set; }
        public float LookPitch => pitch;
        public bool IsDashing => dashing;
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
        private float stepPhase;
        private float stepWeight;
        private float dashLag;
        private float dashLagVelocity;
        private bool runFeelApplied;
        private bool dashing;
        private bool forwardHeld;
        private float lastForwardPressTime = float.NegativeInfinity;

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
            if (cameraTransform == null) return;
            UpdateRunFeel(out Vector3 feelPosition, out float feelRoll);
            bool feel = feelPosition != Vector3.zero || feelRoll != 0f;
            if (!cameraKickDirty && !feel && !runFeelApplied) return;
            cameraTransform.localPosition = cameraBaseLocalPosition + cameraKickPosition + feelPosition;
            cameraTransform.localRotation = Quaternion.Euler(pitch + cameraKickPitch, 0f, cameraKickRoll + feelRoll);
            cameraKickDirty = cameraKickPosition != Vector3.zero || cameraKickPitch != 0f || cameraKickRoll != 0f;
            runFeelApplied = feel;
        }

        // 一歩ごとの沈みと左右の揺れ、ダッシュの出だしで体に遅れるカメラ。照準（AimForward）には影響しない。
        private void UpdateRunFeel(out Vector3 position, out float roll)
        {
            float dt = Time.deltaTime;
            Vector3 velocity = controller.enabled ? controller.velocity : Vector3.zero;
            velocity.y = 0f;
            float speed = velocity.magnitude;
            bool running = !Suspended && controller.enabled && controller.isGrounded && speed > 0.5f;
            stepWeight = Mathf.MoveTowards(stepWeight, running ? 1f : 0f, dt * 6f);
            stepPhase += speed * dt / (dashing ? dashStepLength : stepLength) * Mathf.PI;

            float contact = 1f - Mathf.Abs(Mathf.Sin(stepPhase));
            float size = stepWeight * (dashing ? 1.35f : 1f);
            position = new Vector3(Mathf.Cos(stepPhase) * stepSway, -contact * contact * stepDip, 0f) * size;
            roll = Mathf.Cos(stepPhase) * stepRollDegrees * size;

            dashLag = Mathf.SmoothDamp(dashLag, 0f, ref dashLagVelocity, dashLagSmoothing, Mathf.Infinity, dt);
            dashLag = Mathf.Clamp(dashLag, -dashLagBack, dashLagForward);
            if (Mathf.Abs(dashLag) < 0.0005f && Mathf.Abs(dashLagVelocity) < 0.005f) dashLag = dashLagVelocity = 0f;
            position.z += dashLag;
        }

        // 前進を素早く2回押し、2回目を押し続けている間だけダッシュする。
        private void UpdateDash(Vector2 input)
        {
            bool held = input.y > 0.5f;
            if (held && !forwardHeld)
            {
                if (Time.time - lastForwardPressTime <= Game.Core.BalanceConfig.Instance.dashDoubleTapSeconds) SetDashing(true);
                lastForwardPressTime = Time.time;
            }
            if (!held) SetDashing(false);
            forwardHeld = held;
        }

        private void SetDashing(bool value)
        {
            if (dashing == value) return;
            dashing = value;
            dashLagVelocity = value ? -dashStartKick : dashStopKick;
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
            if (MovementLocked || StatusEffectController.IsStunnedObj(gameObject))
            {
                SetDashing(false);
                forwardHeld = false;
                HandleStunnedGravity();
            }
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
            UpdateDash(input);
            float speed = dashing ? moveSpeed * Game.Core.BalanceConfig.Instance.dashSpeedMultiplier : moveSpeed;
            Vector3 horizontal = (transform.right * input.x + transform.forward * input.y) * speed;

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
