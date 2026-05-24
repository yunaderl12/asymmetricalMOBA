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

        [Header("Camera placement (in model local space)")]
        [SerializeField] private Vector3 cameraLocalPosition = new Vector3(0f, 1.6f, 2.5f);
        [SerializeField] private Vector3 cameraLookAtLocal = new Vector3(0f, 1.2f, 0f);
        [SerializeField] private float fieldOfView = 30f;

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
            if (modelRoot == null)
            {
                var prefab = Resources.Load<GameObject>("Knight/knight");
                if (prefab == null)
                {
                    Debug.LogWarning("[KnightAvatarPreview] Could not load Resources/Knight/knight — is the FBX in a Resources folder?");
                    return;
                }
                modelRoot = Instantiate(prefab, new Vector3(200f, 0f, 0f), Quaternion.identity);
                modelRoot.name = "Knight_Auto";
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

            // 2. Camera child of modelRoot so it stays attached to the subject
            var camGO = new GameObject("KnightPreviewCamera");
            camGO.transform.SetParent(modelRoot.transform, false);
            camGO.transform.localPosition = cameraLocalPosition;
            previewCamera = camGO.AddComponent<Camera>();
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = backgroundColor;
            previewCamera.targetTexture = Texture;
            previewCamera.fieldOfView = fieldOfView;
            previewCamera.nearClipPlane = 0.05f;
            previewCamera.farClipPlane = 30f;
            previewCamera.depth = -10; // render before any other main camera

            // Aim at lookAt point (relative to modelRoot)
            Vector3 worldLookAt = modelRoot.transform.TransformPoint(cameraLookAtLocal);
            camGO.transform.LookAt(worldLookAt);

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

            Debug.Log($"[KnightAvatarPreview] ready: {textureWidth}x{textureHeight}, FOV {fieldOfView}, lookAt {worldLookAt}");
        }
    }
}
