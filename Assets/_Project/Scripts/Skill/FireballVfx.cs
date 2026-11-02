using UnityEngine;

namespace Game.Skill
{
    /// 火球演出で共有するマテリアル・メッシュと、使い捨てパーティクルの生成。
    /// シェーダーは Resources/VFX に置いてあるのでビルドにも含まれる。
    public static class FireballVfx
    {
        private static Material orb, shell, glow, particle, trail;
        private static readonly System.Collections.Generic.Dictionary<string, Material> ExtraMaterials = new System.Collections.Generic.Dictionary<string, Material>();
        private static Mesh sphereMesh, quadMesh;

        public static Material Orb => GetMaterial(ref orb, "VFX/FireOrb");
        public static Material Shell => GetMaterial(ref shell, "VFX/FireShell");
        public static Material Glow => GetMaterial(ref glow, "VFX/FireGlow");
        public static Material Particle => GetMaterial(ref particle, "VFX/FireParticle");
        public static Material Trail
        {
            get
            {
                if (trail == null)
                {
                    GetMaterial(ref trail, "VFX/FireParticle");
                    trail.SetFloat("_Radial", 0f);
                }
                return trail;
            }
        }

        public static Mesh SphereMesh => GetPrimitiveMesh(ref sphereMesh, PrimitiveType.Sphere);
        public static Mesh QuadMesh => GetPrimitiveMesh(ref quadMesh, PrimitiveType.Quad);

        /// 他の演出のシェーダー用。パスごとにマテリアルを1つ共有する。
        public static Material GetMaterial(string path)
        {
            ExtraMaterials.TryGetValue(path, out var cached);
            var material = GetMaterial(ref cached, path);
            ExtraMaterials[path] = material;
            return material;
        }

        private static Material GetMaterial(ref Material cache, string path)
        {
            if (cache != null) return cache;
            var shader = Resources.Load<Shader>(path);
            if (shader == null)
            {
                Debug.LogWarning("[FireballVfx] Resources/" + path + " not found");
                return null;
            }
            cache = new Material(shader);
            return cache;
        }

        private static Mesh GetPrimitiveMesh(ref Mesh cache, PrimitiveType type)
        {
            if (cache != null) return cache;
            var temp = GameObject.CreatePrimitive(type);
            cache = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);
            return cache;
        }

        /// 当たり判定を持たない描画専用オブジェクトを作る。
        public static MeshRenderer NewMeshObject(string name, Transform parent, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        /// 無効状態で作る。設定後に呼び出し側で SetActive(true) と Play() を行う（再生中は設定できない項目があるため）。
        public static ParticleSystem NewParticles(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            if (parent != null) go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Particle;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var main = ps.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            return ps;
        }

        public static Gradient FadeGradient(Color start, Color end)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        /// 火球からぴゅっと飛ぶ火花。進行方向へ抜けつつ、重力で放物線を描いて消える。
        public static void SpawnSparks(Vector3 position, Vector3 direction, int count)
        {
            SpawnSparks(position, direction, count, new Color(1f, 0.9f, 0.55f), new Color(1f, 0.5f, 0.12f), new Color(1f, 0.3f, 0.05f));
        }

        public static void SpawnSparks(Vector3 position, Vector3 direction, int count, Color start, Color end)
        {
            SpawnSparks(position, direction, count, start, end, end);
        }

        private static void SpawnSparks(Vector3 position, Vector3 direction, int count, Color start, Color end, Color fade)
        {
            var ps = NewParticles("FireSparks", null);
            ps.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));

            var main = ps.main;
            main.duration = 0.1f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.34f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            main.startColor = new ParticleSystem.MinMaxGradient(start, end);
            main.gravityModifier = 1.4f;
            main.maxParticles = count;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 70f;
            shape.radius = 0.08f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = FadeGradient(Color.white, fade);

            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.035f;
            r.lengthScale = 1.5f;

            ps.gameObject.SetActive(true);
            ps.Play();
        }
    }
}
