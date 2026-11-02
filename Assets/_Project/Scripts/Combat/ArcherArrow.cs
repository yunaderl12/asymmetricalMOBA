using UnityEngine;
using Game.Skill;

namespace Game.Combat
{
    /// 塔が放つ矢。山なりに飛び、狙った相手を追って必ず当たる。相手が先に倒れたら、その場所に落ちて消える。
    public class ArcherArrow : MonoBehaviour
    {
        private const float ArcHeightPerDistance = 0.12f;
        private const float TargetHeight = 1f;
        private const float TrailSeconds = 0.14f;
        private const float TrailWidth = 0.12f;
        private const float HitRecoil = 0.5f;

        private static readonly Color Spark = new Color(1f, 0.92f, 0.7f);
        private static readonly Color SparkDeep = new Color(0.8f, 0.55f, 0.3f);

        private HealthComponent target;
        private GameObject shooter;
        private int damage;
        private float speed;
        private Quaternion modelOffset;
        private Vector3 start;
        private Vector3 lastTargetPoint;
        private float travelled;
        private TrailRenderer trail;

        public static void Launch(GameObject template, Vector3 from, HealthComponent target, GameObject shooter, int damage, float speed, Quaternion modelOffset)
        {
            var go = Instantiate(template, from, Quaternion.identity);
            go.name = "ArcherArrow";
            go.SetActive(true);
            var arrow = go.AddComponent<ArcherArrow>();
            arrow.target = target;
            arrow.shooter = shooter;
            arrow.damage = damage;
            arrow.speed = speed;
            arrow.modelOffset = modelOffset;
            arrow.start = from;
            arrow.lastTargetPoint = target.transform.position + Vector3.up * TargetHeight;

            arrow.trail = go.AddComponent<TrailRenderer>();
            arrow.trail.sharedMaterial = FireballVfx.Trail;
            arrow.trail.time = TrailSeconds;
            arrow.trail.widthMultiplier = TrailWidth;
            arrow.trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            arrow.trail.colorGradient = FireballVfx.FadeGradient(Color.white, Spark);
            arrow.trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void Update()
        {
            if (target != null && !target.IsDead) lastTargetPoint = target.transform.position + Vector3.up * TargetHeight;

            float distance = Mathf.Max(0.01f, Vector3.Distance(start, lastTargetPoint));
            travelled += speed * Time.deltaTime;
            float k = Mathf.Clamp01(travelled / distance);
            Vector3 next = Vector3.Lerp(start, lastTargetPoint, k) + Vector3.up * (4f * k * (1f - k) * distance * ArcHeightPerDistance);
            Vector3 move = next - transform.position;
            if (move.sqrMagnitude > 0.000001f) transform.rotation = Quaternion.LookRotation(move) * modelOffset;
            transform.position = next;
            if (k >= 1f) Arrive(move);
        }

        private void Arrive(Vector3 move)
        {
            if (target != null && !target.IsDead)
            {
                target.TakeDamage(damage, shooter, DamageType.Physical);
                ImpactPunch.Play(target.gameObject, move, HitRecoil);
            }
            FireballVfx.SpawnSparks(transform.position, -move.normalized, 8, Spark, SparkDeep);
            Destroy(gameObject);
        }
    }
}
