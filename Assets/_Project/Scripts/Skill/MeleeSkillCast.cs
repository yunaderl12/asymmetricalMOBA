using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Hero;

namespace Game.Skill
{
    /// 溜め→発動→余韻で動く近接スキルの共通部分。SPとクールタイムは発動の瞬間に消費し、溜めが中断されたら消費しない。
    public abstract class MeleeSkillCast : MonoBehaviour
    {
        private enum Phase { Idle, WindUp, Active, Recover }

        private const float BodySearchMargin = 3f;
        private const float CloseEnoughForAnyAngle = 0.5f;

        private static readonly List<HealthComponent> FoundBuffer = new List<HealthComponent>();
        private static readonly HashSet<HealthComponent> SeenBuffer = new HashSet<HealthComponent>();

        protected SkillData Skill { get; private set; }
        protected Transform CameraTransform { get; private set; }
        protected HeroFPSController Controller { get; private set; }
        protected CharacterModelAnimator ModelAnim { get; private set; }
        protected HeroViewModel View { get; private set; }
        protected bool IsLocalPlayer { get; private set; }

        private HealthComponent health;
        private Camera heroCamera;
        private float baseFov;
        private Phase phase = Phase.Idle;
        private float timer;
        private Func<bool> tryPay;
        private Action<HealthComponent> onHit;
        private Action onReleased;
        private Vector3 cameraPosition;
        private float cameraPitch, cameraRoll, cameraFov;
        private bool cameraTouched;
        private float shakeTimer, shakeSeconds, shakeSize;

        public bool IsBusy => phase != Phase.Idle;

        /// 余韻の秒数（この間に構えを戻す）。
        protected abstract float RecoverSeconds { get; }
        protected abstract void OnWindUp(float k);
        protected abstract void OnRelease();
        /// 発動中の動き。終わったら false を返す。
        protected virtual bool TickActive(float seconds) { return false; }
        protected abstract void OnRecover(float seconds, float k);
        protected virtual void OnPoseEnd() { }

        protected virtual void Awake()
        {
            health = GetComponent<HealthComponent>();
            Controller = GetComponent<HeroFPSController>();
            IsLocalPlayer = GetComponent<HumanHeroMarker>() != null;
        }

        private void OnDisable()
        {
            EndPose();
        }

        public bool BeginCast(SkillData skillData, Transform cam, Func<bool> tryPay, Action<HealthComponent> onHit, Action onReleased)
        {
            if (IsBusy) return false;
            Skill = skillData;
            CameraTransform = cam;
            this.tryPay = tryPay;
            this.onHit = onHit;
            this.onReleased = onReleased;
            if (ModelAnim == null) ModelAnim = GetComponent<CharacterModelAnimator>();
            if (View == null) View = GetComponent<HeroViewModel>();
            if (heroCamera == null && cam != null)
            {
                heroCamera = cam.GetComponent<Camera>();
                if (heroCamera != null) baseFov = heroCamera.fieldOfView;
            }

            phase = Phase.WindUp;
            timer = 0f;
            if (Controller != null) Controller.MovementLocked = Skill.castLocksMovement;
            if (ModelAnim != null) ModelAnim.CastLocked = true;
            Debug.Log($"[Skill] {Skill.skillName} winding up ({Skill.castTime}s)");
            return true;
        }

        private void Update()
        {
            ClearCameraTargets();
            switch (phase)
            {
                case Phase.WindUp: UpdateWindUp(); break;
                case Phase.Active: UpdateActive(); break;
                case Phase.Recover: UpdateRecover(); break;
            }
            ApplyCamera();
        }

        private void UpdateWindUp()
        {
            string reason = null;
            if (health != null && health.IsDead) reason = "dead";
            else if (!StatusEffectController.CanUseSkills(gameObject)) reason = "stunned / silenced";
            else if (ModelAnim != null && ModelAnim.IsRespawnAnimPlaying) reason = "respawning";
            if (reason != null)
            {
                Debug.Log($"[Skill] {Skill.skillName} cancelled ({reason})");
                EndPose();
                return;
            }

            timer += Time.deltaTime;
            float k = Skill.castTime > 0f ? Mathf.Clamp01(timer / Skill.castTime) : 1f;
            OnWindUp(k);
            if (k >= 1f) Release();
        }

        private void Release()
        {
            if (tryPay != null && !tryPay())
            {
                Debug.Log($"[Skill] {Skill.skillName} cancelled (cannot pay)");
                EndPose();
                return;
            }
            onReleased?.Invoke();
            phase = Phase.Active;
            timer = 0f;
            OnRelease();
        }

        private void UpdateActive()
        {
            bool stopped = (health != null && health.IsDead) || StatusEffectController.IsStunnedObj(gameObject);
            timer += Time.deltaTime;
            if (!stopped && TickActive(timer)) return;
            phase = Phase.Recover;
            timer = 0f;
            if (Controller != null) Controller.MovementLocked = false;
            if (ModelAnim != null) ModelAnim.CastLocked = false;
            OnRecover(0f, 0f);
        }

        private void UpdateRecover()
        {
            timer += Time.deltaTime;
            if (timer >= RecoverSeconds)
            {
                EndPose();
                return;
            }
            OnRecover(timer, timer / RecoverSeconds);
        }

        private void EndPose()
        {
            phase = Phase.Idle;
            if (Controller != null) Controller.MovementLocked = false;
            if (ModelAnim != null)
            {
                ModelAnim.CastLocked = false;
                ModelAnim.ClearMeleePose();
            }
            OnPoseEnd();
        }

        /// スキルの動きに合わせた今フレームのカメラのずれ（揺れは自動で足す）。
        protected void SetCamera(Vector3 position, float pitch, float roll, float fovOffset)
        {
            cameraPosition = position;
            cameraPitch = pitch;
            cameraRoll = roll;
            cameraFov = fovOffset;
            cameraTouched = true;
        }

        protected void Shake(float seconds, float size)
        {
            shakeTimer = shakeSeconds = seconds;
            shakeSize = size;
            cameraTouched = true;
        }

        private void ClearCameraTargets()
        {
            cameraPosition = Vector3.zero;
            cameraPitch = cameraRoll = cameraFov = 0f;
        }

        private void ApplyCamera()
        {
            if (!cameraTouched) return;
            Vector3 shake = Vector3.zero;
            if (shakeTimer > 0f)
            {
                shakeTimer -= Time.unscaledDeltaTime;
                shake = UnityEngine.Random.insideUnitSphere * (shakeSize * Mathf.Clamp01(shakeTimer / shakeSeconds));
            }
            if (Controller != null) Controller.SetCameraKick(cameraPosition + shake, cameraPitch, cameraRoll);
            if (heroCamera != null) heroCamera.fieldOfView = baseFov + cameraFov;
            if (phase == Phase.Idle && shakeTimer <= 0f)
            {
                if (Controller != null) Controller.SetCameraKick(Vector3.zero, 0f, 0f);
                if (heroCamera != null) heroCamera.fieldOfView = baseFov;
                cameraTouched = false;
            }
        }

        /// 体の位置から半径内の敵を集める。coneDegrees が 0 以上なら forward を中心にその角度の扇の中だけ。壁の向こうは含めない。
        protected List<HealthComponent> FindEnemies(Vector3 origin, float radius, Vector3 forward, float coneDegrees)
        {
            FoundBuffer.Clear();
            SeenBuffer.Clear();
            forward.y = 0f;
            foreach (var col in Physics.OverlapSphere(origin, radius + BodySearchMargin, ~0, QueryTriggerInteraction.Collide))
            {
                var hp = col.GetComponentInParent<HealthComponent>();
                if (hp == null || hp.IsDead || hp.CompareTag("Player") || hp.gameObject == gameObject || !SeenBuffer.Add(hp)) continue;

                Bounds body = FireballProjectile.BodyBoundsOf(hp.gameObject, col);
                Vector3 closest = body.ClosestPoint(origin);
                if ((closest - origin).sqrMagnitude > radius * radius) continue;
                if (coneDegrees >= 0f)
                {
                    Vector3 flat = closest - origin;
                    flat.y = 0f;
                    if (flat.magnitude > CloseEnoughForAnyAngle && Vector3.Angle(forward, flat) > coneDegrees * 0.5f) continue;
                }
                if (IsBlocked(origin, body.center)) continue;
                FoundBuffer.Add(hp);
            }
            return FoundBuffer;
        }

        private bool IsBlocked(Vector3 from, Vector3 to)
        {
            if (!Physics.Linecast(from, to, out var hit, ~0, QueryTriggerInteraction.Ignore)) return false;
            return hit.collider.GetComponentInParent<HealthComponent>() == null;
        }

        /// ダメージと被弾の演出。当たった数を返す。
        protected int HitEnemies(List<HealthComponent> targets, Vector3 from, float flashSize)
        {
            int count = targets.Count;
            for (int i = 0; i < count; i++)
            {
                var hp = targets[i];
                Bounds body = FireballProjectile.BodyBoundsOf(hp.gameObject, null);
                Vector3 point = body.ClosestPoint(from);
                onHit?.Invoke(hp);
                MeleeFx.Impact(point, point - from, flashSize);
                if (hp == null) continue;
                FireHitEffect.PlayFlash(hp.gameObject);
                ImpactPunch.Play(hp.gameObject, hp.transform.position - transform.position);
            }
            if (count > 0 && IsLocalPlayer) HitStop.Play(Skill.hitStopSeconds);
            return count;
        }

        protected Vector3 Chest => transform.position + Vector3.up * 1.2f;

        protected static float EaseOut(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }

        protected static float EaseIn(float t)
        {
            return t * t;
        }
    }
}
