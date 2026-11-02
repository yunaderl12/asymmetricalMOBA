using UnityEngine;

namespace Game.Skill
{
    /// 着弾の瞬間の閃光。広がりながら消える光輪と点光源。
    public class FireFlash : MonoBehaviour
    {
        private MeshRenderer glow;
        private Light flashLight;
        private MaterialPropertyBlock block;
        private float startTime;
        private float size;
        private float duration;
        private Color startColor;
        private Color endColor;
        private float lightIntensity;

        public static void Spawn(Vector3 position, float size, float duration, float pull)
        {
            Spawn(position, size, duration, pull, new Color(3f, 2.6f, 2f), new Color(2f, 0.7f, 0.15f), new Color(1f, 0.6f, 0.25f));
        }

        public static void Spawn(Vector3 position, float size, float duration, float pull, Color startColor, Color endColor, Color lightColor, float lightIntensity = 6f)
        {
            var go = new GameObject("FireFlash");
            go.transform.position = position;
            var flash = go.AddComponent<FireFlash>();
            flash.size = size;
            flash.duration = duration;
            flash.startTime = Time.unscaledTime;
            flash.startColor = startColor;
            flash.endColor = endColor;
            flash.lightIntensity = lightIntensity;
            flash.block = new MaterialPropertyBlock();
            flash.glow = FireballVfx.NewMeshObject("Glow", go.transform, FireballVfx.QuadMesh, FireballVfx.Glow);
            flash.block.SetFloat("_Pull", pull);
            flash.flashLight = go.AddComponent<Light>();
            flash.flashLight.type = LightType.Point;
            flash.flashLight.color = lightColor;
            flash.flashLight.range = size * 4f;
            flash.Apply(0f);
        }

        // ヒットストップ中も進むように実時間で動かす。
        private void Update()
        {
            float t = (Time.unscaledTime - startTime) / duration;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            Apply(t);
        }

        private void Apply(float t)
        {
            float fade = (1f - t) * (1f - t);
            float eased = 1f - (1f - t) * (1f - t);
            glow.transform.localScale = Vector3.one * (size * (0.45f + 0.55f * eased));
            block.SetColor("_Color", Color.Lerp(startColor, endColor, t) * fade);
            glow.SetPropertyBlock(block);
            flashLight.intensity = lightIntensity * fade;
        }
    }
}
