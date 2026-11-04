using UnityEngine;

namespace Game.Skill
{
    /// 魔物が破裂する瞬間の閃光と、四方へ飛び散る白い粒。
    public static class DeathBurst
    {
        private static readonly Color FlashCore = new Color(3f, 3f, 3f);
        private static readonly Color FlashEnd = new Color(1.2f, 1.2f, 1.3f);

        public static void Spawn(Vector3 position, int count)
        {
            FireFlash.Spawn(position, 3.2f, 0.16f, 0.6f, FlashCore, FlashEnd, Color.white, 2.5f);

            var ps = FireballVfx.NewParticles("DeathBurst", null);
            ps.transform.position = position;

            var main = ps.main;
            main.duration = 0.1f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.65f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 12f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.28f);
            main.startColor = Color.white;
            main.gravityModifier = 0.35f;
            main.maxParticles = count;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.35f;

            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.dampen = 0.12f;
            limit.limit = 1f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            ps.gameObject.SetActive(true);
            ps.Play();
        }
    }
}
