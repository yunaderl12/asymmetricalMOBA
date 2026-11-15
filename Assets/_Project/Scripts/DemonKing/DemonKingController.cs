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

        [Header("Camera")]
        [Tooltip("Radius of the sphere swept from the shoulder to the camera. Walls and pillars pull the camera in front of them.")]
        [SerializeField] private float cameraRadius = 0.2f;
        [Tooltip("Camera position seen from the shoulder (ShoulderTarget). Higher and farther back than the prefab, so the camera looks down on the Demon King.")]
        [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 2.8f, -7f);
        [Tooltip("Degrees the camera looks down from the shoulder's forward (added to the mouse pitch).")]
        [SerializeField] private float cameraTilt = 15f;

        private static readonly RaycastHit[] CameraHits = new RaycastHit[16];

        private float pitch;
        private Transform cameraTransform;
        private Vector3 cameraRest;

        private CharacterController controller;
        private Vector3 slipVelocity;
        private StatModifierContainer mods;
        private DemonKingInputActions inputs;
        private float verticalVelocity;
        private bool cursorLocked;

        public bool MovementEnabled { get; set; } = true;
        public float EffectiveMoveSpeed => moveSpeed * (mods != null ? mods.TotalSpdMultiplier : 1f) * (terrain != null ? terrain.SpeedFactor : 1f);
        private Game.Core.TerrainWalker terrain;

        public DemonKingInputActions Inputs
        {
            get
            {
                if (inputs == null) { inputs = new DemonKingInputActions(); Game.Core.KeyBindings.Track(inputs.asset); }
                return inputs;
            }
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            mods = GetComponent<StatModifierContainer>();
            terrain = Game.Core.TerrainWalker.For(gameObject);
            terrain.SlopePenalty = Game.Core.BalanceConfig.Instance.demonKingSlopePenalty;
            terrain.SplashScale = Game.Core.BalanceConfig.Instance.demonKingModelScale;
            if (GetComponent<Game.Core.FallRespawn>() == null) gameObject.AddComponent<Game.Core.FallRespawn>();
            if (inputs == null) { inputs = new DemonKingInputActions(); Game.Core.KeyBindings.Track(inputs.asset); }
            var cam = pitchTarget != null ? pitchTarget.GetComponentInChildren<Camera>(true) : null;
            if (cam != null)
            {
                cameraTransform = cam.transform;
                // 少し上から斜め下を見下ろす。マウスの上下はこの角度に足される。
                cameraTransform.localPosition = cameraOffset;
                cameraTransform.localRotation = Quaternion.Euler(cameraTilt, 0f, 0f);
                cameraRest = cameraOffset;
            }
        }

        private void LateUpdate()
        {
            if (cameraTransform == null) return;
            Vector3 origin = pitchTarget.position;
            Vector3 offset = pitchTarget.TransformPoint(cameraRest) - origin;
            float full = offset.magnitude;
            float nearest = full;
            int count = Physics.SphereCastNonAlloc(origin, cameraRadius, offset / full, CameraHits, full, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = CameraHits[i];
                // 動くキャラの陰でカメラが揺れないよう、地形と建物だけで止める。
                if (hit.distance <= 0f || hit.distance >= nearest || hit.collider.GetComponentInParent<HealthComponent>() != null) continue;
                nearest = hit.distance;
            }
            cameraTransform.localPosition = cameraRest * (nearest / full);
        }

        private Game.Hero.CharacterModelAnimator modelAnim;
        private float airTime;

        private void Start()
        {
            modelAnim = Game.Hero.CharacterModelAnimator.AttachDemonKingModel(gameObject);
            if (modelAnim != null) modelAnim.NominalSpeed = moveSpeed;
            HideCoreHealthBar();
        }

        // 自分のコアのHPはHUDに出ているので、城内でカメラの前を塞ぐ頭上のバーは消す。
        private static void HideCoreHealthBar()
        {
            var core = GameObject.Find("DemonCastle_Core");
            var bar = core != null ? core.GetComponentInChildren<Game.UI.HealthBarUI>(true) : null;
            if (bar != null) bar.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (inputs == null) { inputs = new DemonKingInputActions(); Game.Core.KeyBindings.Track(inputs.asset); }
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
            if (Game.UI.InGameMenu.BlocksGameInput) return;
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
            transform.Rotate(0f, look.x * lookSensitivity * Game.Core.GameSettings.MouseSensitivity, 0f, Space.World);

            // Pitch: rotate pitchTarget (ShoulderTarget) - Cinemachine TPS follows its orientation
            if (pitchTarget != null)
            {
                pitch = Mathf.Clamp(pitch + -look.y * lookSensitivity * Game.Core.GameSettings.MouseSensitivity, pitchMin, pitchMax);
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
            if (modelAnim != null && modelAnim.Body != null)
            {
                airTime = controller.isGrounded ? 0f : airTime + Time.deltaTime;
                modelAnim.Body.Airborne = airTime > 0.15f;
                modelAnim.Body.VerticalSpeed = verticalVelocity;
            }
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
