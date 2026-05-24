using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Game.UI
{
    /// <summary>
    /// Renders a 3D model (e.g. the Knight FBX) into a RenderTexture so the lobby UI
    /// can show it via a RawImage. Drop a Knight prefab/instance into the scene at
    /// any out-of-view location (e.g. far x=200), assign it to <see cref="modelRoot"/>,
    /// add this component on a separate GameObject, and reference Texture from your
    /// avatar UI.
    ///
    /// This avoids changing the lobby canvas mode — the avatar UI stays in
    /// ScreenSpaceOverlay and just shows the off-screen camera's output as a 2D image.
    /// </summary>
    public class KnightAvatarPreview : MonoBehaviour
    {
        [Header("Subject")]
        [Tooltip("The 3D model GameObject (Knight prefab / FBX instance). Place it off-screen in the scene.")]
        [SerializeField] private GameObject modelRoot;

        [Header("Camera framing")]
        [Tooltip("When true, the camera is positioned automatically so the model fits the texture. Disable to use the manual values below.")]
        [SerializeField] private bool autoFrame = true;
        [Tooltip("Extra space around the model when auto-framing (1.0 = exact fit; 1.3 = 30% padding).")]
        [SerializeField] private float framePadding = 1.35f;
        [Tooltip("Direction the camera looks FROM in model space (e.g. (0,0,1) looks toward -Z; the model's front).")]
        [SerializeField] private Vector3 viewDirection = new Vector3(0f, 0.2f, 1f);
        [SerializeField] private float fieldOfView = 30f;

        [Header("Manual placement (fallback when autoFrame=false or bounds missing)")]
        [SerializeField] private Vector3 cameraLocalPosition = new Vector3(0f, 1.6f, 2.5f);
        [SerializeField] private Vector3 cameraLookAtLocal = new Vector3(0f, 1.2f, 0f);

        [Header("RenderTexture")]
        [SerializeField] private int textureWidth = 512;
        [SerializeField] private int textureHeight = 768;
        [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0f);
        [SerializeField] private int antiAliasing = 4;

        [Header("Light (optional)")]
        [Tooltip("If true, a dedicated directional light is spawned that only lights the model layer (none -> uses scene lighting).")]
        [SerializeField] private bool spawnDedicatedLight = true;
        [SerializeField] private Vector3 lightEulerAngles = new Vector3(40f, -30f, 0f);
        [SerializeField] private float lightIntensity = 1.4f;

        private Camera previewCamera;
        private Light previewLight;
        public RenderTexture Texture { get; private set; }

        // Animation playback
        private PlayableGraph playableGraph;
        private AnimationClipPlayable clipPlayable;
        private float clipDuration;

        private void Awake()
        {
            Setup();
        }

        private void OnDestroy()
        {
            if (Texture != null) { Texture.Release(); Texture = null; }
            if (previewCamera != null) Destroy(previewCamera.gameObject);
            if (previewLight != null) Destroy(previewLight.gameObject);
            if (playableGraph.IsValid()) playableGraph.Destroy();
        }

        private float lastProgressLog;
        private void Update()
        {
            // Manual looping for the AnimationClipPlayable.
            if (playableGraph.IsValid() && clipPlayable.IsValid() && clipDuration > 0f)
            {
                double t = clipPlayable.GetTime();
                if (t >= clipDuration) clipPlayable.SetTime(t - clipDuration);
                if (Time.unscaledTime - lastProgressLog > 2f)
                {
                    lastProgressLog = Time.unscaledTime;
                    Debug.Log($"[KnightAvatarPreview] clipPlayable.GetTime={t:F2}/{clipDuration:F2}s graphPlaying={playableGraph.IsPlaying()}");
                }
            }
        }

        private void Setup()
        {
            // Single FBX now holds mesh + every animation merged together.
            if (modelRoot == null)
            {
                var prefab = Resources.Load<GameObject>("Knight/knight");
                if (prefab == null)
                {
                    Debug.LogWarning("[KnightAvatarPreview] Could not load Resources/Knight/knight — is the FBX in a Resources folder?");
                    return;
                }
                modelRoot = Instantiate(prefab, new Vector3(200f, 0f, 0f), Quaternion.identity);
                modelRoot.name = "Knight_Auto (" + prefab.name + ")";
                // Apply KnightMat to all renderers so the textures show up.
                var mat = Resources.Load<Material>("Knight/KnightMat");
                if (mat != null)
                {
                    foreach (var r in modelRoot.GetComponentsInChildren<Renderer>(true))
                    {
                        var mats = new Material[r.sharedMaterials.Length];
                        for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                        r.sharedMaterials = mats;
                    }
                    Debug.Log("[KnightAvatarPreview] applied KnightMat to auto-loaded Knight");
                }
                else
                {
                    Debug.LogWarning("[KnightAvatarPreview] KnightMat not found in Resources/Knight/");
                }
            }

            // 1. RenderTexture
            Texture = new RenderTexture(textureWidth, textureHeight, 24, RenderTextureFormat.ARGB32);
            Texture.antiAliasing = antiAliasing;
            Texture.useMipMap = false;
            Texture.Create();

            // 2. Camera
            var camGO = new GameObject("KnightPreviewCamera");
            camGO.transform.SetParent(modelRoot.transform, false);
            previewCamera = camGO.AddComponent<Camera>();
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = backgroundColor;
            previewCamera.targetTexture = Texture;
            previewCamera.fieldOfView = fieldOfView;
            previewCamera.nearClipPlane = 0.05f;
            previewCamera.farClipPlane = 1000f;
            previewCamera.depth = -10; // render before any other main camera

            if (autoFrame && TryComputeModelBounds(out var bounds))
            {
                // Distance required to fit bounds in the FOV (both axes).
                float halfFovRad = fieldOfView * 0.5f * Mathf.Deg2Rad;
                float distY = (bounds.size.y * 0.5f) / Mathf.Tan(halfFovRad);
                float aspect = (float)textureWidth / Mathf.Max(1, textureHeight);
                float halfHFovRad = Mathf.Atan(Mathf.Tan(halfFovRad) * aspect);
                float distX = (bounds.size.x * 0.5f) / Mathf.Tan(halfHFovRad);
                float distance = Mathf.Max(distX, distY) * framePadding;
                Vector3 dir = viewDirection.sqrMagnitude > 1e-4f ? viewDirection.normalized : Vector3.back;
                camGO.transform.position = bounds.center + dir * distance;
                camGO.transform.LookAt(bounds.center);
                Debug.Log($"[KnightAvatarPreview] auto-frame: center={bounds.center} size={bounds.size} dist={distance:F2}");
            }
            else
            {
                camGO.transform.localPosition = cameraLocalPosition;
                Vector3 worldLookAt = modelRoot.transform.TransformPoint(cameraLookAtLocal);
                camGO.transform.LookAt(worldLookAt);
            }

            // 3. Optional dedicated light so the subject is well-lit regardless of scene
            if (spawnDedicatedLight)
            {
                var lightGO = new GameObject("KnightPreviewLight");
                lightGO.transform.SetParent(modelRoot.transform, false);
                lightGO.transform.localEulerAngles = lightEulerAngles;
                previewLight = lightGO.AddComponent<Light>();
                previewLight.type = LightType.Directional;
                previewLight.intensity = lightIntensity;
                previewLight.color = Color.white;
                previewLight.shadows = LightShadows.None;
            }

            // 4. Animation
            SetupAnimation();

            Debug.Log($"[KnightAvatarPreview] ready: {textureWidth}x{textureHeight}, FOV {fieldOfView}");
        }

        private void SetupAnimation()
        {
            if (modelRoot == null) return;
            var animator = modelRoot.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                // Dump hierarchy + components so we can see what the Instantiate actually produced.
                Debug.LogWarning($"[KnightAvatarPreview] no Animator found on '{modelRoot.name}'. Dumping hierarchy:");
                foreach (var t in modelRoot.GetComponentsInChildren<Transform>(true))
                {
                    string comps = "";
                    foreach (var c in t.GetComponents<Component>())
                    {
                        if (comps.Length > 0) comps += ", ";
                        comps += (c == null) ? "<missing>" : c.GetType().Name;
                    }
                    Debug.Log($"  '{t.name}': [{comps}]");
                }
                // Add one. PlayableGraph still drives an empty Animator if no controller is set.
                animator = modelRoot.AddComponent<Animator>();
                var avatars = Resources.LoadAll<Avatar>("Knight/knight");
                if (avatars == null || avatars.Length == 0) avatars = Resources.LoadAll<Avatar>("Knight");
                if (avatars != null && avatars.Length > 0) { animator.avatar = avatars[0]; Debug.Log($"[KnightAvatarPreview] added Animator with avatar '{avatars[0].name}'"); }
                else Debug.Log("[KnightAvatarPreview] added Animator (no Avatar found in Resources/Knight)");
            }

            AnimationClip clip = FindAnimationClip();
            if (clip == null)
            {
                Debug.LogWarning("[KnightAvatarPreview] no AnimationClip found anywhere under Resources/Knight/");
                return;
            }

            // Make the Animator behave well for an off-screen preview:
            //  - applyRootMotion=false so root drift doesn't push the model
            //    out of the auto-framed camera.
            //  - updateMode=UnscaledTime so a paused game (Time.timeScale=0)
            //    still animates the lobby preview.
            //  - enabled=true defensively.
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            animator.enabled = true;
            Debug.Log($"[KnightAvatarPreview] animator pre-play: avatar={(animator.avatar != null ? animator.avatar.name : "NULL")} isHuman={animator.isHuman} hasController={(animator.runtimeAnimatorController != null)}");

            clipDuration = clip.length;
            playableGraph = PlayableGraph.Create("KnightAvatarPreview");
            playableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            clipPlayable = AnimationClipPlayable.Create(playableGraph, clip);
            clipPlayable.SetApplyFootIK(false);
            clipPlayable.SetSpeed(1.0);
            var output = AnimationPlayableOutput.Create(playableGraph, "AnimationOutput", animator);
            output.SetSourcePlayable(clipPlayable);
            playableGraph.Play();
            Debug.Log($"[KnightAvatarPreview] playing clip '{clip.name}' length={clipDuration:F2}s isLooping={clip.isLooping}");
        }

        private static AnimationClip FindAnimationClip()
        {
            // Everything is now in Resources/Knight/knight.fbx (single merged file).
            var clipsFile = Resources.LoadAll<AnimationClip>("Knight/knight");
            Debug.Log($"[KnightAvatarPreview] LoadAll<AnimationClip>('Knight/knight') -> {(clipsFile != null ? clipsFile.Length : 0)} clips");
            if (clipsFile != null)
            {
                foreach (var c in clipsFile) if (c != null) Debug.Log($"  clip: '{c.name}' length={c.length:F2}");
                var picked = PickPreferredClip(clipsFile);
                if (picked != null) return picked;
            }

            // Fallback: search the whole Knight/ folder if the file path lookup returned nothing.
            var clipsFolder = Resources.LoadAll<AnimationClip>("Knight");
            Debug.Log($"[KnightAvatarPreview] LoadAll<AnimationClip>('Knight') -> {(clipsFolder != null ? clipsFolder.Length : 0)} clips");
            if (clipsFolder != null)
            {
                foreach (var c in clipsFolder) if (c != null) Debug.Log($"  folder-clip: '{c.name}' length={c.length:F2}");
                var picked = PickPreferredClip(clipsFolder);
                if (picked != null) return picked;
            }

            // Last-ditch: untyped enumeration.
            var all = Resources.LoadAll("Knight");
            Debug.Log($"[KnightAvatarPreview] LoadAll('Knight') (untyped) -> {(all != null ? all.Length : 0)} objects");
            if (all != null)
            {
                foreach (var o in all)
                {
                    if (o is AnimationClip ac) { Debug.Log($"  untyped-clip: '{ac.name}'"); return ac; }
                }
            }
            return null;
        }

        // Minimum length to consider a clip "real". Anything below this is treated as
        // an empty/keyframe stub and skipped (the FBX export sometimes emits a 1-frame
        // T-pose clip alongside the real animation).
        private const float MinUsefulClipLength = 0.2f;

        // Prefers the user's explicitly-requested 'Alert_frame_rate_60' first,
        // then any clip containing 'alert', then idle/stand/ready as fallbacks.
        // Empty / 1-frame clips are filtered out and the longest non-preview clip is
        // used as the very last resort.
        private static AnimationClip PickPreferredClip(AnimationClip[] clips)
        {
            if (clips == null || clips.Length == 0) return null;

            // Dump every clip we found with its length so we can see what the FBX exposed.
            for (int i = 0; i < clips.Length; i++)
            {
                var c = clips[i];
                if (c == null) continue;
                Debug.Log($"  [candidate {i}] name='{c.name}' length={c.length:F3}s frameRate={c.frameRate} isLooping={c.isLooping}");
            }

            // 1) Exact-substring match for the requested clip name (length must be useful).
            foreach (var c in clips)
            {
                if (c == null || c.length < MinUsefulClipLength) continue;
                if (c.name.IndexOf("Alert_frame_rate_60", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Debug.Log($"[KnightAvatarPreview] selected '{c.name}' (exact 'Alert_frame_rate_60' match, len={c.length:F2}s)");
                    return c;
                }
            }
            // 2) Fuzzier "alert / idle / stand / ready" matching.
            string[] prefer = new[] { "alert", "idle", "stand", "ready" };
            foreach (var p in prefer)
            {
                foreach (var c in clips)
                {
                    if (c == null || c.length < MinUsefulClipLength) continue;
                    if (c.name.IndexOf(p, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Debug.Log($"[KnightAvatarPreview] selected '{c.name}' (matched preference '{p}', len={c.length:F2}s)");
                        return c;
                    }
                }
            }

            // 3) Longest non-preview clip whose length is useful.
            AnimationClip longest = null;
            foreach (var c in clips)
            {
                if (c == null || c.length < MinUsefulClipLength) continue;
                if (c.name.StartsWith("__preview__")) continue;
                if (longest == null || c.length > longest.length) longest = c;
            }
            if (longest != null)
            {
                Debug.Log($"[KnightAvatarPreview] selected '{longest.name}' (longest useful clip, len={longest.length:F2}s)");
                return longest;
            }

            // 4) Last-ditch: any non-preview clip regardless of length.
            return PickNonPreviewClip(clips);
        }

        private static AnimationClip PickNonPreviewClip(AnimationClip[] clips)
        {
            foreach (var c in clips) { if (c != null && !c.name.StartsWith("__preview__")) return c; }
            if (clips != null && clips.Length > 0) return clips[0];
            return null;
        }

        private bool TryComputeModelBounds(out Bounds bounds)
        {
            bounds = default;
            if (modelRoot == null) return false;
            var renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
            bool has = false;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                // SkinnedMeshRenderer.bounds requires the renderer to be enabled to give a valid
                // value pre-first-frame in some Unity versions; toggle if needed.
                if (!has) { bounds = r.bounds; has = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return has && bounds.size.sqrMagnitude > 1e-6f;
        }
    }
}
