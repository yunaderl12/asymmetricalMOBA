using UnityEngine;

namespace Game.Skill
{
    /// 火球の見た目（本体・光輪・点光源・火の粉・トレイル）。溜めから飛翔まで同じオブジェクトを使い続ける。
    public class FireballVisual : MonoBehaviour
    {
        private const float GlowScale = 3f;

        private Transform core;
        private MeshRenderer coreRenderer;
        private Transform glow;
        private MeshRenderer glowRenderer;
        private Light glowLight;
        private ParticleSystem chargeEmbers;
        private ParticleSystem flightEmbers;
        private TrailRenderer trail;
        private MaterialPropertyBlock coreBlock;
        private MaterialPropertyBlock glowBlock;
        private float radius;
        private float energy = 1f;
        private float taper;

        public static FireballVisual Create(Vector3 position)
        {
            var go = new GameObject("Fireball");
            go.transform.position = position;
            var v = go.AddComponent<FireballVisual>();
            v.Build();
            return v;
        }

        private void Build()
        {
            coreBlock = new MaterialPropertyBlock();
            glowBlock = new MaterialPropertyBlock();
            coreRenderer = FireballVfx.NewMeshObject("Core", transform, FireballVfx.SphereMesh, FireballVfx.Orb);
            core = coreRenderer.transform;
            glowRenderer = FireballVfx.NewMeshObject("Glow", transform, FireballVfx.QuadMesh, FireballVfx.Glow);
            glow = glowRenderer.transform;

            glowLight = gameObject.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.color = new Color(1f, 0.55f, 0.2f);

            BuildChargeEmbers();
            BuildFlightEmbers();
            BuildTrail();
            SetShape(0.01f, 1f, 1f, 0f);
        }

        // 溜め中に火球へ吸い込まれる火の粉。
        private void BuildChargeEmbers()
        {
            chargeEmbers = FireballVfx.NewParticles("ChargeEmbers", transform);
            var main = chargeEmbers.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = 0.3f;
            main.startSpeed = -1.6f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.03f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.45f), new Color(1f, 0.45f, 0.1f));
            main.maxParticles = 40;
            var emission = chargeEmbers.emission;
            emission.rateOverTime = 45f;
            var shape = chargeEmbers.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;
            shape.radiusThickness = 0f;
            var r = chargeEmbers.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.05f;
            r.lengthScale = 1.5f;
            chargeEmbers.gameObject.SetActive(true);
            chargeEmbers.Play();
        }

        // 飛翔中に後ろへ残る火の粉。距離に応じて出るのでワールド空間で放出する。
        private void BuildFlightEmbers()
        {
            flightEmbers = FireballVfx.NewParticles("FlightEmbers", transform);
            var main = flightEmbers.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.1f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.8f, 0.4f), new Color(1f, 0.4f, 0.08f));
            main.gravityModifier = -0.15f;
            main.maxParticles = 60;
            var emission = flightEmbers.emission;
            emission.rateOverDistance = 9f;
            var shape = flightEmbers.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;
            var size = flightEmbers.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            var color = flightEmbers.colorOverLifetime;
            color.enabled = true;
            color.color = FireballVfx.FadeGradient(Color.white, new Color(1f, 0.25f, 0.05f));
        }

        private void BuildTrail()
        {
            // 着弾時に切り離せるよう子オブジェクトに持たせる。
            var trailObject = new GameObject("Trail");
            trailObject.transform.SetParent(transform, false);
            trail = trailObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = FireballVfx.Trail;
            trail.time = 0.08f;
            trail.minVertexDistance = 0.05f;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.colorGradient = FireballVfx.FadeGradient(new Color(1f, 0.75f, 0.3f), new Color(1f, 0.2f, 0.03f));
            trail.textureMode = LineTextureMode.Stretch;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.emitting = false;
        }

        /// stretch: 進行方向の伸び / width: 太さの倍率 / taper: 後ろのすぼまり（0〜1）。
        /// 頭の位置を保つよう、伸びた分だけ本体を後ろへずらす。
        public void SetShape(float newRadius, float stretch, float width, float newTaper)
        {
            radius = newRadius;
            taper = newTaper;
            float d = radius * 2f;
            core.localScale = new Vector3(d * width, d * width, d * stretch);
            core.localPosition = new Vector3(0f, 0f, -radius * (stretch - 1f));
            glow.localScale = Vector3.one * (d * GlowScale);
            if (trail != null) trail.widthMultiplier = d * 0.55f;
            if (chargeEmbers != null)
            {
                var chargeShape = chargeEmbers.shape;
                chargeShape.radius = Mathf.Max(0.12f, radius * 2.2f);
            }
            Apply();
        }

        public void SetEnergy(float value)
        {
            energy = Mathf.Clamp01(value);
            Apply();
        }

        private void Apply()
        {
            coreBlock.SetFloat("_Taper", taper);
            coreBlock.SetFloat("_Energy", energy);
            coreRenderer.SetPropertyBlock(coreBlock);
            glowBlock.SetColor("_Color", new Color(1.3f, 0.6f, 0.12f) * energy);
            glowRenderer.SetPropertyBlock(glowBlock);
            glowLight.intensity = 12f * radius * energy;
            glowLight.range = 1f + radius * 18f;
        }

        /// 溜めの火の粉を止めて、飛翔用のトレイルと火の粉を出し始める。
        public void BeginFlight()
        {
            if (chargeEmbers != null) Destroy(chargeEmbers.gameObject);
            chargeEmbers = null;
            flightEmbers.gameObject.SetActive(true);
            flightEmbers.Play();
            trail.Clear();
            trail.emitting = true;
        }

        /// 本体が止まっても尾だけ遅れて残るよう、トレイルと火の粉を切り離す。
        public void ReleaseTail()
        {
            if (flightEmbers != null && flightEmbers.gameObject.activeSelf)
            {
                flightEmbers.transform.SetParent(null, true);
                flightEmbers.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                Destroy(flightEmbers.gameObject, 0.6f);
            }
            flightEmbers = null;
            if (trail != null)
            {
                trail.transform.SetParent(null, true);
                trail.emitting = false;
                trail.autodestruct = true;
                trail = null;
            }
        }
    }
}
