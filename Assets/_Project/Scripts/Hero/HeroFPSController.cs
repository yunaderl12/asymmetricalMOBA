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
        [Tooltip("Metres per step. Longer = fewer bobs per second.")]
        [SerializeField] private float stepLength = 3.2f;
        [SerializeField] private float dashStepLength = 4.4f;
        [Tooltip("How far the camera sinks in the middle of each step (m). Up-and-down only, no roll.")]
        [SerializeField] private float stepDip = 0.035f;
        [Tooltip("How fast the camera falls behind when a dash starts (m/s)")]
        [SerializeField] private float dashStartKick = 5f;
        [SerializeField] private float dashStopKick = 2f;
        [SerializeField] private float dashLagSmoothing = 0.2f;
        [SerializeField] private float dashLagBack = 0.32f;
        [SerializeField] private float dashLagForward = 0.14f;

        public float MoveSpeed => moveSpeed;
        private Game.Core.TerrainWalker terrain;
        public void SetMoveSpeed(float value) { moveSpeed = value; }
        public bool LookEnabled { get; set; } = true;
        /// 溜め中など、視点だけ動かせて移動できない状態。
        public bool MovementLocked { get; set; }
        /// スキルが勇者を直接動かしている間（Teleport の飛行中）は、移動も視点も重力も止める。
        public bool Suspended { get; set; }
        /// スキルが勇者を上下に動かしている間（Stomp の跳躍）は、視点だけ動かせて、移動と重力を止める。腕は見えたまま。
        public bool SkillMotion { get; set; }
        /// パッシブの回避など、入力の代わりに体を動かす水平速度。ゼロなら入力で動く。
        public Vector3 BurstVelocity { get; set; }
        /// 空中でジャンプを押したときに呼ぶ。上昇速度を返す（0 なら跳ばない）。
        public System.Func<float> AirJump { get; set; }
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
        private float shakeStrength;
        private float shakeSeconds;
        private float shakeTimer;
        private HeroDeathCamera deathCamera;
        private Camera viewCamera;
        private float designFov = -1f;
        private float skillFov;
        private float speedFov;
        private float speedFovVelocity;
        private ParticleSystem dashLines;
        private static readonly Color DashLineColor = new Color(1f, 1f, 1f, 0.32f);
        private const float DashLineRate = 120f;
        private Vector3 hitPushVelocity;
        private float hitPushTimer;
        private const float HitPushSeconds = 0.15f;
        private const float HitShakeSeconds = 0.18f;
        private const float ShakeFrequency = 28f;
        private const float ShakeRollPerMeter = 12f; // 揺れ 1m あたりの傾き（度）

        private bool IsAnimLocked()
        {
            if (modelAnim == null) modelAnim = GetComponent<CharacterModelAnimator>();
            return modelAnim != null && modelAnim.IsRespawnAnimPlaying;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            terrain = Game.Core.TerrainWalker.For(gameObject);
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

        /// スキル演出による視野角のずれ（度）。0 で元に戻す。視野角はここでまとめて決める（走りの広がりと足し合わせる）。
        public void SetSkillFov(float offsetDegrees)
        {
            skillFov = offsetDegrees;
        }

        // 走ると視野が少し広がり、ダッシュ中はさらに広がって、視界の外へ流れる線が出る（スピード感）。
        private void UpdateFov()
        {
            if (viewCamera == null && cameraTransform != null)
            {
                viewCamera = cameraTransform.GetComponent<Camera>();
                if (viewCamera != null) designFov = viewCamera.fieldOfView;
            }
            if (viewCamera == null) return;
            var cfg = Game.Core.BalanceConfig.Instance;
            Vector3 velocity = controller != null && controller.enabled ? controller.velocity : Vector3.zero;
            velocity.y = 0f;
            float speed = velocity.magnitude;
            bool fast = dashing && speed > moveSpeed * 0.9f;
            float target = fast ? cfg.dashFovBoost : cfg.runFovBoost * Mathf.Clamp01(speed / Mathf.Max(0.1f, moveSpeed));
            speedFov = Mathf.SmoothDamp(speedFov, target, ref speedFovVelocity, cfg.speedFovSmoothing);
            float fov = designFov + skillFov + speedFov;
            if (Mathf.Abs(viewCamera.fieldOfView - fov) > 0.001f) viewCamera.fieldOfView = fov;

            if (fast && dashLines == null && cfg.dashSpeedLines)
            {
                dashLines = MeleeFx.SpawnSpeedLines(cameraTransform, DashLineColor);
                var emission = dashLines.emission;
                emission.rateOverTime = DashLineRate;
            }
            else if (!fast && dashLines != null) StopDashLines();
        }

        private void StopDashLines()
        {
            if (dashLines == null) return;
            dashLines.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            dashLines = null;
        }

        /// 地響きなどの画面の揺れ。strength はカメラが動く幅 (m)。いま揺れているものより弱ければ足さない。
        /// 上へ打ち上げる（魔王のフィールド魔法「竜巻」）。今の上向きの速さより強いときだけ上書きする。
        public void Launch(float upSpeed)
        {
            if (upSpeed > verticalVelocity) verticalVelocity = upSpeed;
        }

        public bool IsGrounded => controller != null && controller.isGrounded;

        public void Shake(float strength, float seconds)
        {
            if (strength <= 0f || seconds <= 0f) return;
            float current = shakeTimer > 0f ? shakeStrength * shakeTimer / shakeSeconds : 0f;
            if (strength < current) return;
            shakeStrength = strength;
            shakeSeconds = seconds;
            shakeTimer = seconds;
        }

        /// at から radius 以内にいる自分の勇者の画面を揺らす。近いほど強い。
        public static void ShakeNear(Vector3 at, float strength, float radius, float seconds)
        {
            var hero = HumanHeroMarker.Instance;
            if (hero == null || radius <= 0f) return;
            float distance = Vector3.Distance(hero.transform.position, at);
            if (distance >= radius) return;
            var fps = hero.GetComponent<HeroFPSController>();
            if (fps != null) fps.Shake(strength * (1f - distance / radius), seconds);
        }

        // 揺れの位置と傾き。細かく速い揺れが、時間とともに弱まる。
        private void UpdateShake(out Vector3 position, out float roll)
        {
            position = Vector3.zero;
            roll = 0f;
            if (shakeTimer <= 0f) return;
            shakeTimer -= Time.deltaTime;
            float fade = Mathf.Clamp01(shakeTimer / shakeSeconds);
            float size = shakeStrength * fade * fade;
            float t = Time.time * ShakeFrequency;
            position = new Vector3(Mathf.PerlinNoise(t, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f, 0f) * 2f * size;
            roll = (Mathf.PerlinNoise(t, 5.1f) - 0.5f) * 2f * size * ShakeRollPerMeter;
        }

        private void LateUpdate()
        {
            if (cameraTransform == null) return;
            UpdateFov();
            // 倒れた自分やリスポーンを外から映しているあいだは、カメラを動かさない。
            if (deathCamera == null) deathCamera = GetComponent<HeroDeathCamera>();
            if (deathCamera != null && deathCamera.IsActive) return;
            UpdateRunFeel(out Vector3 feelPosition, out float feelRoll);
            UpdateShake(out Vector3 shakePosition, out float shakeRoll);
            feelPosition += shakePosition;
            feelRoll += shakeRoll;
            bool feel = feelPosition != Vector3.zero || feelRoll != 0f;
            if (!cameraKickDirty && !feel && !runFeelApplied) return;
            cameraTransform.localPosition = cameraBaseLocalPosition + cameraKickPosition + feelPosition;
            cameraTransform.localRotation = Quaternion.Euler(pitch + cameraKickPitch, 0f, cameraKickRoll + feelRoll);
            cameraKickDirty = cameraKickPosition != Vector3.zero || cameraKickPitch != 0f || cameraKickRoll != 0f;
            runFeelApplied = feel;
        }

        // 一歩ごとに縦に少し沈む揺れ（左右の揺れと傾きはなし）と、ダッシュの出だしで体に遅れるカメラ。照準（AimForward）には影響しない。
        private void UpdateRunFeel(out Vector3 position, out float roll)
        {
            float dt = Time.deltaTime;
            Vector3 velocity = controller.enabled ? controller.velocity : Vector3.zero;
            velocity.y = 0f;
            float speed = velocity.magnitude;
            bool running = !Suspended && controller.enabled && controller.isGrounded && speed > 0.5f;
            stepWeight = Mathf.MoveTowards(stepWeight, running ? 1f : 0f, dt * 6f);
            stepPhase += speed * dt / (dashing ? dashStepLength : stepLength) * Mathf.PI;

            // 一歩（stepPhase が π 進む）ごとに、なめらかに1回沈んで戻る。
            float sink = Mathf.Sin(stepPhase);
            float size = stepWeight * (dashing ? 1.3f : 1f);
            position = new Vector3(0f, -sink * sink * stepDip, 0f) * size;
            roll = 0f;

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
            HealthComponent.AnyDamage += OnAnyDamage;
            LockCursor(true);
        }

        private void OnDisable()
        {
            HealthComponent.AnyDamage -= OnAnyDamage;
            StopDashLines();
            // 死んでいるあいだなどは、走りの広がりを外して元の視野角に戻す。
            speedFov = speedFovVelocity = 0f;
            if (viewCamera != null && designFov > 0f) viewCamera.fieldOfView = designFov + skillFov;
            if (inputs == null) return;
            inputs.Hero.Pause.performed -= OnPause;
            inputs.Hero.Disable();
            LockCursor(false);
        }

        private void Update()
        {
            if (Game.UI.InGameMenu.BlocksGameInput || Suspended) return;
            if (IsAnimLocked())
            {
                // Keep gravity so the hero doesn't float, but ignore movement input.
                verticalVelocity += gravity * Time.deltaTime;
                if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = groundedStickForce;
                controller.Move(new Vector3(0f, verticalVelocity * Time.deltaTime, 0f));
                return;
            }
            HandleLook();
            if (SkillMotion)
            {
                verticalVelocity = 0f;
                return;
            }
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
            float sensitivity = lookSensitivity * Game.Core.GameSettings.MouseSensitivity;
            float yawDelta = look.x * sensitivity;
            float pitchDelta = -look.y * sensitivity;

            transform.Rotate(0f, yawDelta, 0f, Space.World);

            pitch = Mathf.Clamp(pitch + pitchDelta, pitchMin, pitchMax);
            if (cameraTransform != null)
            {
                cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
        }

        // 魔物や魔王に殴られたら、殴られた向きへほんの少し押され、画面も小さく揺れる（殴られたことに気づけるように）。
        private void OnAnyDamage(HealthComponent target, int amount, GameObject attacker)
        {
            if (target == null || target.gameObject != gameObject || attacker == null || attacker == gameObject) return;
            if (attacker.CompareTag("Player") || target.IsDead) return;
            var cfg = Game.Core.BalanceConfig.Instance;
            Vector3 away = transform.position - attacker.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = -transform.forward;
            // 速さは直線的に落ちるので、初速は平均の2倍。
            hitPushVelocity = away.normalized * (2f * cfg.heroHitKnockback / HitPushSeconds);
            hitPushTimer = HitPushSeconds;
            Shake(cfg.heroHitShake, HitShakeSeconds);
        }

        private Vector3 HitPush()
        {
            if (hitPushTimer <= 0f) return Vector3.zero;
            hitPushTimer -= Time.deltaTime;
            return hitPushVelocity * Mathf.Clamp01(hitPushTimer / HitPushSeconds);
        }

        private void HandleStunnedGravity()
        {
            verticalVelocity += gravity * Time.deltaTime;
            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = groundedStickForce;
            Vector3 motion = IceField.ApplySlip(transform.position, Vector3.zero, ref slipVelocity) + HitPush();
            motion.y = verticalVelocity;
            controller.Move(motion * Time.deltaTime);
        }

        private void HandleMove()
        {
            Vector2 input = inputs.Hero.Move.ReadValue<Vector2>();
            UpdateDash(input);
            float speed = dashing ? moveSpeed * Game.Core.BalanceConfig.Instance.dashSpeedMultiplier : moveSpeed;
            speed *= terrain.SpeedFactor;
            Vector3 horizontal = BurstVelocity != Vector3.zero ? BurstVelocity : (transform.right * input.x + transform.forward * input.y) * speed;

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
                if (AirJump != null && inputs.Hero.Jump.WasPerformedThisFrame())
                {
                    float lift = AirJump();
                    if (lift > 0f) verticalVelocity = lift;
                }
            }

            Vector3 motion = IceField.ApplySlip(transform.position, horizontal, ref slipVelocity) + HitPush();
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
            // 試合中は ESC で InGameMenu が開き、カーソルもそちらで出し入れする。
            if (Game.UI.InGameMenu.Instance != null) return;
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
