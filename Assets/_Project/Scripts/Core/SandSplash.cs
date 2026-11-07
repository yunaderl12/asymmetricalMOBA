using UnityEngine;

namespace Game.Core
{
    /// 砂地を歩いたときに足元から跳ねる砂の粒。シーンに1つだけ置いた ParticleSystem から粒を出す。
    /// 粒はローポリの見た目に合わせて小さな八面体で、飛びながら回って縮んで消える。
    public static class SandSplash
    {
        private const float GroundProbeUp = 1.5f;
        private const float GroundProbeDown = 4f;
        private const float MinLife = 0.45f, MaxLife = 0.75f;
        private const float MinSize = 0.16f, MaxSize = 0.32f;
        private const float MinUp = 2.4f, MaxUp = 4.4f;
        private const float Spread = 2.0f;
        private const float KickBack = 0.18f;
        // 地面の砂より少し濃い色と明るい色を混ぜて、同じ色の砂の上でも粒が見えるようにする。
        private static readonly Color[] Colors =
        {
            new Color(0.70f, 0.55f, 0.33f), new Color(0.62f, 0.47f, 0.27f), new Color(0.96f, 0.88f, 0.68f),
        };

        // 川の水しぶき。白っぽい水色の粒。
        private static readonly Color[] WaterColors =
        {
            new Color(0.80f, 0.94f, 1.00f), new Color(0.55f, 0.82f, 0.95f), new Color(0.95f, 1.00f, 1.00f),
        };

        private static ParticleSystem system;

        /// 浅瀬を歩いたときの水しぶき。surfaceY は水面の高さ。粒は砂より高く跳ねて、細かい。
        public static void SpawnWater(Vector3 position, Vector3 velocity, int drops, float surfaceY, float scale = 1f)
        {
            if (!EnsureSystem()) return;
            var kick = -new Vector3(velocity.x, 0f, velocity.z) * KickBack + new Vector3(velocity.x, 0f, velocity.z) * 0.12f;
            var p = new ParticleSystem.EmitParams();
            for (int i = 0; i < drops; i++)
            {
                var side = Random.insideUnitCircle * Spread * 1.3f * scale;
                p.position = new Vector3(position.x + side.x * 0.22f, surfaceY + 0.03f, position.z + side.y * 0.22f);
                p.velocity = new Vector3(side.x, Random.Range(MinUp * 1.1f, MaxUp * 1.25f) * Mathf.Sqrt(scale), side.y) + kick;
                p.startLifetime = Random.Range(MinLife, MaxLife);
                p.startSize = Random.Range(MinSize * 0.6f, MaxSize * 0.75f) * scale;
                p.startColor = WaterColors[Random.Range(0, WaterColors.Length)];
                p.rotation3D = new Vector3(Random.value * 360f, Random.value * 360f, Random.value * 360f);
                p.angularVelocity3D = Random.insideUnitSphere * 540f;
                system.Emit(p, 1);
            }
        }

        /// position: 歩いている者の位置、velocity: その移動の速さ（m/s）。粒は少し後ろへ蹴り上がる。
        /// scale: 歩く者の見た目の大きさ（魔王は大きいので粒も高く大きく跳ねる）。
        public static void Spawn(Vector3 position, Vector3 velocity, int grains, float scale = 1f)
        {
            if (!EnsureSystem()) return;
            Vector3 ground = position;
            if (Physics.Raycast(position + Vector3.up * GroundProbeUp, Vector3.down, out var hit, GroundProbeUp + GroundProbeDown, ~0, QueryTriggerInteraction.Ignore))
                ground = hit.point;
            var kick = -new Vector3(velocity.x, 0f, velocity.z) * KickBack;
            var p = new ParticleSystem.EmitParams();
            for (int i = 0; i < grains; i++)
            {
                var side = Random.insideUnitCircle * Spread * scale;
                p.position = ground + new Vector3(side.x * 0.25f, 0.08f, side.y * 0.25f);
                p.velocity = new Vector3(side.x, Random.Range(MinUp, MaxUp) * Mathf.Sqrt(scale), side.y) + kick;
                p.startLifetime = Random.Range(MinLife, MaxLife);
                p.startSize = Random.Range(MinSize, MaxSize) * scale;
                p.startColor = Colors[Random.Range(0, Colors.Length)];
                p.rotation3D = new Vector3(Random.value * 360f, Random.value * 360f, Random.value * 360f);
                p.angularVelocity3D = Random.insideUnitSphere * 540f;
                system.Emit(p, 1);
            }
        }

        private static bool EnsureSystem()
        {
            if (system != null) return true;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return false;

            var go = new GameObject("SandSplash");
            system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 1.1f;
            main.startRotation3D = true;
            main.maxParticles = 800;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.6f, 0.8f), new Keyframe(1f, 0f)));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = BuildGrain();
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var material = new Material(shader) { name = "SandSplash" };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            renderer.sharedMaterial = material;
            system.Play();
            return true;
        }

        /// 面ごとに法線を分けた八面体（ローポリの砂粒）。
        private static Mesh BuildGrain()
        {
            Vector3[] c = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
            int[][] faces =
            {
                new[] { 0, 4, 3 }, new[] { 0, 3, 5 }, new[] { 0, 5, 2 }, new[] { 0, 2, 4 },
                new[] { 1, 3, 4 }, new[] { 1, 5, 3 }, new[] { 1, 2, 5 }, new[] { 1, 4, 2 },
            };
            var vertices = new Vector3[24];
            var triangles = new int[24];
            for (int f = 0; f < faces.Length; f++)
                for (int k = 0; k < 3; k++)
                {
                    vertices[f * 3 + k] = c[faces[f][k]] * 0.5f;
                    triangles[f * 3 + k] = f * 3 + k;
                }
            var mesh = new Mesh { name = "SandGrain", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
