using UnityEngine;
using Game.Combat;

namespace Game.Skill
{
    /// バフの間、体のまわりに立ちのぼる光の粒。他の人からバフ中だと分かるようにする。時間切れか死亡で止まる。
    public class BuffAura : MonoBehaviour
    {
        private const float BodyRadius = 0.8f;
        private const float EmitPerSecond = 26f;

        private ParticleSystem motes;
        private HealthComponent health;
        private float timer;

        public static void Attach(GameObject owner, float seconds, Color color, float sizeScale)
        {
            var go = new GameObject("BuffAura");
            go.transform.SetParent(owner.transform, false);
            var aura = go.AddComponent<BuffAura>();
            aura.timer = seconds;
            aura.health = owner.GetComponent<HealthComponent>();
            aura.Build(color, sizeScale);
        }

        private void Build(Color color, float sizeScale)
        {
            motes = FireballVfx.NewParticles("Motes", transform);
            motes.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            var main = motes.main;
            main.duration = 1f;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.15f);
            main.startColor = color;
            main.maxParticles = 80;

            var emission = motes.emission;
            emission.rateOverTime = EmitPerSecond * sizeScale;

            var shape = motes.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = BodyRadius * sizeScale;
            shape.radiusThickness = 0.3f;

            var size = motes.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            motes.gameObject.SetActive(true);
            motes.Play();
        }

        private void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0f && (health == null || !health.IsDead)) return;
            enabled = false;
            motes.transform.SetParent(null, true);
            var main = motes.main;
            main.stopAction = ParticleSystemStopAction.Destroy;
            motes.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(gameObject);
        }
    }
}
