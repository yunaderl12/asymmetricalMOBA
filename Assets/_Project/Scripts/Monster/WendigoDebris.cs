using UnityEngine;
using Game.Combat;
using Game.Skill;

namespace Game.Monster
{
    /// ウェンディゴが地面を引っ掻いて飛ばす瓦礫（土と石のかたまり）。放物線を描いて飛び、回りながら勇者に当たる。
    /// 地面に落ちると砕けて消える。1つの瓦礫は1人に1回だけ当たる。
    public class WendigoDebris : MonoBehaviour
    {
        private const float Gravity = 14f;
        private static readonly Color[] Colors =
        {
            new Color(0.30f, 0.20f, 0.11f), new Color(0.22f, 0.15f, 0.08f), new Color(0.36f, 0.35f, 0.33f), new Color(0.14f, 0.24f, 0.08f),
        };
        private static Material material;

        private GameObject owner;
        private WendigoData data;
        private Vector3 velocity;
        private Vector3 spin;
        private int damage;
        private float traveled;

        public static void Spawn(GameObject owner, Vector3 start, Vector3 velocity, int damage, float size, WendigoData data)
        {
            var go = new GameObject("WendigoDebris");
            go.transform.position = start;
            go.transform.rotation = Random.rotation;
            go.transform.localScale = Vector3.one * size;
            go.AddComponent<MeshFilter>().sharedMesh = EarthChips.ShardMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Material();
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", Colors[Random.Range(0, Colors.Length)]);
            renderer.SetPropertyBlock(block);
            var d = go.AddComponent<WendigoDebris>();
            d.owner = owner;
            d.data = data;
            d.velocity = velocity;
            d.damage = damage;
            d.spin = Random.insideUnitSphere * 720f;
        }

        private static Material Material()
        {
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "WendigoDebris" };
            material.SetFloat("_Smoothness", 0.05f);
            return material;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            Vector3 from = transform.position;
            velocity += Vector3.down * Gravity * dt;
            Vector3 to = from + velocity * dt;
            traveled += (to - from).magnitude;
            transform.Rotate(spin * dt, Space.Self);

            foreach (var col in Physics.OverlapCapsule(from, to, data.debrisHitRadius, ~0, QueryTriggerInteraction.Collide))
            {
                if (!col.CompareTag("Player")) continue;
                var hp = col.GetComponentInParent<HealthComponent>();
                if (hp == null || hp.IsDead) continue;
                hp.TakeDamage(damage, owner, DamageType.Physical);
                if (hp != null && !hp.IsDead) ImpactPunch.Play(hp.gameObject, new Vector3(velocity.x, 0f, velocity.z).normalized, 0.6f);
                Shatter(to);
                return;
            }
            if (Physics.Linecast(from, to, out var hit, ~0, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<BossBase>() == null && !hit.collider.CompareTag("Player"))
            {
                Shatter(hit.point);
                return;
            }
            transform.position = to;
            if (traveled >= data.debrisRange * 1.4f || to.y < -20f) Destroy(gameObject);
        }

        private void Shatter(Vector3 at)
        {
            EarthChips.Spawn(at, 0.6f, 5);
            Destroy(gameObject);
        }
    }
}
