using System;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Hero;

namespace Game.Skill
{
    /// Teleport: はじまりのまちへの帰還。
    /// 溜め（つぶれて光を吸い込む。攻撃を受けると中止）→ 細い光に伸びて消える
    /// → 真上へ昇る → 上空でホバーしてまちの方を向く → 光の軌跡を残してまちへ降りる → 着地の衝撃。
    /// 1人称カメラは勇者と一緒に飛ぶ。飛んでいる間は当たり判定がなく、操作もできない。
    public class HeroTeleport : MonoBehaviour
    {
        private enum Phase { Idle, WindUp, Stretch, Rise, Hover, Travel, Landing }

        private static readonly Color Neon = new Color(0.1f, 1f, 1f);
        private static readonly Color NeonDeep = new Color(0f, 0.55f, 1f);
        // シアンの光のままだと茶色や緑の地面が緑色に光るので、点光源だけ白に寄せて弱くする。
        private static readonly Color LightTint = new Color(0.6f, 0.92f, 1f);
        private const float FlashLightIntensity = 1.5f;

        private const float StretchSeconds = 0.1f;
        private const float WindUpSquash = 0.4f;
        private const float StretchThin = 0.12f;
        private const float StretchTall = 3.5f;
        private const float ArcHeightPerDistance = 0.2f;
        private const float ChargeSquash = 0.3f;
        private const float FinalSquashSeconds = 0.5f;
        private const float WindUpRingSeconds = 1f;
        private const float EmberRateStart = 25f;
        private const float EmberRateEnd = 110f;
        private const float HoverBob = 0.5f;
        private const float TravelGlide = 0.55f;
        private const float TravelDrop = 0.45f;
        private const float RiseLookBlend = 0.3f;
        private const float SpeedLineRateRise = 60f;
        private const float SpeedLineRateTravel = 110f;
        private const float CameraCrouch = 0.3f;
        private const float CameraRise = 0.9f;
        private const float FovWindUp = -6f;
        private const float FovFlight = 18f;
        private const float FovHover = 6f;
        private const float FovLanding = -10f;
        private const float LandingCameraDrop = 0.45f;
        private const float MaxFlightPitch = 70f;
        private const float TravelBlendRatio = 0.2f;
        private const float LandingRingRadius = 7f;
        // 軌跡の先頭をカメラより後ろに置き、降下中に画面を覆わないようにする。
        private const float TrailBehindCamera = 5f;
        private const float SpeedLineLead = 20f;
        private const float LightHeadSize = 4f;

        private HealthComponent health;
        private CharacterController cc;
        private HeroFPSController controller;
        private CharacterModelAnimator modelAnim;
        private Transform cameraTransform;
        private Camera heroCamera;
        private float baseFov;
        private Transform model;
        private Vector3 modelBaseScale;

        private Phase phase = Phase.Idle;
        private SkillData skill;
        private Func<bool> tryPay;
        private float timer;
        private float nextRingTime;
        private bool damagedInWindUp;
        private Vector3 groundPosition, hoverPosition;
        private Quaternion startYaw, travelYaw;
        private Vector3 p0, p1, p2, p3;
        private ShockRing windUpRing;
        private ParticleSystem pullEmbers;
        private ParticleSystem speedLines;
        private TrailRenderer lightTrail;
        private GameObject lightHead;

        public bool IsBusy => phase != Phase.Idle;

        private void Awake()
        {
            health = GetComponent<HealthComponent>();
            cc = GetComponent<CharacterController>();
            controller = GetComponent<HeroFPSController>();
        }

        private void OnEnable()
        {
            HealthComponent.AnyDamage += HandleAnyDamage;
        }

        private void OnDisable()
        {
            HealthComponent.AnyDamage -= HandleAnyDamage;
            if (phase != Phase.Idle) EndAll();
        }

        private void HandleAnyDamage(HealthComponent target, int amount, GameObject attacker)
        {
            if (phase == Phase.WindUp && target == health && amount > 0) damagedInWindUp = true;
        }

        /// tryPay は飛び立つ瞬間に呼ばれ、false なら取りやめる（溜めの中断ではSPを消費しない）。
        public bool BeginCast(SkillData skillData, Transform cam, Func<bool> tryPay)
        {
            if (IsBusy) return false;
            skill = skillData;
            this.tryPay = tryPay;
            cameraTransform = cam;
            if (modelAnim == null) modelAnim = GetComponent<CharacterModelAnimator>();
            model = modelAnim != null ? modelAnim.ModelRoot : null;
            if (model != null) modelBaseScale = model.localScale;
            if (heroCamera == null && cam != null)
            {
                heroCamera = cam.GetComponent<Camera>();
                if (heroCamera != null) baseFov = heroCamera.fieldOfView;
            }

            phase = Phase.WindUp;
            timer = 0f;
            nextRingTime = 0f;
            damagedInWindUp = false;
            if (controller != null) controller.MovementLocked = true;
            if (modelAnim != null) modelAnim.CastLocked = true;
            BuildPullEmbers();
            Debug.Log($"[Skill] Teleport winding up ({skill.castTime}s)");
            return true;
        }

        private void Update()
        {
            timer += Time.deltaTime;
            switch (phase)
            {
                case Phase.WindUp: UpdateWindUp(); break;
                case Phase.Stretch: UpdateStretch(); break;
                case Phase.Rise: UpdateRise(); break;
                case Phase.Hover: UpdateHover(); break;
                case Phase.Travel: UpdateTravel(); break;
                case Phase.Landing: UpdateLanding(); break;
            }
        }

        // つぶれて、光を吸い込む。
        private void UpdateWindUp()
        {
            string reason = null;
            if (health != null && health.IsDead) reason = "dead";
            else if (damagedInWindUp) reason = "took damage";
            else if (!StatusEffectController.CanUseSkills(gameObject)) reason = "stunned / silenced";
            else if (modelAnim != null && modelAnim.IsRespawnAnimPlaying) reason = "respawning";
            if (reason != null)
            {
                Debug.Log($"[Skill] Teleport cancelled ({reason})");
                if (health == null || !health.IsDead) FireballVfx.SpawnSparks(transform.position + Vector3.up, Vector3.up, 14, Neon, NeonDeep);
                EndAll();
                return;
            }

            // 溜めの間はゆっくり沈み、放つ直前に深くつぶれる。
            float cast = skill.castTime;
            float k = cast > 0f ? Mathf.Clamp01(timer / cast) : 1f;
            float final = Mathf.Clamp01((timer - (cast - FinalSquashSeconds)) / FinalSquashSeconds);
            float eased = Mathf.Lerp(ChargeSquash * k, 1f, final * final);
            if (pullEmbers != null)
            {
                var emission = pullEmbers.emission;
                emission.rateOverTime = Mathf.Lerp(EmberRateStart, EmberRateEnd, k);
            }
            if (k < 1f && timer >= nextRingTime)
            {
                windUpRing = ShockRing.Spawn(transform.position, 3f, 0.3f, WindUpRingSeconds, 0.5f, Neon);
                nextRingTime += WindUpRingSeconds;
            }
            SetModelScale(1f + WindUpSquash * 0.7f * eased, 1f - WindUpSquash * eased);
            SetCamera(Vector3.up * (-CameraCrouch * eased), 0f, FovWindUp * eased);
            if (k < 1f) return;

            if (tryPay != null && !tryPay())
            {
                Debug.Log("[Skill] Teleport cancelled (cannot pay)");
                EndAll();
                return;
            }
            StopPullEmbers();
            phase = Phase.Stretch;
            timer = 0f;
        }

        // 細い光の線に伸びて消える。
        private void UpdateStretch()
        {
            float k = Mathf.Clamp01(timer / StretchSeconds);
            SetModelScale(Mathf.Lerp(1f + WindUpSquash * 0.7f, StretchThin, k), Mathf.Lerp(1f - WindUpSquash, StretchTall, k));
            SetCamera(Vector3.up * Mathf.Lerp(-CameraCrouch, CameraRise, k), 0f, Mathf.Lerp(FovWindUp, FovFlight, k));
            if (k >= 1f) Depart();
        }

        private void Depart()
        {
            Vector3 chest = transform.position + Vector3.up;
            FireballVfx.SpawnSparks(chest, Vector3.up, 26, Neon, NeonDeep);
            FireFlash.Spawn(chest, 3.5f, 0.2f, 0.6f, new Color(1.6f, 3f, 3f), new Color(0.1f, 1.4f, 1.8f), LightTint, FlashLightIntensity);
            if (cameraTransform != null) FireFlash.Spawn(cameraTransform.position + cameraTransform.forward * 1.2f, 5f, 0.16f, 0f, new Color(0.9f, 2.2f, 2.2f), new Color(0f, 0.9f, 1.2f), LightTint, 0f);

            if (model != null) model.gameObject.SetActive(false);
            if (cc != null) cc.enabled = false;
            if (controller != null) controller.Suspended = true;

            // 判定に自分が当たらないよう、当たり判定を切ってから着地点を求める。
            groundPosition = transform.position;
            p3 = FindLandingPosition();
            Vector3 flat = p3 - groundPosition;
            flat.y = 0f;
            float height = BalanceConfig.Instance.teleportArcHeight + flat.magnitude * ArcHeightPerDistance;
            hoverPosition = groundPosition + Vector3.up * height;
            // 上空から水平に滑り出し、まちの真上から降りる。
            p0 = hoverPosition;
            p1 = hoverPosition + flat * TravelGlide;
            p2 = p3 + Vector3.up * (height * TravelDrop);
            startYaw = transform.rotation;
            travelYaw = flat.sqrMagnitude > 0.01f ? Quaternion.LookRotation(flat.normalized) : startYaw;

            BuildLight();
            phase = Phase.Rise;
            timer = 0f;
            Debug.Log($"[Skill] Teleport flying to {p3}");
        }

        private Vector3 FindLandingPosition()
        {
            if (RespawnManager.Instance != null) return RespawnManager.Instance.GetSpawnPosition();
            var town = GameObject.FindWithTag("StartTown");
            return town != null ? town.transform.position : transform.position;
        }

        // 飛行中に継続ダメージなどで倒れたら、そこで打ち切る（復活は RespawnManager に任せる）。
        private bool EndIfDead()
        {
            if (health == null || !health.IsDead) return false;
            EndAll();
            return true;
        }

        // 真上へ昇る。視線は水平に寄せ、地面が遠ざかるのを見せる。
        private void UpdateRise()
        {
            if (EndIfDead()) return;
            float t = Mathf.Clamp01(timer / Mathf.Max(0.05f, BalanceConfig.Instance.teleportRiseSeconds));
            Vector3 position = Vector3.Lerp(groundPosition, hoverPosition, Smooth(t));
            transform.position = position;
            SetCamera(Vector3.up * CameraRise, -LookPitch() * Mathf.Clamp01(t / RiseLookBlend), Mathf.Lerp(FovFlight, FovHover, t));
            UpdateLight(position + Vector3.up, Vector3.up, SpeedLineRateRise * (1f - t));
            if (t >= 1f)
            {
                phase = Phase.Hover;
                timer = 0f;
            }
        }

        // 上空で少し漂いながら、まちを見下ろす向きへ変わる。
        private void UpdateHover()
        {
            if (EndIfDead()) return;
            float t = Mathf.Clamp01(timer / Mathf.Max(0.05f, BalanceConfig.Instance.teleportHoverSeconds));
            Vector3 position = hoverPosition + Vector3.up * (Mathf.Sin(t * Mathf.PI) * HoverBob);
            transform.position = position;
            transform.rotation = Quaternion.Slerp(startYaw, travelYaw, Smooth(t));
            SetCamera(Vector3.up * CameraRise, PitchToTown(position) * Smooth(t) - LookPitch(), FovHover);
            UpdateLight(position + Vector3.up, travelYaw * Vector3.forward, 0f);
            if (t >= 1f)
            {
                phase = Phase.Travel;
                timer = 0f;
            }
        }

        // まちへ滑空して降りる。視線は着地点へ向け、着地前に元の高さへ戻す。
        private void UpdateTravel()
        {
            if (EndIfDead()) return;
            float t = Mathf.Clamp01(timer / Mathf.Max(0.05f, BalanceConfig.Instance.teleportTravelSeconds));
            float u = Smooth(t);
            Vector3 position = Bezier(u);
            Vector3 forward = BezierTangent(Mathf.Clamp(u, 0.02f, 0.98f)).normalized;
            transform.position = position;

            float blendIn = Mathf.Clamp01(t / TravelBlendRatio);
            float blendOut = Mathf.Clamp01((1f - t) / TravelBlendRatio);
            float fov = Mathf.Lerp(FovHover, FovFlight, Mathf.Sin(t * Mathf.PI)) * blendOut;
            SetCamera(Vector3.up * (CameraRise * blendOut), (PitchToTown(position) - LookPitch()) * blendOut, fov);

            Vector3 head = position + Vector3.up - forward * (TrailBehindCamera * Mathf.Min(blendIn, blendOut));
            UpdateLight(head, forward, SpeedLineRateTravel * Mathf.Sin(t * Mathf.PI));
            if (t >= 1f) Land();
        }

        private float PitchToTown(Vector3 from)
        {
            Vector3 toTown = p3 - from;
            float flat = new Vector2(toTown.x, toTown.z).magnitude;
            return Mathf.Clamp(Mathf.Atan2(-toTown.y, flat) * Mathf.Rad2Deg, 0f, MaxFlightPitch);
        }

        private float LookPitch()
        {
            return controller != null ? controller.LookPitch : 0f;
        }

        private static float Smooth(float t)
        {
            return t * t * (3f - 2f * t);
        }

        private void UpdateLight(Vector3 head, Vector3 forward, float speedLineRate)
        {
            if (lightTrail != null) lightTrail.transform.position = head;
            if (speedLines == null) return;
            Vector3 eye = cameraTransform != null ? cameraTransform.position : head;
            speedLines.transform.SetPositionAndRotation(eye + forward * SpeedLineLead, Quaternion.LookRotation(-forward));
            var emission = speedLines.emission;
            emission.rateOverTime = speedLineRate;
        }

        private void Land()
        {
            transform.position = p3;
            if (cc != null) cc.enabled = true;
            if (controller != null) controller.Suspended = false;
            if (model != null)
            {
                model.gameObject.SetActive(true);
                model.localScale = modelBaseScale;
            }
            ReleaseLight();

            Vector3 chest = p3 + Vector3.up;
            FireballVfx.SpawnSparks(p3 + Vector3.up * 0.2f, Vector3.up, 40, Neon, NeonDeep);
            FireFlash.Spawn(chest, 4.5f, 0.24f, 0.6f, new Color(1.6f, 3f, 3f), new Color(0.1f, 1.4f, 1.8f), LightTint, FlashLightIntensity);
            ShockRing.Spawn(p3, 0.4f, LandingRingRadius, 0.5f, 1.3f, Neon);
            ImpactPunch.Play(gameObject, transform.forward, 0f);

            phase = Phase.Landing;
            timer = 0f;
            Debug.Log("[Skill] Teleport landed");
        }

        // 着地の衝撃でカメラが沈んで戻る。
        private void UpdateLanding()
        {
            float seconds = Mathf.Max(0.05f, BalanceConfig.Instance.teleportLandingSeconds);
            float k = Mathf.Clamp01(timer / seconds);
            float settle = (1f - k) * (1f - k);
            SetCamera(Vector3.up * (-LandingCameraDrop * settle), 0f, FovLanding * settle);
            if (k >= 1f) EndAll();
        }

        private Vector3 Bezier(float u)
        {
            float v = 1f - u;
            return v * v * v * p0 + 3f * v * v * u * p1 + 3f * v * u * u * p2 + u * u * u * p3;
        }

        private Vector3 BezierTangent(float u)
        {
            float v = 1f - u;
            return 3f * v * v * (p1 - p0) + 6f * v * u * (p2 - p1) + 3f * u * u * (p3 - p2);
        }

        private void BuildPullEmbers()
        {
            pullEmbers = FireballVfx.NewParticles("TeleportPull", transform);
            pullEmbers.transform.localPosition = Vector3.up;
            var main = pullEmbers.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = 0.4f;
            main.startSpeed = -4f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
            main.startColor = new ParticleSystem.MinMaxGradient(Neon, NeonDeep);
            main.maxParticles = 60;
            var emission = pullEmbers.emission;
            emission.rateOverTime = EmberRateStart;
            var shape = pullEmbers.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 1.7f;
            shape.radiusThickness = 0f;
            var r = pullEmbers.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.05f;
            r.lengthScale = 2f;
            pullEmbers.gameObject.SetActive(true);
            pullEmbers.Play();
        }

        private void StopPullEmbers()
        {
            if (pullEmbers == null) return;
            pullEmbers.transform.SetParent(null, true);
            pullEmbers.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(pullEmbers.gameObject, 0.5f);
            pullEmbers = null;
        }

        private void StopWindUpRing()
        {
            if (windUpRing != null) Destroy(windUpRing.gameObject);
            windUpRing = null;
        }

        // 空に残る光の軌跡と、1人称で流れて見える光の筋。
        private void BuildLight()
        {
            var go = new GameObject("TeleportLight");
            go.transform.position = transform.position + Vector3.up;
            lightTrail = go.AddComponent<TrailRenderer>();
            lightTrail.sharedMaterial = FireballVfx.Trail;
            lightTrail.time = 3f;
            lightTrail.minVertexDistance = 0.3f;
            lightTrail.widthMultiplier = 1.6f;
            lightTrail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            lightTrail.colorGradient = FireballVfx.FadeGradient(Neon, NeonDeep);
            lightTrail.textureMode = LineTextureMode.Stretch;
            lightTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lightTrail.receiveShadows = false;

            // 軌跡の先頭の光の玉。上空でホバーしている間も遠くから見える。
            var headGlow = FireballVfx.NewMeshObject("Head", go.transform, FireballVfx.QuadMesh, FireballVfx.Glow);
            headGlow.transform.localScale = Vector3.one * LightHeadSize;
            var headBlock = new MaterialPropertyBlock();
            headBlock.SetColor("_Color", new Color(0.5f, 2.2f, 2.4f));
            headBlock.SetFloat("_Pull", 0f);
            headGlow.SetPropertyBlock(headBlock);
            lightHead = headGlow.gameObject;
            // 真下を見下ろしても自分のカメラを覆わないよう、モデルと同じレイヤーに置く。
            if (model != null) lightHead.layer = model.gameObject.layer;

            // カメラの前方から飛行方向の逆へ流し、すれ違う光の筋として見せる。
            speedLines = FireballVfx.NewParticles("TeleportLines", go.transform);
            var main = speedLines.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
            main.startSpeed = 20f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
            main.startColor = new ParticleSystem.MinMaxGradient(Neon, new Color(0.7f, 1f, 1f));
            main.maxParticles = 120;
            var emission = speedLines.emission;
            emission.rateOverTime = SpeedLineRateRise;
            var shape = speedLines.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 0f;
            shape.radius = 9f;
            shape.radiusThickness = 0.45f;
            var color = speedLines.colorOverLifetime;
            color.enabled = true;
            color.color = FireballVfx.FadeGradient(Color.white, Neon);
            var r = speedLines.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.15f;
            r.lengthScale = 2f;
            speedLines.gameObject.SetActive(true);
            speedLines.Play();
        }

        // 本体が着地しても、軌跡は空に少し残って消える。
        private void ReleaseLight()
        {
            if (lightHead != null) Destroy(lightHead);
            lightHead = null;
            if (lightTrail == null) return;
            lightTrail.emitting = false;
            lightTrail.autodestruct = true;
            if (speedLines != null) speedLines.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            lightTrail = null;
            speedLines = null;
        }

        private void EndAll()
        {
            StopPullEmbers();
            StopWindUpRing();
            ReleaseLight();
            phase = Phase.Idle;
            if (cc != null) cc.enabled = true;
            if (controller != null)
            {
                controller.Suspended = false;
                controller.MovementLocked = false;
                controller.SetCameraKick(Vector3.zero, 0f);
            }
            if (modelAnim != null) modelAnim.CastLocked = false;
            if (model != null)
            {
                // 死亡中のモデルの表示は CharacterModelAnimator に任せる。
                if (health == null || !health.IsDead) model.gameObject.SetActive(true);
                model.localScale = modelBaseScale;
            }
            if (heroCamera != null) heroCamera.fieldOfView = baseFov;
        }

        private void SetModelScale(float width, float height)
        {
            if (model != null) model.localScale = Vector3.Scale(modelBaseScale, new Vector3(width, height, width));
        }

        private void SetCamera(Vector3 position, float pitch, float fovOffset)
        {
            if (controller != null) controller.SetCameraKick(position, pitch);
            if (heroCamera != null) heroCamera.fieldOfView = baseFov + fovOffset;
        }
    }
}
