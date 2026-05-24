using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using Game.Combat;
using Game.Core;

namespace Game.Hero
{
    /// <summary>
    /// In-match Knight avatar for the Swordsman class. Spawns the Knight FBX as a
    /// child of the Hero GameObject, loads animation clips from Resources/Knight by
    /// name substring, and runs a simple state machine that selects which clip plays
    /// based on movement input / death / respawn / attack triggers.
    ///
    /// Auto-spawned by HeroClassInitializer when className == "Swordsman" — no
    /// scene setup needed.
    /// </summary>
    public class HeroKnightAnimator : MonoBehaviour
    {
        [Header("Visual")]
        [SerializeField] private Vector3 modelOffset = Vector3.zero;
        [SerializeField] private Vector3 modelEulerOffset = new Vector3(0f, 180f, 0f);
        [SerializeField] private float modelScale = 1f;
        [Tooltip("If true, looks for a child named 'Body' on the hero and hides it once the Knight is spawned.")]
        [SerializeField] private bool hideExistingBody = true;

        [Header("Clip name hints (substring, case-insensitive)")]
        [SerializeField] private string idleHint = "Alert";
        [SerializeField] private string forwardHint = "Run02";
        [SerializeField] private string backLeftHint = "BackLeft";
        [SerializeField] private string backRightHint = "BackRight";
        [SerializeField] private string deathHint = "dying_backwards";
        [SerializeField] private string respawnHint = "fall4";
        [SerializeField] private string attackHint = "right_hand_sword";

        private enum AnimState { Idle, Forward, BackLeft, BackRight, Dying, Respawning, Attacking }

        private GameObject knightModel;
        private Animator animator;
        private PlayableGraph graph;
        private AnimationPlayableOutput output;
        private AnimationClipPlayable currentPlayable;
        private AnimationClip currentClip;
        private AnimState currentState = AnimState.Idle;
        private float attackTimer;
        private bool respawning;
        private HeroInputActions inputs;
        private CharacterController controller;
        private HealthComponent health;
        private HeroAttack heroAttack;
        private float lastSeenAttackCooldown;

        // Cached clip lookups
        private AnimationClip clipIdle, clipForward, clipBackLeft, clipBackRight, clipDying, clipRespawning, clipAttacking;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            health = GetComponent<HealthComponent>();
            heroAttack = GetComponent<HeroAttack>();
            inputs = new HeroInputActions();
        }

        private void OnEnable()
        {
            inputs.Hero.Enable();
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
            if (graph.IsValid()) graph.Destroy();
        }

        private void Start()
        {
            SpawnModel();
            if (knightModel == null) { enabled = false; return; }
            LoadAllClips();
            SetupGraph();
            AttachRespawnEvents();
            ApplyState(AnimState.Idle, force: true);
        }

        private void SpawnModel()
        {
            var prefab = Resources.Load<GameObject>("Knight/knight");
            if (prefab == null)
            {
                Debug.LogWarning("[HeroKnightAnimator] Resources/Knight/knight not found");
                return;
            }
            knightModel = Instantiate(prefab, transform);
            knightModel.name = "Knight_Visual";
            knightModel.transform.localPosition = modelOffset;
            knightModel.transform.localEulerAngles = modelEulerOffset;
            knightModel.transform.localScale = Vector3.one * modelScale;

            // Apply KnightMat so the textures show.
            var mat = Resources.Load<Material>("Knight/KnightMat");
            if (mat != null)
            {
                foreach (var r in knightModel.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                    r.sharedMaterials = mats;
                }
            }

            animator = knightModel.GetComponentInChildren<Animator>(true);
            if (animator == null) animator = knightModel.AddComponent<Animator>();
            // Hand the Animator the FBX's humanoid Avatar so bone retargeting works.
            var avatars = Resources.LoadAll<Avatar>("Knight/knight");
            if (avatars == null || avatars.Length == 0) avatars = Resources.LoadAll<Avatar>("Knight");
            if (avatars != null && avatars.Length > 0) animator.avatar = avatars[0];
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;

            if (hideExistingBody)
            {
                var body = transform.Find("Body");
                if (body != null) body.gameObject.SetActive(false);
            }
        }

        // Loads every AnimationClip under Resources/Knight (any FBX, any folder depth) once.
        private void LoadAllClips()
        {
            var all = Resources.LoadAll<AnimationClip>("Knight");
            Debug.Log($"[HeroKnightAnimator] LoadAll<AnimationClip>('Knight') -> {(all != null ? all.Length : 0)} clips");
            if (all == null) return;
            foreach (var c in all) if (c != null) Debug.Log($"  clip: '{c.name}' length={c.length:F2}s");

            clipIdle       = PickByHint(all, idleHint);
            clipForward    = PickByHint(all, forwardHint);
            clipBackLeft   = PickByHint(all, backLeftHint);
            clipBackRight  = PickByHint(all, backRightHint);
            clipDying      = PickByHint(all, deathHint);
            clipRespawning = PickByHint(all, respawnHint);
            clipAttacking  = PickByHint(all, attackHint);
            Debug.Log($"[HeroKnightAnimator] mapped clips: idle={Name(clipIdle)} forward={Name(clipForward)} backL={Name(clipBackLeft)} backR={Name(clipBackRight)} dying={Name(clipDying)} respawn={Name(clipRespawning)} attack={Name(clipAttacking)}");
        }

        private static string Name(AnimationClip c) => c == null ? "<missing>" : c.name;

        private static AnimationClip PickByHint(AnimationClip[] clips, string hint)
        {
            if (clips == null || string.IsNullOrEmpty(hint)) return null;
            // Prefer longest match in case the hint matches multiple clips (e.g. Run02 vs Run021).
            AnimationClip best = null;
            foreach (var c in clips)
            {
                if (c == null) continue;
                if (c.name.IndexOf(hint, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (best == null || c.length > best.length) best = c;
            }
            return best;
        }

        private void SetupGraph()
        {
            if (animator == null) return;
            graph = PlayableGraph.Create("HeroKnightAnimator");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            output = AnimationPlayableOutput.Create(graph, "out", animator);
            graph.Play();
        }

        private void Update()
        {
            // Loop the active clip manually (AnimationClipPlayable doesn't loop on its own).
            if (currentPlayable.IsValid() && currentClip != null && currentClip.length > 0f)
            {
                double t = currentPlayable.GetTime();
                if (t >= currentClip.length) currentPlayable.SetTime(t - currentClip.length);
            }
            // Tick attack timer.
            if (attackTimer > 0f) attackTimer -= Time.deltaTime;

            // Detect a fresh attack by watching HeroAttack.cooldownTimer rising.
            if (heroAttack != null)
            {
                // We can't read private cooldownTimer; instead poll for a recently performed Attack action.
                if (inputs.Hero.Attack.WasPerformedThisFrame() && !(health != null && health.IsDead))
                {
                    attackTimer = 0.6f; // play attack clip for ~0.6s
                }
            }

            ApplyState(ComputeState(), force: false);
        }

        private AnimState ComputeState()
        {
            if (health != null && health.IsDead) return AnimState.Dying;
            if (respawning) return AnimState.Respawning;
            if (attackTimer > 0f) return AnimState.Attacking;
            Vector2 move = Vector2.zero;
            if (inputs != null) move = inputs.Hero.Move.ReadValue<Vector2>();
            if (move.y > 0.1f) return AnimState.Forward;
            if (move.y < -0.1f)
            {
                return move.x < 0f ? AnimState.BackLeft : AnimState.BackRight;
            }
            // Pure strafe / no input -> idle.
            return AnimState.Idle;
        }

        private void ApplyState(AnimState s, bool force)
        {
            if (!force && s == currentState) return;
            currentState = s;
            var clip = ClipForState(s) ?? clipIdle;
            if (clip == null) return;
            PlayClip(clip);
        }

        private AnimationClip ClipForState(AnimState s)
        {
            switch (s)
            {
                case AnimState.Forward:    return clipForward;
                case AnimState.BackLeft:   return clipBackLeft;
                case AnimState.BackRight:  return clipBackRight;
                case AnimState.Dying:      return clipDying;
                case AnimState.Respawning: return clipRespawning;
                case AnimState.Attacking:  return clipAttacking;
                default:                   return clipIdle;
            }
        }

        private void PlayClip(AnimationClip clip)
        {
            if (clip == null || !graph.IsValid()) return;
            if (currentPlayable.IsValid()) currentPlayable.Destroy();
            currentPlayable = AnimationClipPlayable.Create(graph, clip);
            currentPlayable.SetApplyFootIK(false);
            output.SetSourcePlayable(currentPlayable);
            currentClip = clip;
            Debug.Log($"[HeroKnightAnimator] playing '{clip.name}' (state={currentState})");
        }

        private void OnDeath()
        {
            ApplyState(AnimState.Dying, force: true);
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
            respawning = true;
            ApplyState(AnimState.Respawning, force: true);
        }

        private void OnRespawnFinished()
        {
            respawning = false;
        }
    }
}
