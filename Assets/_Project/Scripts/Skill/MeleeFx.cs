using UnityEngine;

namespace Game.Skill
{
    /// 近接スキルで共有する色と使い捨ての演出。
    public static class MeleeFx
    {
        public static readonly Color SteelCore = new Color(2.2f, 2.5f, 2.8f, 1f);
        public static readonly Color SteelBlue = new Color(0.25f, 0.65f, 1.6f, 1f);
        public static readonly Color SteelLight = new Color(0.7f, 0.85f, 1f, 1f);
        public static readonly Color Dust = new Color(0.62f, 0.56f, 0.46f, 0.55f);

        /// 刃が当たった瞬間の閃光と火花。
        public static void Impact(Vector3 point, Vector3 direction, float flashSize)
        {
            if (direction.sqrMagnitude < 0.001f) direction = Vector3.up;
            FireFlash.Spawn(point, flashSize, 0.14f, 0.8f, SteelCore, SteelBlue, SteelLight, 3f);
            FireballVfx.SpawnSparks(point, direction.normalized, 14, new Color(1f, 1f, 1f), new Color(0.45f, 0.75f, 1f));
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
