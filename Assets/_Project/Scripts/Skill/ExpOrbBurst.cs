using UnityEngine;

namespace Game.Skill
{
    /// 魔物が倒れたときに散らばって、倒した勇者へ吸い込まれる経験値の粒。見た目だけで、経験値は倒した瞬間に入っている。
    public class ExpOrbBurst : MonoBehaviour
    {
        private const int MinOrbs = 8;
        private const int MaxOrbs = 24;
        private const float ScatterSeconds = 0.4f;
        private const float ScatterSpeed = 5f;
        private const float ScatterDrag = 5f;
        private const float HomeSpeedStart = 6f;
        private const float HomeSpeedGrowth = 34f;
        private const float HomeSteer = 9f;
        private const float ArriveDistance = 0.5f;
        private const float MaxSeconds = 3f;
        private const float TargetHeight = 1.1f;

        private static readonly Color Gold = new Color(1f, 0.86f, 0.3f);
        private static readonly Color GoldDeep = new Color(1f, 0.6f, 0.1f);

        private ParticleSystem ps;
        private ParticleSystem.Particle[] particles;
        private Vector3[] velocities;
        private float[] delays;
        private bool[] arrived;
        private Transform target;
        private Vector3 lastTargetPoint;
        private float timer;
        private int remaining;

        public static void Spawn(Vector3 origin, Transform target, int exp)
        {
            if (target == null) return;
            var go = new GameObject("ExpOrbBurst");
            go.transform.position = origin;
            go.AddComponent<ExpOrbBurst>().Begin(origin, target, Mathf.Clamp(exp / 2 + 6, MinOrbs, MaxOrbs));
        }

        private void Begin(Vector3 origin, Transform target, int count)
        {
            this.target = target;
            lastTargetPoint = target.position + Vector3.up * TargetHeight;
            remaining = count;
            particles = new ParticleSystem.Particle[count];
            velocities = new Vector3[count];
            delays = new float[count];
            arrived = new bool[count];

            ps = FireballVfx.NewParticles("Orbs", transform);
            var main = ps.main;
            main.startLifetime = MaxSeconds + 1f;
            main.startSpeed = 0f;
            main.maxParticles = count;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.02f;
            r.lengthScale = 1.4f;
            ps.gameObject.SetActive(true);
            ps.Play();

            for (int i = 0; i < count; i++)
            {
                velocities[i] = (Random.onUnitSphere + Vector3.up * 0.9f).normalized * (ScatterSpeed * Random.Range(0.5f, 1.2f));
                delays[i] = ScatterSeconds * Random.Range(0.7f, 1.4f);
                particles[i].position = origin + Random.insideUnitSphere * 0.3f;
                particles[i].startColor = Color.Lerp(Gold, GoldDeep, Random.value);
                particles[i].startSize = Random.Range(0.12f, 0.22f);
                particles[i].startLifetime = MaxSeconds + 1f;
                particles[i].remainingLifetime = MaxSeconds + 1f;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            timer += dt;
            if (target != null) lastTargetPoint = target.position + Vector3.up * TargetHeight;

            int alive = 0;
            for (int i = 0; i < particles.Length; i++)
            {
                if (arrived[i]) continue;
                Vector3 position = particles[i].position;
                if (timer < delays[i])
                {
                    velocities[i] = Vector3.Lerp(velocities[i], Vector3.zero, 1f - Mathf.Exp(-ScatterDrag * dt));
                }
                else
                {
                    float homing = timer - delays[i];
                    Vector3 desired = (lastTargetPoint - position).normalized * (HomeSpeedStart + HomeSpeedGrowth * homing);
                    velocities[i] = Vector3.Lerp(velocities[i], desired, 1f - Mathf.Exp(-HomeSteer * dt));
                }
                position += velocities[i] * dt;
                if (timer >= delays[i] && (position - lastTargetPoint).sqrMagnitude < ArriveDistance * ArriveDistance)
                {
                    arrived[i] = true;
                    remaining--;
                    if (remaining == 0 && target != null) FireballVfx.SpawnSparks(lastTargetPoint, Vector3.up, 12, Gold, GoldDeep);
                    continue;
                }
                particles[i].position = position;
                particles[i].velocity = velocities[i];
                particles[alive] = particles[i];
                velocities[alive] = velocities[i];
                delays[alive] = delays[i];
                arrived[alive] = false;
                alive++;
            }
            for (int i = alive; i < particles.Length; i++) arrived[i] = true;
            ps.SetParticles(particles, alive);

            if (alive == 0 || timer >= MaxSeconds) Destroy(gameObject);
        }
    }
}
