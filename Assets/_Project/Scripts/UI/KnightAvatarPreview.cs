using UnityEngine;

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

        private void Awake()
        {
            Setup();
        }

        private void OnDestroy()
        {
            if (Texture != null) { Texture.Release(); Texture = null; }
            if (previewCamera != null) Destroy(previewCamera.gameObject);
            if (previewLight != null) Destroy(previewLight.gameObject);
        }

        private void Setup()
        {
            // Auto-load from Resources if no model was assigned in the Inspector.
            // Prefer the "alert" pose (more dynamic ready stance); fall back to the
            // plain idle if it isn't there yet.
            if (modelRoot == null)
            {
                var prefab = Resources.Load<GameObject>("Knight/knight_alert");
                if (prefab == null) prefab = Resources.Load<GameObject>("Knight/knight");
                if (prefab == null)
                {
                    Debug.LogWarning("[KnightAvatarPreview] Could not load Resources/Knight/knight_alert or knight — is the FBX in a Resources folder?");
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

            Debug.Log($"[KnightAvatarPreview] ready: {textureWidth}x{textureHeight}, FOV {fieldOfView}");
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
