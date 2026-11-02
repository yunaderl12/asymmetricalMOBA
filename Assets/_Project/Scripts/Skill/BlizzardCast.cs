using System;
using UnityEngine;
using UnityEngine.AI;
using Game.Combat;
using Game.Hero;

namespace Game.Skill
{
    /// Blizzard の溜めと発射。溜めの間は視線の先の地面に着弾点の輪を出し、発射の瞬間の場所へ氷を走らせる。
    public class BlizzardCast : MonoBehaviour
    {
        private enum Phase { Idle, Charging, Recover }

        private const float HoldForward = 1f;
        private const float HoldDown = 0.34f;
        private const float RecoverSeconds = 0.3f;
        private const float ShakeSeconds = 0.32f;
        private const float ShakeSize = 0.13f;
        private const float CameraThrust = 0.14f;
        private const float WallBackOff = 0.4f;
        private const float WalkableSearch = 6f;
        private const float RingWidth = 0.08f;
        private const int RaycastBuffer = 16;

        private static readonly Color IcePale = new Color(0.75f, 0.95f, 1f);
        private static readonly Color IceBlue = new Color(0.25f, 0.65f, 1f);
        private static readonly RaycastHit[] HitBuffer = new RaycastHit[RaycastBuffer];

        private HealthComponent health;
        private HeroFPSController controller;
        private CharacterModelAnimator modelAnim;
        private Transform cameraTransform;

        private Phase phase = Phase.Idle;
        private SkillData skill;
        private Func<bool> tryPay;
        private Func<float> getFieldSeconds;
        private Action<Vector3> onImpact;
        private float timer;
        private float shakeTimer;
        private Vector3 targetPoint;
        private MeshRenderer targetRing;
        private MeshRenderer handGlow;
        private ParticleSystem handEmbers;
        private MaterialPropertyBlock block;

        public bool IsCasting => phase == Phase.Charging;
        public bool IsPosing => phase != Phase.Idle;

        private void Awake()
        {
            health = GetComponent<HealthComponent>();
            controller = GetComponent<HeroFPSController>();
        }

        private void OnDisable()
        {
            if (phase != Phase.Idle) EndPose();
        }

        /// tryPay は発射の瞬間に呼ばれ、false なら取りやめる。onImpact は氷が着弾点に届いたときに呼ばれる。
        public bool BeginCast(SkillData skillData, Transform cam, Func<bool> tryPay, Func<float> getFieldSeconds, Action<Vector3> onImpact)
        {
            if (phase == Phase.Charging) return false;
            skill = skillData;
            this.tryPay = tryPay;
            this.getFieldSeconds = getFieldSeconds;
            this.onImpact = onImpact;
            cameraTransform = cam;
            if (modelAnim == null) modelAnim = GetComponent<CharacterModelAnimator>();
            if (block == null) block = new MaterialPropertyBlock();

            phase = Phase.Charging;
            timer = 0f;
            if (controller != null) controller.MovementLocked = skill.castLocksMovement;
            if (modelAnim != null) modelAnim.CastLocked = true;
            BuildChargeVisuals();
            UpdateTarget();
            Debug.Log($"[Skill] Blizzard charging ({skill.castTime}s)");
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
                Debug.Log($"[Skill] Blizzard cancelled ({reason})");
                EndPose();
                return;
            }

            timer += Time.deltaTime;
            float k = skill.castTime > 0f ? Mathf.Clamp01(timer / skill.castTime) : 1f;
            Vector3 hold = HoldPoint();
            handGlow.transform.position = hold;
            handGlow.transform.localScale = Vector3.one * Mathf.Lerp(0.08f, 0.35f, k * k);
            handEmbers.transform.position = hold;
            if (modelAnim != null) modelAnim.SetCastPose(-k, k, hold);
            if (controller != null) controller.SetCameraKick(new Vector3(0f, 0f, -CameraThrust * k), 0f);
            UpdateTarget();
            if (k >= 1f) Release();
        }

        private void Release()
        {
            if (tryPay != null && !tryPay())
            {
                Debug.Log("[Skill] Blizzard cancelled (cannot pay)");
                EndPose();
                return;
            }
            Vector3 hold = HoldPoint();
            Vector3 aim = controller != null ? controller.AimForward : transform.forward;
            FireballVfx.SpawnSparks(hold, aim, 18, IcePale, IceBlue);
            FireFlash.Spawn(hold, 1.6f, 0.12f, 0f, new Color(1.2f, 2f, 2.4f), new Color(0.1f, 0.7f, 1.4f), IcePale, 0f);
            DestroyChargeVisuals();

            BlizzardStrike.Spawn(transform.position, targetPoint, skill.areaRadius, getFieldSeconds(), HandleImpact);

            phase = Phase.Recover;
            timer = 0f;
            if (controller != null) controller.MovementLocked = false;
            if (modelAnim != null) modelAnim.CastLocked = false;
        }

        private void HandleImpact(Vector3 point)
        {
            onImpact?.Invoke(point);
            if (this != null && GetComponent<HumanHeroMarker>() != null) shakeTimer = ShakeSeconds;
        }

        private void UpdateRecover()
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / RecoverSeconds);
            float settle = (1f - t) * (1f - t);
            if (modelAnim != null) modelAnim.SetCastPose(0.7f * settle, settle, HoldPoint());
            if (controller != null) controller.SetCameraKick(new Vector3(0f, 0f, CameraThrust * settle), 0f);
            if (t >= 1f) EndPose();
        }

        private void ApplyShake()
        {
            if (shakeTimer <= 0f) return;
            shakeTimer -= Time.unscaledDeltaTime;
            if (controller == null || phase != Phase.Idle) return;
            float size = ShakeSize * Mathf.Clamp01(shakeTimer / ShakeSeconds);
            controller.SetCameraKick(shakeTimer > 0f ? UnityEngine.Random.insideUnitSphere * size : Vector3.zero, 0f);
        }

        // 視線が最初に当たる場所の真下を、歩ける地面へ寄せる。
        private void UpdateTarget()
        {
            Vector3 origin = cameraTransform != null ? cameraTransform.position : transform.position + Vector3.up * 1.7f;
            Vector3 aim = controller != null ? controller.AimForward : transform.forward;
            float distance = skill.range;
            int count = Physics.RaycastNonAlloc(origin, aim, HitBuffer, skill.range, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (HitBuffer[i].collider.transform.IsChildOf(transform)) continue;
                distance = Mathf.Min(distance, HitBuffer[i].distance);
            }
            Vector3 point = IceField.SnapToGround(origin + aim * Mathf.Max(0f, distance - WallBackOff));
            if (NavMesh.SamplePosition(point, out var walkable, WalkableSearch, NavMesh.AllAreas)) point = walkable.position;
            targetPoint = point;

            targetRing.transform.position = point + Vector3.up * 0.06f;
            block.SetColor("_Color", new Color(IcePale.r, IcePale.g, IcePale.b, 0.9f));
            block.SetFloat("_Ring", 1f / (1f + RingWidth));
            block.SetFloat("_Width", RingWidth);
            targetRing.SetPropertyBlock(block);
        }

        private void BuildChargeVisuals()
        {
            targetRing = FireballVfx.NewMeshObject("BlizzardTarget", null, FireballVfx.QuadMesh, FireballVfx.GetMaterial("VFX/ShockRing"));
            targetRing.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            targetRing.transform.localScale = Vector3.one * (skill.areaRadius * 2f * (1f + RingWidth));

            handGlow = FireballVfx.NewMeshObject("BlizzardHandGlow", null, FireballVfx.QuadMesh, FireballVfx.Glow);
            var glowBlock = new MaterialPropertyBlock();
            glowBlock.SetColor("_Color", new Color(0.5f, 1.6f, 2.2f));
            glowBlock.SetFloat("_Pull", 0f);
            handGlow.SetPropertyBlock(glowBlock);
            handGlow.transform.position = HoldPoint();

            handEmbers = FireballVfx.NewParticles("BlizzardHandEmbers", null);
            handEmbers.transform.position = HoldPoint();
            var main = handEmbers.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = 0.25f;
            main.startSpeed = -1.8f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.03f);
            main.startColor = new ParticleSystem.MinMaxGradient(IcePale, IceBlue);
            main.maxParticles = 50;
            var emission = handEmbers.emission;
            emission.rateOverTime = 70f;
            var shape = handEmbers.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.45f;
            shape.radiusThickness = 0f;
            handEmbers.gameObject.SetActive(true);
            handEmbers.Play();
        }

        private void DestroyChargeVisuals()
        {
            if (targetRing != null) Destroy(targetRing.gameObject);
            if (handGlow != null) Destroy(handGlow.gameObject);
            if (handEmbers != null) Destroy(handEmbers.gameObject);
            targetRing = null;
            handGlow = null;
            handEmbers = null;
        }

        private void EndPose()
        {
            phase = Phase.Idle;
            DestroyChargeVisuals();
            if (controller != null)
            {
                controller.MovementLocked = false;
                controller.SetCameraKick(Vector3.zero, 0f);
            }
            if (modelAnim != null)
            {
                modelAnim.CastLocked = false;
                modelAnim.SetCastPose(0f, 0f, Vector3.zero);
            }
        }

        private Vector3 HoldPoint()
        {
            if (cameraTransform == null) return transform.position + Vector3.up * 1.4f + transform.forward * 0.7f;
            return cameraTransform.position + cameraTransform.forward * HoldForward - cameraTransform.up * HoldDown;
        }
    }
}
