using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Core
{
    public class SkyController : MonoBehaviour
    {
        [SerializeField] private Light directionalLight;
        [SerializeField] private float transitionDuration = 5f;
        [SerializeField] private LightningFlash lightningFlash;

        private LevelComponent dkLevel;
        private SkyParams current;
        private Coroutine activeRoutine;

        private void Start()
        {
            var dk = GameObject.FindWithTag("DemonKing");
            if (dk == null) { Debug.LogWarning("[SkyController] DemonKing not found"); return; }
            dkLevel = dk.GetComponent<LevelComponent>();
            if (dkLevel != null) dkLevel.OnLevelChanged += OnDemonKingLevelChanged;

            RenderSettings.ambientMode = AmbientMode.Flat;
            int lv = dkLevel != null ? dkLevel.CurrentLevel : 1;
            current = SkyPalette.For(SkyPalette.StageForLevel(lv));
            ApplyInstant(current);
            UpdateLightning(SkyPalette.StageForLevel(lv));
        }

        private void OnDestroy()
        {
            if (dkLevel != null) dkLevel.OnLevelChanged -= OnDemonKingLevelChanged;
        }

        private void OnDemonKingLevelChanged(int newLevel)
        {
            var stage = SkyPalette.StageForLevel(newLevel);
            var target = SkyPalette.For(stage);
            if (activeRoutine != null) StopCoroutine(activeRoutine);
            activeRoutine = StartCoroutine(LerpTo(target, transitionDuration, stage));
        }

        private void ApplyInstant(SkyParams p)
        {
            RenderSettings.ambientLight = p.ambient;
            foreach (var c in Camera.allCameras)
            {
                if (c == null) continue;
                c.clearFlags = CameraClearFlags.SolidColor;
                c.backgroundColor = p.background;
            }
            if (directionalLight != null)
            {
                directionalLight.color = p.lightColor;
                directionalLight.intensity = p.lightIntensity;
            }
            current = p;
        }

        private IEnumerator LerpTo(SkyParams target, float duration, SkyStage stage)
        {
            var start = current;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / duration);
                var p = new SkyParams
                {
                    background = Color.Lerp(start.background, target.background, u),
                    ambient = Color.Lerp(start.ambient, target.ambient, u),
                    lightColor = Color.Lerp(start.lightColor, target.lightColor, u),
                    lightIntensity = Mathf.Lerp(start.lightIntensity, target.lightIntensity, u),
                };
                ApplyInstant(p);
                yield return null;
            }
            current = target;
            ApplyInstant(target);
            UpdateLightning(stage);
            activeRoutine = null;
        }

        private void UpdateLightning(SkyStage stage)
        {
            if (lightningFlash != null) lightningFlash.IsActive = (stage == SkyStage.OminousPurple);
        }

        public SkyParams CurrentParams => current;
    }
}
