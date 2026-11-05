using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Core
{
    /// 魔王レベルに応じて、城を中心にした赤い空と渦雲の半径を伸ばす。
    /// 外側は青空のままで、ライトと環境光は見ている場所が嵐の中かどうかで変わる。
    public class SkyController : MonoBehaviour
    {
        private static readonly int CenterId = Shader.PropertyToID("_StormCenter");
        private static readonly int RadiusId = Shader.PropertyToID("_StormRadius");
        private static readonly int TimeId = Shader.PropertyToID("_StormTime");
        private const float FullRadiusInside = 0.75f; // 半径のこの割合より内側は完全に嵐の下
        private const float ColorChangePerSecond = 0.6f;
        private const float FindInterval = 1f;

        [SerializeField] private Light directionalLight;
        [SerializeField] private LightningFlash lightningFlash;

        private readonly Camera[] cameraBuffer = new Camera[16];
        private LevelComponent dkLevel;
        private Material skyMaterial;
        private Vector3 center;
        private float unitDistance;
        private float radius;
        private float targetRadius;
        private float growSpeed;
        private float stormAmount;
        private float nextFind;

        public float CurrentRadius => radius;

        private void Start()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;

            var castle = GameObject.Find("DemonCastle");
            var town = GameObject.Find("StartTown");
            center = castle != null ? castle.transform.position : Vector3.zero;
            center.y = 0f;
            unitDistance = 160f;
            if (castle != null && town != null)
            {
                Vector3 between = castle.transform.position - town.transform.position;
                between.y = 0f;
                unitDistance = between.magnitude;
            }

            var config = BalanceConfig.Instance;
            float largest = 0f;
            foreach (var r in config.stormStageRadius) largest = Mathf.Max(largest, r);

            var skyShader = Resources.Load<Shader>("VFX/StormSky");
            if (skyShader != null)
            {
                skyMaterial = new Material(skyShader);
                RenderSettings.skybox = skyMaterial;
            }
            else Debug.LogWarning("[SkyController] StormSky shader not found");

            StormClouds.Create(transform, largest * unitDistance * 1.02f);

            ConnectDemonKing();
            int stage = SkyPalette.StageForLevel(dkLevel != null ? dkLevel.CurrentLevel : 1);
            radius = targetRadius = RadiusForStage(stage);
            growSpeed = 0f;
            UpdateLightning(stage);
            Shader.SetGlobalVector(CenterId, center);
            ApplyCameras();
        }

        private void OnDestroy()
        {
            if (dkLevel != null) dkLevel.OnLevelChanged -= OnDemonKingLevelChanged;
            if (skyMaterial != null) Destroy(skyMaterial);
        }

        // シーンの魔王はBOT魔王に置き換わることがあるので、今いる魔王につなぎ直す。
        private void ConnectDemonKing()
        {
            var dk = GameObject.FindWithTag("DemonKing");
            var found = dk != null ? dk.GetComponent<LevelComponent>() : null;
            if (found == null || found == dkLevel) return;
            if (dkLevel != null) dkLevel.OnLevelChanged -= OnDemonKingLevelChanged;
            dkLevel = found;
            dkLevel.OnLevelChanged += OnDemonKingLevelChanged;
            OnDemonKingLevelChanged(dkLevel.CurrentLevel);
        }

        private void OnDemonKingLevelChanged(int newLevel)
        {
            int stage = SkyPalette.StageForLevel(newLevel);
            targetRadius = RadiusForStage(stage);
            growSpeed = Mathf.Abs(targetRadius - radius) / Mathf.Max(BalanceConfig.Instance.stormGrowSeconds, 0.01f);
            UpdateLightning(stage);
        }

        private float RadiusForStage(int stage)
        {
            var radii = BalanceConfig.Instance.stormStageRadius;
            return radii.Length == 0 ? 0f : radii[Mathf.Clamp(stage, 0, radii.Length - 1)] * unitDistance;
        }

        private void Update()
        {
            if (Time.unscaledTime >= nextFind)
            {
                nextFind = Time.unscaledTime + FindInterval;
                ConnectDemonKing();
            }

            radius = Mathf.MoveTowards(radius, targetRadius, growSpeed * Time.deltaTime);
            Shader.SetGlobalFloat(RadiusId, radius);
            Shader.SetGlobalFloat(TimeId, Time.unscaledTime);

            ApplyCameras();
            ApplyLighting();
        }

        private void ApplyCameras()
        {
            int count = Mathf.Min(Camera.allCamerasCount, cameraBuffer.Length);
            Camera.GetAllCameras(cameraBuffer);
            for (int i = 0; i < count; i++)
            {
                var c = cameraBuffer[i];
                if (c != null && c.targetTexture == null) c.clearFlags = CameraClearFlags.Skybox;
            }
        }

        private void ApplyLighting()
        {
            var cam = Camera.main;
            Vector3 viewer = cam != null ? cam.transform.position : center;
            viewer.y = 0f;
            float inside = 1f - Mathf.InverseLerp(radius * FullRadiusInside, Mathf.Max(radius, 0.01f), Vector3.Distance(viewer, center));
            stormAmount = Mathf.MoveTowards(stormAmount, inside, ColorChangePerSecond * Time.deltaTime);

            var p = SkyParams.Lerp(SkyPalette.Clear, SkyPalette.Storm, stormAmount);
            RenderSettings.ambientLight = p.ambient;
            // 雷が光っている間はライトを LightningFlash に任せる。
            if (directionalLight != null && (lightningFlash == null || !lightningFlash.IsFlashing))
            {
                directionalLight.color = p.lightColor;
                directionalLight.intensity = p.lightIntensity;
            }
        }

        private void UpdateLightning(int stage)
        {
            if (lightningFlash != null) lightningFlash.IsActive = stage >= BalanceConfig.Instance.stormStageRadius.Length - 1;
        }
    }
}
