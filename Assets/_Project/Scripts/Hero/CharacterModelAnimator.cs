using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Game.Combat;
using Game.Core;

namespace Game.Hero
{
    /// <summary>
    /// In-match character avatar (heroes, bot heroes, Demon King). Spawns the model FBX
    /// (HeroClassData.modelPath etc.) as a child, optionally overrides its materials,
    /// hooks the Animator to the shared AnimatorController at
    /// Resources/Knight/KnightAnimator.controller (every model uses the Knight
    /// skeleton, so the Knight clips play on all of them), then drives state changes
    /// by firing triggers — Trig_Idle / Trig_Forward / Trig_BackLeft / Trig_BackRight
    /// / Trig_Dying / Trig_Respawning / Trig_Attacking.
    ///
    /// The local human hero drives states from input; bots and the Demon King drive
    /// them from their actual movement (call <see cref="PlayAttack"/> for attacks).
    /// Call <see cref="Configure"/> right after AddComponent.
    /// </summary>
    public class CharacterModelAnimator : MonoBehaviour
    {
        [Header("Visual")]
        [SerializeField] private string modelPath = "Knight/knight";
        [SerializeField] private string materialPath = "Knight/KnightMat";
        [SerializeField] private Vector3 modelOffset = Vector3.zero;
        // 0,0,0 so the Knight faces the same direction as the Hero's transform.forward.
        // (180° offset caused the run animation to play opposite to actual movement
        // direction — classic moonwalk.)
        [SerializeField] private Vector3 modelEulerOffset = Vector3.zero;
        [SerializeField] private float modelScale = 1f;
        [SerializeField] private bool hideExistingBody = true;
        // 自分のFPSカメラから自分のアバターを除外するためのLayer名。
        // Hero.prefab のCameraの cullingMask はこのLayerを外しているので、
        // 同名のLayerをここに設定すれば1人称視点でアバターが映り込まなくなる。
        // 空文字 or 未定義のLayerならレイヤー設定をスキップする。
        [SerializeField] private string avatarLayerName = "LocalAvatar";
        // true: 自分が操作する勇者（入力で状態を決め、RespawnManager のイベントで復帰する）。
        // false: BOT や魔王（実際の移動量で状態を決め、HP の復活で復帰する）。
        [SerializeField] private bool driveFromInput = true;
        [SerializeField] private float moveThreshold = 0.3f;
        // false for crowds (monsters) so state changes do not flood the console.
        [SerializeField] private bool logStates = true;

        [Header("Attack")]
        [SerializeField] private float attackClipDuration = 0.6f;

        [Header("Respawn")]
        // Fall4 を 1 回だけ再生してから Idle に戻すまでの時間（秒）。
        // クリップ自体は loopTime=0 で最後のフレームを保持するので、
        // この時間が過ぎたタイミングで respawning フラグを下ろして Idle に戻す。
        [SerializeField] private float respawnClipDuration = 4f;

        [Header("Death FX")]
        // Dying クリップ再生開始からアニメーターを停止して死亡ポーズで固める時刻（秒）。
        // dying_backwards はループ前提のクリップで終端が立ち姿に戻るため、
        // 倒れたフレームでフリーズさせて「仁王立ち戻り」を抑止する。
        [SerializeField] private float dyingFreezeAt = 1.5f;
        // フリーズ後、白フラッシュに移るまで死亡ポーズを見せる時間（秒）。
        [SerializeField] private float dyingHoldAfterFreeze = 0.5f;
        // 真っ白マテリアルに切り替えてから粒子に弾けるまでの「白い静止」時間。
        [SerializeField] private float whiteHoldDuration = 0.15f;
        // 純白フラッシュ色（マテリアル・パーティクル共通）。
        [SerializeField] private Color whiteFlashColor = Color.white;
        // 純白マテリアルに乗せるエミッション強度（Unlit シェーダーが取れない時のフォールバック）。
        [SerializeField] private float whiteEmissionIntensity = 4f;
        // 粒子総数。
        [SerializeField] private int deathParticleBurstCount = 140;
        // 粒子の上昇速度（m/s）。
        [SerializeField] private float deathParticleUpwardSpeed = 6f;
        // 粒子の寿命（秒）。
        [SerializeField] private float deathParticleLifetime = 1.8f;
        // 粒子のサイズ。
        [SerializeField] private float deathParticleSize = 0.18f;
        // 粒子発生位置のオフセット（モデル基点からの上下）。
        [SerializeField] private float deathParticleVerticalOffset = 1f;
        // 任意で差し替えるパーティクルプレハブ。null の場合は内部で生成する。
        [SerializeField] private ParticleSystem deathParticlesPrefab;

        private enum AnimState { Idle, Forward, BackLeft, BackRight, Dying, Respawning, Attacking }

        /// <summary>
        /// True while the Fall4 respawn animation is playing (set in OnRespawnFinished,
        /// cleared when respawnAnimTimer hits 0). Other Hero scripts (FPS controller,
        /// attack, skill executor) check this to suspend player input during the
        /// animation so the Swordsman cannot be moved or shoot mid-Fall4.
        /// </summary>
        public bool IsRespawnAnimPlaying => respawning;
        public bool HasModel => model != null;
        public Transform ModelRoot => model != null ? model.transform : null;
        /// 溜め中。基本攻撃と移動アニメーションを止める。
        public bool CastLocked { get; set; }

        private GameObject model;
        private Animator animator;
        private AnimState currentState = AnimState.Idle;
        private float attackTimer;
        private bool respawning;
        private float respawnAnimTimer;
        private HeroInputActions inputs;
        private HealthComponent health;
        private Renderer[] modelRenderers;
        private Material[][] originalMaterials;
        private Vector3 lastPosition;
        private bool wasDead;
        private Material whiteFlashMaterial;
        private Coroutine deathSequenceRoutine;

        // 溜めの構えの強さ（Animator の結果に LateUpdate で上乗せする）
        private const float CastLeanDegrees = 20f;
        private float castLean;
        private float castReach;
        private Vector3 castHoldPoint;
        private bool castBonesCached;
        private Transform spineLower, spineUpper, leftArm, leftForeArm, leftHand, rightArm, rightForeArm, rightHand;

        private static readonly string[] TriggerNames =
        {
            "Trig_Idle", "Trig_Forward", "Trig_BackLeft", "Trig_BackRight",
            "Trig_Dying", "Trig_Respawning", "Trig_Attacking"
        };

        /// <summary>
        /// Must be called right after AddComponent (before Start).
        /// hideFromOwnCamera: move the model to avatarLayerName so the local FPS camera skips it.
        /// </summary>
        public void Configure(string modelPath, string materialPath, float scale, bool driveFromInput, bool hideFromOwnCamera, bool logStates = true)
        {
            this.logStates = logStates;
            this.modelPath = modelPath;
            this.materialPath = materialPath;
            modelScale = scale;
            this.driveFromInput = driveFromInput;
            if (!hideFromOwnCamera) avatarLayerName = "";
            if (!driveFromInput) inputs?.Hero.Disable();
        }

        /// Adds the Demon King model from BalanceConfig (driven by movement, visible to every camera).
        public static CharacterModelAnimator AttachDemonKingModel(GameObject go)
        {
            var cfg = BalanceConfig.Instance;
            if (string.IsNullOrEmpty(cfg.demonKingModelPath) || go.GetComponent<CharacterModelAnimator>() != null) return null;
            var anim = go.AddComponent<CharacterModelAnimator>();
            anim.Configure(cfg.demonKingModelPath, "", cfg.demonKingModelScale, false, false);
            return anim;
        }

        /// Plays the attack clip once (bots / non-input characters).
        public void PlayAttack()
        {
            if (health != null && health.IsDead) return;
            attackTimer = attackClipDuration;
        }

        /// lean: -1 で後ろへ反り、+1 で前へ踏み込む / reach: 両手を holdPoint へ向ける強さ（0〜1）。
        public void SetCastPose(float lean, float reach, Vector3 holdPoint)
        {
            castLean = lean;
            castReach = reach;
            castHoldPoint = holdPoint;
        }

        private void LateUpdate()
        {
            if (model == null || animator == null) return;
            if (castLean == 0f && castReach == 0f) return;
            if (health != null && health.IsDead) return;
            CacheCastBones();

            Quaternion lean = Quaternion.AngleAxis(castLean * CastLeanDegrees * 0.5f, transform.right);
            if (spineLower != null) spineLower.rotation = lean * spineLower.rotation;
            if (spineUpper != null) spineUpper.rotation = lean * spineUpper.rotation;

            AimArm(leftArm, leftForeArm, leftHand, -transform.right);
            AimArm(rightArm, rightForeArm, rightHand, transform.right);
        }

        private void AimArm(Transform arm, Transform foreArm, Transform hand, Vector3 side)
        {
            if (arm == null || foreArm == null || hand == null) return;
            // 肘は少し外・下へ開き、手先は火球を挟むように向ける。
            AimBone(arm, foreArm, castHoldPoint + side * 0.22f - transform.up * 0.12f);
            AimBone(foreArm, hand, castHoldPoint + side * 0.06f);
        }

        private void AimBone(Transform bone, Transform child, Vector3 target)
        {
            Quaternion delta = Quaternion.FromToRotation(child.position - bone.position, target - bone.position);
            bone.rotation = Quaternion.Slerp(Quaternion.identity, delta, castReach) * bone.rotation;
        }

        private void CacheCastBones()
        {
            if (castBonesCached) return;
            castBonesCached = true;
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                switch (t.name)
                {
                    case "Spine01": spineLower = t; break;
                    case "Spine": spineUpper = t; break;
                    case "LeftArm": leftArm = t; break;
                    case "LeftForeArm": leftForeArm = t; break;
                    case "LeftHand": leftHand = t; break;
                    case "RightArm": rightArm = t; break;
                    case "RightForeArm": rightForeArm = t; break;
                    case "RightHand": rightHand = t; break;
                }
            }
        }

        private void Awake()
        {
            health = GetComponent<HealthComponent>();
            inputs = new HeroInputActions();
        }

        private void OnEnable()
        {
            if (driveFromInput) inputs.Hero.Enable();
            if (health != null) health.OnDeath += OnDeath;
        }

        private void OnDisable()
        {
            inputs.Hero.Disable();
            if (health != null) health.OnDeath -= OnDeath;
            DetachRespawnEvents();
        }

        private void OnDestroy()
        {
            inputs?.Dispose();
        }

        private void Start()
        {
            SpawnModel();
            if (model == null) { enabled = false; return; }
            AttachAnimatorController();
            if (driveFromInput) AttachRespawnEvents();
            lastPosition = transform.position;
            wasDead = health != null && health.IsDead;
            // Force idle as the initial state.
            FireTrigger(AnimState.Idle);
            currentState = AnimState.Idle;
        }

        private void SpawnModel()
        {
            if (string.IsNullOrEmpty(modelPath)) return;
            var prefab = Resources.Load<GameObject>(modelPath);
            if (prefab == null)
            {
                Debug.LogWarning("[CharacterModelAnimator] Resources/" + modelPath + " not found");
                return;
            }
            model = Instantiate(prefab, transform);
            model.name = prefab.name + "_Visual";
            model.transform.localPosition = modelOffset;
            model.transform.localEulerAngles = modelEulerOffset;
            model.transform.localScale = Vector3.one * modelScale;

            // 1人称カメラから自分のアバターが映り込まないように専用Layerへ移す。
            ApplyAvatarLayer(model);

            // Optional material override; the resulting materials are cached so they can be
            // restored after the white death flash.
            if (!string.IsNullOrEmpty(materialPath))
            {
                var overrideMaterial = Resources.Load<Material>(materialPath);
                if (overrideMaterial == null) Debug.LogWarning("[CharacterModelAnimator] Resources/" + materialPath + " not found");
                ApplyMaterialToModel(overrideMaterial);
            }
            modelRenderers = model.GetComponentsInChildren<Renderer>(true);
            originalMaterials = new Material[modelRenderers.Length][];
            for (int i = 0; i < modelRenderers.Length; i++) originalMaterials[i] = modelRenderers[i].sharedMaterials;

            animator = model.GetComponentInChildren<Animator>(true);
            if (animator == null) animator = model.AddComponent<Animator>();
            var avatars = Resources.LoadAll<Avatar>(modelPath);
            if (avatars != null && avatars.Length > 0) animator.avatar = avatars[0];
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;

            if (hideExistingBody)
            {
                var body = transform.Find("Body");
                if (body != null) body.gameObject.SetActive(false);
            }
        }

        private void AttachAnimatorController()
        {
            if (animator == null) return;
            var controller = Resources.Load<RuntimeAnimatorController>("Knight/KnightAnimator");
            if (controller == null)
            {
                Debug.LogWarning("[CharacterModelAnimator] Resources/Knight/KnightAnimator.controller not found — open Unity once so the Editor script auto-generates it, or run Tools/Knight/Regenerate Animator Controller.");
                return;
            }
            animator.runtimeAnimatorController = controller;
            if (logStates) Debug.Log("[CharacterModelAnimator] assigned controller '" + controller.name + "'");
        }

        private void Update()
        {
            if (attackTimer > 0f) attackTimer -= Time.deltaTime;
            if (respawnAnimTimer > 0f)
            {
                respawnAnimTimer -= Time.deltaTime;
                if (respawnAnimTimer <= 0f) respawning = false;
            }
            if (driveFromInput && !CastLocked && inputs.Hero.Attack.WasPerformedThisFrame() && !(health != null && health.IsDead))
            {
                attackTimer = attackClipDuration;
            }
            // Without RespawnManager events (bots), treat dead -> alive as the respawn moment.
            bool dead = health != null && health.IsDead;
            if (!driveFromInput && wasDead && !dead) OnRespawnFinished();
            wasDead = dead;
            ApplyState(ComputeState());
            lastPosition = transform.position;
        }

        private Vector2 ReadMove()
        {
            if (driveFromInput) return inputs != null ? inputs.Hero.Move.ReadValue<Vector2>() : Vector2.zero;
            if (Time.deltaTime <= 0f) return Vector2.zero;
            Vector3 v = transform.InverseTransformDirection((transform.position - lastPosition) / Time.deltaTime);
            // Sideways movement plays the forward run (there is no strafe clip).
            if (v.z < -moveThreshold) return new Vector2(Mathf.Sign(v.x), -1f);
            if (v.z > moveThreshold || Mathf.Abs(v.x) > moveThreshold) return new Vector2(0f, 1f);
            return Vector2.zero;
        }

        private AnimState ComputeState()
        {
            // 死亡カウントダウン中(IsDead=true / respawning=false)は Dying を保持する。
            // 実際のリスポーン(テレポート)瞬間に OnRespawnFinished で respawning=true と
            // Fall4 用タイマーがセットされ、その間 Respawning ステートを優先する。
            if (health != null && health.IsDead) return AnimState.Dying;
            if (respawning) return AnimState.Respawning;
            if (CastLocked) return AnimState.Idle;
            if (attackTimer > 0f) return AnimState.Attacking;
            Vector2 move = ReadMove();
            if (move.y > 0.1f) return AnimState.Forward;
            if (move.y < -0.1f)
                return move.x < 0f ? AnimState.BackLeft : AnimState.BackRight;
            return AnimState.Idle;
        }

        private void ApplyState(AnimState s)
        {
            if (s == currentState) return;
            currentState = s;
            FireTrigger(s);
            if (logStates) Debug.Log("[CharacterModelAnimator] state -> " + s);
        }

        private void FireTrigger(AnimState s)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            // Reset all triggers to avoid stacking (e.g. if multiple frames flipped quickly).
            for (int i = 0; i < TriggerNames.Length; i++) animator.ResetTrigger(TriggerNames[i]);
            animator.SetTrigger(TriggerNames[(int)s]);
        }

        private void OnDeath()
        {
            currentState = AnimState.Dying;
            FireTrigger(AnimState.Dying);
            if (deathSequenceRoutine != null) StopCoroutine(deathSequenceRoutine);
            deathSequenceRoutine = StartCoroutine(DeathSequence());
        }

        /// <summary>
        /// Dying クリップを最後まで流す → アバターを純白マテリアルで一気にフラッシュ →
        /// 一瞬白を保持 → ParticleSystem で光弾として上方向に拡散・蒸散させ、
        /// アバター本体は非表示にする。リスポーン時(OnRespawnFinished)で再表示と
        /// マテリアル復元を行う。
        /// </summary>
        private IEnumerator DeathSequence()
        {
            // 念のため、前回フリーズしたままになっていた場合のリセット
            if (animator != null) animator.speed = 1f;

            // Phase 1: Dying クリップを「倒れた」フレームまで再生
            yield return new WaitForSeconds(dyingFreezeAt);

            // Phase 2: アニメーターを止めて死亡ポーズで固める。
            // (loopTime=0 でも dying_backwards の最終フレームが立ち姿に戻るため、
            //  Mecanim に任せず明示的に speed=0 でフリーズする。)
            if (animator != null) animator.speed = 0f;

            // Phase 3: 死亡ポーズを少し見せる
            yield return new WaitForSeconds(dyingHoldAfterFreeze);

            // Phase 4: 一気に純白へ
            ApplyMaterialToModel(GetOrCreateWhiteFlashMaterial());

            // Phase 5: 真っ白の状態を一瞬保持
            yield return new WaitForSeconds(whiteHoldDuration);

            // Phase 6: 光弾として拡散・蒸散し、アバターは消す
            Vector3 spawnPos = (model != null ? model.transform.position : transform.position)
                               + Vector3.up * deathParticleVerticalOffset;
            SpawnDeathParticles(spawnPos);
            if (model != null) model.SetActive(false);

            deathSequenceRoutine = null;
        }

        private void RestoreOriginalMaterials()
        {
            if (modelRenderers == null) return;
            for (int i = 0; i < modelRenderers.Length; i++)
            {
                if (modelRenderers[i] != null) modelRenderers[i].sharedMaterials = originalMaterials[i];
            }
        }

        private void ApplyMaterialToModel(Material m)
        {
            if (model == null || m == null) return;
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = m;
                r.sharedMaterials = mats;
            }
        }

        /// <summary>
        /// avatarLayerName で指定されたLayerをroot以下の全GameObjectに再帰的に適用する。
        /// Hero.prefab の Camera は同 Layer を cullingMask から外しているので、
        /// 自分のアバターが1人称視点で映り込まなくなる。
        /// </summary>
        private void ApplyAvatarLayer(GameObject root)
        {
            if (root == null || string.IsNullOrEmpty(avatarLayerName)) return;
            int layer = LayerMask.NameToLayer(avatarLayerName);
            if (layer < 0)
            {
                Debug.LogWarning("[CharacterModelAnimator] Layer '" + avatarLayerName + "' not defined in TagManager — skipping.");
                return;
            }
            SetLayerRecursively(root, layer);
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            var t = go.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                SetLayerRecursively(t.GetChild(i).gameObject, layer);
            }
        }

        private Material GetOrCreateWhiteFlashMaterial()
        {
            if (whiteFlashMaterial != null) return whiteFlashMaterial;
            // ライティングに左右されない真っ白を出したいので Unlit を優先する。
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Standard");
            whiteFlashMaterial = new Material(shader);
            if (whiteFlashMaterial.HasProperty("_BaseColor"))
                whiteFlashMaterial.SetColor("_BaseColor", whiteFlashColor);
            if (whiteFlashMaterial.HasProperty("_Color"))
                whiteFlashMaterial.SetColor("_Color", whiteFlashColor);
            if (whiteFlashMaterial.HasProperty("_EmissionColor"))
            {
                whiteFlashMaterial.EnableKeyword("_EMISSION");
                whiteFlashMaterial.SetColor("_EmissionColor", whiteFlashColor * whiteEmissionIntensity);
            }
            return whiteFlashMaterial;
        }

        private void SpawnDeathParticles(Vector3 position)
        {
            if (deathParticlesPrefab != null)
            {
                var inst = Instantiate(deathParticlesPrefab, position, Quaternion.identity);
                inst.Play();
                Destroy(inst.gameObject, deathParticleLifetime + 1f);
                return;
            }

            var go = new GameObject("CharacterDeathParticles");
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 0.3f;
            main.loop = false;
            main.startLifetime = deathParticleLifetime;
            main.startSpeed = deathParticleUpwardSpeed;
            main.startSize = deathParticleSize;
            main.startColor = whiteFlashColor;
            main.maxParticles = deathParticleBurstCount * 2;
            main.gravityModifier = -0.4f; // 負の重力で上方向に漂わせる
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, deathParticleBurstCount) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.45f;

            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.space = ParticleSystemSimulationSpace.World;
            vol.x = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
            vol.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
            vol.y = new ParticleSystem.MinMaxCurve(deathParticleUpwardSpeed * 0.5f, deathParticleUpwardSpeed);

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(whiteFlashColor, 0f), new GradientColorKey(whiteFlashColor, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) }
            );
            col.color = gradient;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.material = GetOrCreateParticleMaterial();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            ps.Play();
            Destroy(go, deathParticleLifetime + 1f);
        }

        private Material GetOrCreateParticleMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Particles/Additive");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", whiteFlashColor);
            if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", whiteFlashColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", whiteFlashColor);
            // URP Particles/Unlit を半透明・加算合成寄りに寄せる試み。
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);   // 1 = Transparent
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 1f);       // 1 = Additive
            return mat;
        }

        private void AttachRespawnEvents()
        {
            var rm = RespawnManager.Instance;
            if (rm == null) return;
            rm.OnRespawnStarted.AddListener(OnRespawnStarted);
            rm.OnRespawnFinished.AddListener(OnRespawnFinished);
        }

        private void DetachRespawnEvents()
        {
            var rm = RespawnManager.Instance;
            if (rm == null) return;
            rm.OnRespawnStarted.RemoveListener(OnRespawnStarted);
            rm.OnRespawnFinished.RemoveListener(OnRespawnFinished);
        }

        private void OnRespawnStarted(int seconds)
        {
            // 死亡カウントダウン中は Dying 姿勢を保持するだけ。Fall4 は実際にリスポーンする
            // 瞬間(OnRespawnFinished)で初めて再生する。
        }

        private void OnRespawnFinished()
        {
            // テレポート + ResetHealth 後にここが呼ばれる。IsDead=false になっているので
            // ComputeState の優先順位上 Respawning が選ばれ、Fall4 が頭から再生される。

            // 死亡演出を中断 & アバターとマテリアルを復帰
            if (deathSequenceRoutine != null)
            {
                StopCoroutine(deathSequenceRoutine);
                deathSequenceRoutine = null;
            }
            if (model != null && !model.activeSelf) model.SetActive(true);
            RestoreOriginalMaterials();
            // DeathSequence で speed=0 に凍結されている場合があるので戻す。
            if (animator != null) animator.speed = 1f;

            respawning = true;
            respawnAnimTimer = respawnClipDuration;
            currentState = AnimState.Respawning;
            FireTrigger(AnimState.Respawning);

            // SetActive(false→true) で Animator が直前(Dying)の姿勢で一瞬目覚めて
            // ちらつくのを避けるため、Respawning を強制的に頭から再生する。
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.Play("Respawning", 0, 0f);
                animator.Update(0f);
            }
        }
    }
}
