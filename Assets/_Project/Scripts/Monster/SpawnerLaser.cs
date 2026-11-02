using UnityEngine;
using Game.Skill;

namespace Game.Monster
{
    /// スポナーの塔が放つレーザーの演出。当たり判定は Spawner が行う。
    public class SpawnerLaser : MonoBehaviour
    {
        private const float GrowSeconds = 0.08f;
        private const float HoldSeconds = 0.14f;
        private const float FadeSeconds = 0.28f;
        private const float Width = 1.9f;
        private const float EmberSpacing = 0.7f;

        private static readonly Color Pink = new Color(1f, 0.25f, 0.75f);
        private static readonly Color DeepPurple = new Color(0.32f, 0.04f, 0.5f);
        private static readonly Color FlashStart = new Color(2.5f, 0.9f, 2.2f);
        private static readonly Color FlashEnd = new Color(0.7f, 0.1f, 1.1f);
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private static readonly int LengthId = Shader.PropertyToID("_Length");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");

        private static Mesh beamMesh;

        private MeshRenderer beam;
        private MaterialPropertyBlock block;
        private Vector3 end;
        private float age;
        private bool reached;

        public static void Spawn(Vector3 from, Vector3 to)
        {
            var go = new GameObject("SpawnerLaser");
            go.transform.position = from;
            go.AddComponent<SpawnerLaser>().Begin(from, to);
        }

        private void Begin(Vector3 from, Vector3 to)
        {
            end = to;
            float length = Mathf.Max(0.1f, Vector3.Distance(from, to));
            beam = FireballVfx.NewMeshObject("Beam", transform, BeamMesh(), FireballVfx.GetMaterial("VFX/SpawnerLaser"));
            beam.transform.rotation = Quaternion.LookRotation((to - from) / length);
            beam.transform.localScale = new Vector3(Width, Width, length);
            block = new MaterialPropertyBlock();
            block.SetFloat(LengthId, length);
            block.SetFloat(SeedId, Random.value * 100f);
            Apply(0f, 0f);

            FireFlash.Spawn(from, 3f, 0.3f, 0f, FlashStart, FlashEnd, Pink, 5f);
            SpawnEmbers(from, to, length);
        }

        private void Update()
        {
            age += Time.deltaTime;
            float progress = Mathf.Clamp01(age / GrowSeconds);
            float fade = Mathf.Clamp01((age - GrowSeconds - HoldSeconds) / FadeSeconds);
            Apply(progress, fade);

            if (!reached && progress >= 1f)
            {
                reached = true;
                FireFlash.Spawn(end, 3.5f, 0.35f, 0f, FlashStart, FlashEnd, Pink, 6f);
                FireballVfx.SpawnSparks(end, Vector3.up, 22, Pink, DeepPurple);
            }
            if (fade >= 1f) Destroy(gameObject);
        }

        private void Apply(float progress, float fade)
        {
            block.SetFloat(ProgressId, progress);
            block.SetFloat(FadeId, fade);
            beam.SetPropertyBlock(block);
        }

        // 炎の名残の火の粉が、線に沿って立ちのぼる。
        private void SpawnEmbers(Vector3 from, Vector3 to, float length)
        {
            var ps = FireballVfx.NewParticles("Embers", transform);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.gravityModifier = -0.25f;
            int count = Mathf.CeilToInt(length / EmberSpacing);
            main.maxParticles = count;
            var color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = FireballVfx.FadeGradient(Pink, DeepPurple);
            ps.gameObject.SetActive(true);

            var emit = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                emit.position = Vector3.Lerp(from, to, Random.value) + Random.insideUnitSphere * 0.3f;
                emit.velocity = Random.insideUnitSphere * 0.8f;
                emit.applyShapeToPosition = false;
                ps.Emit(emit, 1);
            }
        }

        private static Mesh BeamMesh()
        {
            if (beamMesh != null) return beamMesh;
            beamMesh = new Mesh { name = "SpawnerLaserBeam" };
            beamMesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f),
                new Vector3(-0.5f, 0f, 1f), new Vector3(0.5f, 0f, 1f),
            };
            beamMesh.uv = new[] { new Vector2(-1f, 0f), new Vector2(1f, 0f), new Vector2(-1f, 1f), new Vector2(1f, 1f) };
            beamMesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            // 帯は頂点シェーダーでカメラへ向けるので、広めの範囲にしておく。
            beamMesh.bounds = new Bounds(new Vector3(0f, 0f, 0.5f), Vector3.one);
            return beamMesh;
        }
    }
}
