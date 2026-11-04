using UnityEngine;

namespace Game.Skill
{
    /// 近接スキルで共有する色と使い捨ての演出。
    public static class MeleeFx
    {
        /// クラスごとの演出の色。
        public struct Palette
        {
            public Color Core, Main, Light, Spark;
        }

        public static readonly Color SteelCore = new Color(2.2f, 2.5f, 2.8f, 1f);
        public static readonly Color SteelBlue = new Color(0.25f, 0.65f, 1.6f, 1f);
        public static readonly Color SteelLight = new Color(0.7f, 0.85f, 1f, 1f);
        public static readonly Color Dust = new Color(0.62f, 0.56f, 0.46f, 0.55f);

        public static readonly Palette Steel = new Palette { Core = SteelCore, Main = SteelBlue, Light = SteelLight, Spark = new Color(0.45f, 0.75f, 1f) };
        public static readonly Palette Ember = new Palette
        {
            Core = new Color(2.8f, 2f, 1f, 1f),
            Main = new Color(1.8f, 0.6f, 0.12f, 1f),
            Light = new Color(1f, 0.72f, 0.4f, 1f),
            Spark = new Color(1f, 0.5f, 0.12f),
        };

        /// 刃が当たった瞬間の閃光と火花。
        public static void Impact(Vector3 point, Vector3 direction, float flashSize, Palette palette)
        {
            if (direction.sqrMagnitude < 0.001f) direction = Vector3.up;
            FireFlash.Spawn(point, flashSize, 0.14f, 0.8f, palette.Core, palette.Main, palette.Light, 3f);
            FireballVfx.SpawnSparks(point, direction.normalized, 14, new Color(1f, 1f, 1f), palette.Spark);
        }

        /// 視界の外へ流れていく細い線。カメラの子に置く。止めるときは Stop(StopEmitting) で自動的に消える。
        public static ParticleSystem SpawnSpeedLines(Transform camera, Color color)
        {
            var ps = FireballVfx.NewParticles("SpeedLines", camera);
            ps.transform.localPosition = Vector3.forward * 7f;
            ps.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.14f, 0.24f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(26f, 38f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.05f);
            main.startColor = color;
            main.maxParticles = 90;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.rateOverTime = 260f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 4f;
            shape.radiusThickness = 0.35f;

            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.05f;
            r.lengthScale = 2f;

            ps.gameObject.SetActive(true);
            ps.Play();
            return ps;
        }

        /// 足元から舞う土煙。
        public static void DustBurst(Vector3 position, float radius, int count, float speed)
        {
            var ps = FireballVfx.NewParticles("MeleeDust", null);
            ps.transform.SetPositionAndRotation(position + Vector3.up * 0.1f, Quaternion.Euler(-90f, 0f, 0f));

            var main = ps.main;
            main.duration = 0.1f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startColor = Dust;
            main.gravityModifier = -0.05f;
            main.maxParticles = count;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.radiusThickness = 0.3f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.4f));

            var color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = FireballVfx.FadeGradient(Color.white, Color.white);

            ps.gameObject.SetActive(true);
            ps.Play();
        }
    }
}
