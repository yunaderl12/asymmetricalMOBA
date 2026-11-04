using System;
using UnityEngine;
using Game.Combat;
using Game.Hero;

namespace Game.Skill
{
    /// 火球の溜めと発射、その前後の構え。
    /// 溜め: 後ろへ反り、両手の間で火球を育てる / 発射: 前へ踏み込んで戻る。
    /// 自分の勇者モデルは1人称カメラに映らないので、同じ動きをカメラでも表現する。
    public class FireballCast : MonoBehaviour
    {
        private enum Phase { Idle, Charging, Recover }

        private const float ChargedRadius = 0.15f;    // 視界をふさがない大きさ
        private const float FlightRadius = 0.3f;      // 離れても火球だと分かる大きさ
        private const float StartRadius = 0.04f;
        private const float WindUpRatio = 0.3f;       // 溜め時間のうち、反りきるまでの割合
        private const float RecoverSeconds = 0.42f;
        private const float ThrustSeconds = 0.07f;
        private const float HoldForward = 1f;         // カメラから火球までの距離
        private const float HoldDown = 0.34f;
        private const float CameraPullBack = 0.12f;
        private const float CameraLeanPitch = -3f;
        private const float CameraThrust = 0.16f;
        private const float CameraThrustPitch = 2.5f;
        private const float FovCharge = -2.5f;
        private const float FovThrust = 5f;
        private const float ShakeSeconds = 0.22f;
        private const float ShakeSize = 0.05f;

        private HealthComponent health;
        private HeroFPSController controller;
        private CharacterModelAnimator modelAnim;
        private Transform cameraTransform;
        private Camera heroCamera;
        private float baseFov;

        private Phase phase = Phase.Idle;
        private SkillData skill;
        private Func<bool> tryPay;
        private Action<HealthComponent> onHit;
        private float sizeScale = 1f;
        private FireballVisual visual;
        private float timer;
        private float shakeTimer;
        private bool cameraTouched;
        private Vector3 lastHoldPoint;

        public bool IsCasting => phase == Phase.Charging;
        /// 溜めか発射後の戻りの途中（カメラを動かしている間）。
        public bool IsPosing => phase != Phase.Idle;

        private void Awake()
        {
            health = GetComponent<HealthComponent>();
            controller = GetComponent<HeroFPSController>();
        }

        private void OnDisable()
        {
            if (phase == Phase.Charging) Cancel("disabled");
            EndPose();
        }

        /// tryPay は発射の瞬間に呼ばれ、false なら発射をやめる。onHit は敵に当たったときに呼ばれる。
        public bool BeginCast(SkillData skillData, Transform cam, float sizeScale, Func<bool> tryPay, Action<HealthComponent> onHit)
        {
            this.sizeScale = sizeScale;
            if (phase == Phase.Charging) return false;
            skill = skillData;
            this.tryPay = tryPay;
            this.onHit = onHit;
            cameraTransform = cam;
            if (modelAnim == null) modelAnim = GetComponent<CharacterModelAnimator>();
            if (heroCamera == null && cam != null)
            {
                heroCamera = cam.GetComponent<Camera>();
                if (heroCamera != null) baseFov = heroCamera.fieldOfView;
            }

            phase = Phase.Charging;
            timer = 0f;
            visual = FireballVisual.Create(HoldPoint());
            if (controller != null) controller.MovementLocked = skill.castLocksMovement;
            if (modelAnim != null) modelAnim.CastLocked = true;
            Debug.Log($"[Skill] Fireball charging ({skill.castTime}s)");
            return true;
        }

        private void Update()
        {
            switch (phase)
            {
                case Phase.Charging: UpdateCharge(); break;
                case Phase.Recover: UpdateRecover(); break;
            }
            ApplyShake();
        }

        private void UpdateCharge()
        {
            string reason = null;
            if (health != null && health.IsDead) reason = "dead";
            else if (!StatusEffectController.CanUseSkills(gameObject)) reason = "stunned / silenced";
            else if (modelAnim != null && modelAnim.IsRespawnAnimPlaying) reason = "respawning";
            if (reason != null)
            {
                Cancel(reason);
                return;
            }

            timer += Time.deltaTime;
            float k = skill.castTime > 0f ? Mathf.Clamp01(timer / skill.castTime) : 1f;
            float windUp = EaseOut(Mathf.Clamp01(k / WindUpRatio));

            // 終盤ほど脈打ちを大きくして「もう放たれる」感を出す。
            float pulse = 1f + Mathf.Sin(timer * 34f) * 0.05f * k;
            float radius = Mathf.Lerp(StartRadius, ChargedRadius, EaseOut(k)) * sizeScale * pulse;

            lastHoldPoint = HoldPoint();
            visual.transform.position = lastHoldPoint;
            visual.SetShape(radius, 1f, 1f, 0f);

            if (modelAnim != null) modelAnim.SetCastPose(-windUp, windUp, lastHoldPoint);
            SetCamera(new Vector3(0f, 0.02f * windUp, -CameraPullBack * windUp), CameraLeanPitch * windUp, FovCharge * windUp);

            if (k >= 1f) Release();
        }

        private void Release()
        {
            if (tryPay != null && !tryPay())
            {
                Cancel("cannot pay");
                return;
            }

            Vector3 orbPosition = visual.transform.position;
            Vector3 aimOrigin = cameraTransform != null ? cameraTransform.position : orbPosition;
            Vector3 aimForward = controller != null ? controller.AimForward : transform.forward;
            Vector3 aimPoint = aimOrigin + aimForward * skill.range;
            if (FireballProjectile.Probe(aimOrigin, aimForward, 0f, skill.range, gameObject, out var aimed)) aimPoint = aimed.Point;

            Vector3 toAim = aimPoint - orbPosition;
            // 至近距離は照準点へ向けると真横に飛びかねないので、視線方向へ飛ばす。
            Vector3 direction = toAim.magnitude > 1.5f ? toAim.normalized : aimForward;
            var launched = visual;
            visual = null;
            FireballProjectile.Launch(launched, direction, skill.projectileSpeed, skill.range, toAim.magnitude, ChargedRadius * sizeScale, FlightRadius * sizeScale, gameObject, OnProjectileHit);

            phase = Phase.Recover;
            timer = 0f;
            if (controller != null) controller.MovementLocked = false;
            if (modelAnim != null) modelAnim.CastLocked = false;
        }

        // 素早く前へ踏み込み、少し長めに戻る。
        private void UpdateRecover()
        {
            timer += Time.deltaTime;
            if (timer >= RecoverSeconds)
            {
                EndPose();
                return;
            }
            float lean, reach, camera;
            if (timer < ThrustSeconds)
            {
                float t = EaseOut(timer / ThrustSeconds);
                lean = Mathf.Lerp(-1f, 0.85f, t);
                reach = 1f;
                camera = t;
            }
            else
            {
                float t = (timer - ThrustSeconds) / (RecoverSeconds - ThrustSeconds);
                float settle = (1f - t) * (1f - t);
                lean = 0.85f * settle;
                reach = settle;
                camera = settle;
            }
            Vector3 thrustPoint = HoldPoint();
            if (modelAnim != null) modelAnim.SetCastPose(lean, reach, thrustPoint);
            SetCamera(new Vector3(0f, 0f, CameraThrust * camera), CameraThrustPitch * camera, FovThrust * camera);
        }

        private void OnProjectileHit(HealthComponent hp, Vector3 point)
        {
            if (this == null || hp == null) return;
            onHit?.Invoke(hp);
            // 自分の攻撃が当たったときだけ、手応えとして止めと揺れを入れる。
            if (GetComponent<HumanHeroMarker>() != null)
            {
                HitStop.Play(skill.hitStopSeconds);
                shakeTimer = ShakeSeconds;
            }
        }

        private void ApplyShake()
        {
            if (shakeTimer <= 0f) return;
            shakeTimer -= Time.unscaledDeltaTime;
            if (controller == null || phase != Phase.Idle) return;
            float size = ShakeSize * Mathf.Clamp01(shakeTimer / ShakeSeconds);
            Vector3 offset = shakeTimer > 0f ? UnityEngine.Random.insideUnitSphere * size : Vector3.zero;
            controller.SetCameraKick(offset, 0f);
        }

        private void Cancel(string reason)
        {
            phase = Phase.Idle;
            if (visual != null) Destroy(visual.gameObject);
            visual = null;
            EndPose();
            Debug.Log($"[Skill] Fireball cancelled ({reason})");
        }

        private void EndPose()
        {
            phase = Phase.Idle;
            if (controller != null) controller.MovementLocked = false;
            if (modelAnim != null)
            {
                modelAnim.CastLocked = false;
                modelAnim.SetCastPose(0f, 0f, Vector3.zero);
            }
            if (cameraTouched) SetCamera(Vector3.zero, 0f, 0f);
            cameraTouched = false;
        }

        private void SetCamera(Vector3 position, float pitch, float fovOffset)
        {
            cameraTouched = true;
            if (controller != null) controller.SetCameraKick(position, pitch);
            if (heroCamera != null) heroCamera.fieldOfView = baseFov + fovOffset;
        }

        private Vector3 HoldPoint()
        {
            if (cameraTransform == null) return transform.position + Vector3.up * 1.4f + transform.forward * 0.7f;
            return cameraTransform.position + cameraTransform.forward * HoldForward - cameraTransform.up * HoldDown;
        }

        private static float EaseOut(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }
    }
}
