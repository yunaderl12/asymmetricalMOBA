using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Game.Hero;

namespace Game.UI
{
    /// <summary>
    /// Renders a 3D character model into a RenderTexture so the lobby UI can show it
    /// via a RawImage. Call <see cref="ShowModel"/> with a Resources path (e.g. the
    /// selected class's HeroClassData.modelPath); the model is spawned out of view
    /// (x=200) and plays the Knight alert clip (all models share the Knight skeleton).
    ///
    /// This avoids changing the lobby canvas mode — the avatar UI stays in
    /// ScreenSpaceOverlay and just shows the off-screen camera's output as a 2D image.
    /// </summary>
    public class CharacterAvatarPreview : MonoBehaviour
    {
        [Header("Subject")]
        [Tooltip("Where the model is spawned (keep it out of view of the lobby camera).")]
        [SerializeField] private Vector3 stagePosition = new Vector3(200f, 0f, 0f);

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

        private GameObject modelRoot;
        private string currentModelPath;
        private string currentMaterialPath;
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
            if (modelRoot != null) Destroy(modelRoot);
        }

        /// <summary>
        /// Swaps the previewed model. materialPath (optional) overrides every renderer's material.
        /// Returns false when the model could not be loaded (the caller can fall back to the flat avatar).
        /// </summary>
        public bool ShowModel(string modelPath, string materialPath)
        {
            if (Texture == null || string.IsNullOrEmpty(modelPath)) return false;
            if (modelRoot != null && modelPath == currentModelPath && materialPath == currentMaterialPath) return true;

            var prefab = Resources.Load<GameObject>(modelPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[CharacterAvatarPreview] Could not load Resources/{modelPath}");
                return false;
            }
            if (playableGraph.IsValid()) playableGraph.Destroy();
            if (modelRoot != null) Destroy(modelRoot);

            modelRoot = Instantiate(prefab, stagePosition, Quaternion.identity);
            modelRoot.name = "Preview (" + prefab.name + ")";
            var boneFit = ModelBoneFit.Attach(modelRoot);
            var floatingItem = FloatingHeldItem.AttachIfPresent(modelRoot);
            currentModelPath = modelPath;
            currentMaterialPath = materialPath;
            if (!string.IsNullOrEmpty(materialPath))
            {
                var mat = Resources.Load<Material>(materialPath);
                if (mat != null)
                {
                    foreach (var r in modelRoot.GetComponentsInChildren<Renderer>(true))
                    {
                        var mats = new Material[r.sharedMaterials.Length];
                        for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                        r.sharedMaterials = mats;
                    }
                }
                else
                {
                    Debug.LogWarning($"[CharacterAvatarPreview] Resources/{materialPath} not found");
                }
            }

            // 手に持つ物が広がるTポーズではなく、実際に見せるポーズの大きさで画面に収める。
            var poseClip = FindAnimationClip();
            if (poseClip != null) poseClip.SampleAnimation(modelRoot, 0f);
            if (boneFit != null) boneFit.Apply();
            if (floatingItem != null) floatingItem.Place();

            FrameCamera();
            SetupAnimation();
            Debug.Log($"[CharacterAvatarPreview] showing '{modelPath}'");
            return true;
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
                    Debug.Log($"[CharacterAvatarPreview] clipPlayable.GetTime={t:F2}/{clipDuration:F2}s graphPlaying={playableGraph.IsPlaying()}");
                }
            }
        }

        private void Setup()
        {
            // 1. RenderTexture
            Texture = new RenderTexture(textureWidth, textureHeight, 24, RenderTextureFormat.ARGB32);
            Texture.antiAliasing = antiAliasing;
            Texture.useMipMap = false;
            Texture.Create();

            // 2. Camera (placed by FrameCamera once a model is shown)
            var camGO = new GameObject("CharacterPreviewCamera");
            previewCamera = camGO.AddComponent<Camera>();
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = backgroundColor;
            previewCamera.targetTexture = Texture;
            previewCamera.fieldOfView = fieldOfView;
            previewCamera.nearClipPlane = 0.05f;
            previewCamera.farClipPlane = 1000f;
            previewCamera.depth = -10; // render before any other main camera

            // 3. Optional dedicated light so the subject is well-lit regardless of scene
            if (spawnDedicatedLight)
            {
                var lightGO = new GameObject("CharacterPreviewLight");
                lightGO.transform.position = stagePosition;
                lightGO.transform.eulerAngles = lightEulerAngles;
                previewLight = lightGO.AddComponent<Light>();
                previewLight.type = LightType.Directional;
                previewLight.intensity = lightIntensity;
                previewLight.color = Color.white;
                previewLight.shadows = LightShadows.None;
            }

            Debug.Log($"[CharacterAvatarPreview] ready: {textureWidth}x{textureHeight}, FOV {fieldOfView}");
        }

        private void FrameCamera()
        {
            var camT = previewCamera.transform;
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
                camT.position = bounds.center + dir * distance;
                camT.LookAt(bounds.center);
            }
            else
            {
                camT.position = modelRoot.transform.TransformPoint(cameraLocalPosition);
                camT.LookAt(modelRoot.transform.TransformPoint(cameraLookAtLocal));
            }
        }

        private void SetupAnimation()
        {
            if (modelRoot == null) return;
            var animator = modelRoot.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                // Dump hierarchy + components so we can see what the Instantiate actually produced.
                Debug.LogWarning($"[CharacterAvatarPreview] no Animator found on '{modelRoot.name}'. Dumping hierarchy:");
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
                var avatars = Resources.LoadAll<Avatar>(currentModelPath);
                if (avatars != null && avatars.Length > 0) animator.avatar = avatars[0];
            }

            AnimationClip clip = FindAnimationClip();
            if (clip == null)
            {
                Debug.LogWarning("[CharacterAvatarPreview] no AnimationClip found anywhere under Resources/Knight/");
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
            Debug.Log($"[CharacterAvatarPreview] animator pre-play: avatar={(animator.avatar != null ? animator.avatar.name : "NULL")} isHuman={animator.isHuman} hasController={(animator.runtimeAnimatorController != null)}");

            clipDuration = clip.length;
            playableGraph = PlayableGraph.Create("CharacterAvatarPreview");
            playableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            clipPlayable = AnimationClipPlayable.Create(playableGraph, clip);
            clipPlayable.SetApplyFootIK(false);
            clipPlayable.SetSpeed(1.0);
            var output = AnimationPlayableOutput.Create(playableGraph, "AnimationOutput", animator);
            output.SetSourcePlayable(clipPlayable);
            playableGraph.Play();
            Debug.Log($"[CharacterAvatarPreview] playing clip '{clip.name}' length={clipDuration:F2}s isLooping={clip.isLooping}");
        }

        private static AnimationClip FindAnimationClip()
        {
            // 1) Dedicated Alert FBX (Meshy AI Animation_Alert_frame_rate_60 baked in).
            //    Whatever clip is in this FBX IS the alert — return the longest non-stub.
            var alertClips = Resources.LoadAll<AnimationClip>("Knight/knight_alert");
            Debug.Log($"[CharacterAvatarPreview] LoadAll<AnimationClip>('Knight/knight_alert') -> {(alertClips != null ? alertClips.Length : 0)} clips");
            if (alertClips != null && alertClips.Length > 0)
            {
                foreach (var c in alertClips) if (c != null) Debug.Log($"  alert-clip: '{c.name}' length={c.length:F2}");
                // Pick the longest clip in the alert FBX (skip the T-pose stub if any).
                AnimationClip best = null;
                foreach (var c in alertClips)
                {
                    if (c == null || c.name.StartsWith("__preview__")) continue;
                    if (best == null || c.length > best.length) best = c;
                }
                if (best != null)
                {
                    Debug.Log($"[CharacterAvatarPreview] selected '{best.name}' from knight_alert FBX (len={best.length:F2}s)");
                    return best;
                }
            }

            // 2) Character FBX clips.
            var clipsFile = Resources.LoadAll<AnimationClip>("Knight/knight");
            Debug.Log($"[CharacterAvatarPreview] LoadAll<AnimationClip>('Knight/knight') -> {(clipsFile != null ? clipsFile.Length : 0)} clips");
            if (clipsFile != null)
            {
                foreach (var c in clipsFile) if (c != null) Debug.Log($"  clip: '{c.name}' length={c.length:F2}");
                var picked = PickPreferredClip(clipsFile);
                if (picked != null) return picked;
            }

            // 3) Folder scan.
            var clipsFolder = Resources.LoadAll<AnimationClip>("Knight");
            Debug.Log($"[CharacterAvatarPreview] LoadAll<AnimationClip>('Knight') -> {(clipsFolder != null ? clipsFolder.Length : 0)} clips");
            if (clipsFolder != null)
            {
                foreach (var c in clipsFolder) if (c != null) Debug.Log($"  folder-clip: '{c.name}' length={c.length:F2}");
                var picked = PickPreferredClip(clipsFolder);
                if (picked != null) return picked;
            }

            // 4) Last-ditch: untyped enumeration.
            var all = Resources.LoadAll("Knight");
            Debug.Log($"[CharacterAvatarPreview] LoadAll('Knight') (untyped) -> {(all != null ? all.Length : 0)} objects");
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

        // Prefers any clip whose name contains 'alert' (the requested ready stance),
        // even if it's only a single-frame static pose. Length filtering is only
        // applied to the longest-clip fallback, NOT to name-based matches, because
        // an alert/idle pose is often authored as a 1-frame static.
        private static AnimationClip PickPreferredClip(AnimationClip[] clips)
        {
            if (clips == null || clips.Length == 0) return null;

            // Dump every clip we found with its length so we can see what the FBX exposed.
            var concat = new System.Text.StringBuilder("[CharacterAvatarPreview] all clips:");
            for (int i = 0; i < clips.Length; i++)
            {
                var c = clips[i];
                if (c == null) continue;
                Debug.Log($"  [candidate {i}] name='{c.name}' length={c.length:F3}s frameRate={c.frameRate} isLooping={c.isLooping}");
                concat.Append("\n  ").Append(i).Append(": '").Append(c.name).Append("' ").Append(c.length.ToString("F3")).Append("s");
            }
            Debug.Log(concat.ToString());

            // 1) Exact-substring match for the requested clip name (NO length filter — alert can be static).
            foreach (var c in clips)
            {
                if (c == null) continue;
                if (c.name.IndexOf("Alert_frame_rate_60", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Debug.Log($"[CharacterAvatarPreview] selected '{c.name}' (exact 'Alert_frame_rate_60' match, len={c.length:F2}s)");
                    return c;
                }
            }
            // 2) Fuzzier name matching (still NO length filter — these are likely poses).
            string[] prefer = new[] { "alert", "idle", "stand", "ready" };
            foreach (var p in prefer)
            {
                foreach (var c in clips)
                {
                    if (c == null) continue;
                    if (c.name.IndexOf(p, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Debug.Log($"[CharacterAvatarPreview] selected '{c.name}' (matched preference '{p}', len={c.length:F2}s)");
                        return c;
                    }
                }
            }

            // 3) Longest non-preview clip whose length is useful (filter applied here only).
            AnimationClip longest = null;
            foreach (var c in clips)
            {
                if (c == null || c.length < MinUsefulClipLength) continue;
                if (c.name.StartsWith("__preview__")) continue;
                if (longest == null || c.length > longest.length) longest = c;
            }
            if (longest != null)
            {
                Debug.Log($"[CharacterAvatarPreview] selected '{longest.name}' (longest useful clip, len={longest.length:F2}s)");
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
                Bounds b = r is SkinnedMeshRenderer skinned ? PosedBounds(skinned) : r.bounds;
                if (!has) { bounds = b; has = true; }
                else bounds.Encapsulate(b);
            }
            return has && bounds.size.sqrMagnitude > 1e-6f;
        }

        // SkinnedMeshRenderer.bounds はポーズを反映しないので、今の姿勢を焼いて測る。
        private static Bounds PosedBounds(SkinnedMeshRenderer skinned)
        {
            var baked = new Mesh();
            skinned.BakeMesh(baked, true);
            var result = new Bounds(skinned.transform.position, Vector3.zero);
            var vertices = baked.vertices;
            for (int i = 0; i < vertices.Length; i++) result.Encapsulate(skinned.transform.TransformPoint(vertices[i]));
            Destroy(baked);
            return result;
        }
    }
}
