using UnityEngine;
using Game.Skill;

namespace Game.Monster
{
    /// 塔のてっぺんの水晶の光。普段はほのかに光り、溜めるほど明るく脈打って火の粉を吸い込み、放つ瞬間に弾ける。
    public class SpawnerTowerVisual : MonoBehaviour
    {
        private const int BodyMaterialIndex = 0;
        private const int CrystalMaterialIndex = 1;
        private const float IdleGlow = 0.35f;
        private const float ChargedGlow = 2.2f;
        private const float ReleaseGlow = 5f;
        private const float ReleaseSeconds = 0.3f;
        private const float GlowSize = 6f;
        private const float GlowPull = 2.5f; // 水晶に埋もれないようカメラ側へ寄せる
        private const float ReleaseGlowSize = 7f;
        private const float LightRange = 16f;
        private const float LightIntensity = 9f;
        private const float SparkRadius = 2.6f;
        private const float SparkInwardSpeed = 5.5f;
        private const float SparkRate = 90f;

        private static readonly Color Pink = new Color(1f, 0.25f, 0.75f);
        private static readonly Color DeepPurple = new Color(0.4f, 0.06f, 0.75f);
        private static readonly Color DestroyedTint = new Color(0.28f, 0.28f, 0.32f);
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int PullId = Shader.PropertyToID("_Pull");

        [SerializeField] private MeshRenderer towerRenderer;
        [SerializeField] private Transform crystal;

        private MaterialPropertyBlock block;
        private MeshRenderer glow;
        private MaterialPropertyBlock glowBlock;
        private Light chargeLight;
        private ParticleSystem sparks;
        private float charge;
        private float release;
        private float phase;
        private bool dead;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            glowBlock = new MaterialPropertyBlock();
            phase = Random.value * 10f;

            glow = FireballVfx.NewMeshObject("CrystalGlow", crystal, FireballVfx.QuadMesh, FireballVfx.Glow);
            glow.enabled = false;
            glowBlock.SetFloat(PullId, GlowPull);

            var lightObject = new GameObject("CrystalLight");
            lightObject.transform.SetParent(crystal, false);
            chargeLight = lightObject.AddComponent<Light>();
            chargeLight.type = LightType.Point;
            chargeLight.color = Pink;
            chargeLight.range = LightRange;
            chargeLight.intensity = 0f;
            chargeLight.enabled = false;

            sparks = FireballVfx.NewParticles("ChargeSparks", crystal);
            var main = sparks.main;
            main.startLifetime = SparkRadius / SparkInwardSpeed;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.startColor = Pink;
            main.maxParticles = 80;
            var shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = SparkRadius;
            shape.radiusThickness = 0f;
            var velocity = sparks.velocityOverLifetime;
            velocity.enabled = true;
            velocity.radial = -SparkInwardSpeed;
            sparks.gameObject.SetActive(true);
            sparks.Play();
        }

        /// 溜めの進み具合（0〜1）。
        public void SetCharge(float amount)
        {
            charge = Mathf.Clamp01(amount);
        }

        public void Release()
        {
            charge = 0f;
            release = 1f;
        }

        /// 溜めを撃たずにやめる。
        public void Cancel()
        {
            charge = 0f;
        }

        /// 破壊された塔は黒ずみ、水晶の光も消える。修復はない。
        public void SetDestroyed()
        {
            dead = true;
            charge = 0f;
            release = 0f;
            glow.enabled = false;
            chargeLight.enabled = false;
            var emission = sparks.emission;
            emission.rateOverTime = 0f;
            for (int i = BodyMaterialIndex; i <= CrystalMaterialIndex; i++)
            {
                towerRenderer.GetPropertyBlock(block, i);
                block.SetColor(BaseColorId, DestroyedTint);
                block.SetColor(EmissionId, Color.black);
                towerRenderer.SetPropertyBlock(block, i);
            }
            enabled = false;
        }

        private void Update()
        {
            if (dead) return;
            release = Mathf.Max(0f, release - Time.deltaTime / ReleaseSeconds);

            float level = Mathf.Pow(charge, 1.4f);
            float t = Time.time + phase;
            // 溜めるほど脈が速くなる。
            float pulse = 1f + Mathf.Sin(t * Mathf.Lerp(2f, 34f, charge)) * Mathf.Lerp(0.1f, 0.3f, charge);
            float glowAmount = Mathf.Lerp(IdleGlow, ChargedGlow, level) * pulse + release * ReleaseGlow;
            Color color = Color.Lerp(DeepPurple, Pink, Mathf.Max(level, release));

            towerRenderer.GetPropertyBlock(block, CrystalMaterialIndex);
            block.SetColor(EmissionId, color * glowAmount);
            towerRenderer.SetPropertyBlock(block, CrystalMaterialIndex);

            float size = level * GlowSize * pulse + release * ReleaseGlowSize;
            glow.enabled = size > 0.02f;
            if (glow.enabled)
            {
                glow.transform.localScale = Vector3.one * size;
                glowBlock.SetColor(ColorId, color * (level + release));
                glow.SetPropertyBlock(glowBlock);
            }

            float intensity = (level * pulse + release) * LightIntensity;
            chargeLight.enabled = intensity > 0.02f;
            chargeLight.intensity = intensity;
            chargeLight.color = color;

            var emission = sparks.emission;
            emission.rateOverTime = charge > 0f ? SparkRate * Mathf.Sqrt(charge) : 0f;
        }
    }
}
