using UnityEngine;
using Game.Combat;
using Game.Skill;

namespace Game.Monster
{
    /// 雑魚魔物が飛ばすまっすぐな飛び道具（ドライアドの木の葉の刃・ハーピーの羽・イフリートの火球）。
    /// 勇者に当たるとダメージ。地面や壁に当たるとはじけて消える。魔物は通り抜ける。
    public class MonsterBolt : MonoBehaviour
    {
        public enum Kind { Leaf, Feather, Fire }

        private static readonly Color LeafLight = new Color(0.62f, 0.95f, 0.35f);
        private static readonly Color LeafDark = new Color(0.16f, 0.52f, 0.16f);
        private static readonly Color FeatherLight = new Color(1f, 0.92f, 0.7f);
        private static readonly Color FeatherDark = new Color(0.95f, 0.5f, 0.15f);
        private static Material unlit;

        private GameObject owner;
        private Kind kind;
        private Vector3 direction;
        private float speed;
        private float range;
        private float hitRadius;
        private int damage;
        private float traveled;
        private Transform spin;
        private TrailRenderer trail;
        private FireballVisual fire;

        /// fire を渡すと、その火球（溜めに使ったもの）をそのまま飛ばす。
        public static MonsterBolt Spawn(Kind kind, GameObject owner, Vector3 start, Vector3 direction, float speed, float range,
            float hitRadius, int damage, FireballVisual fire = null)
        {
            var go = fire != null ? fire.gameObject : new GameObject("MonsterBolt_" + kind);
            go.transform.position = start;
            go.transform.rotation = Quaternion.LookRotation(direction);
            var b = go.AddComponent<MonsterBolt>();
            b.owner = owner;
            b.kind = kind;
            b.direction = direction.normalized;
            b.speed = speed;
            b.range = range;
            b.hitRadius = hitRadius;
            b.damage = damage;
            b.fire = fire;
            if (fire != null) fire.BeginFlight();
            else b.Build();
            return b;
        }

        private static Material Unlit()
        {
            if (unlit == null) unlit = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "MonsterBolt" };
            return unlit;
        }

        private void Build()
        {
            spin = new GameObject("Spin").transform;
            spin.SetParent(transform, false);
            var block = new MaterialPropertyBlock();
            if (kind == Kind.Leaf)
            {
                // 細長い葉の刃。くるくる回りながら飛ぶ。
                var blade = FireballVfx.NewMeshObject("Blade", spin, FireballVfx.SphereMesh, Unlit());
                blade.transform.localScale = new Vector3(0.34f, 0.05f, 0.75f);
                block.SetColor("_BaseColor", LeafLight);
                blade.SetPropertyBlock(block);
                var vein = FireballVfx.NewMeshObject("Vein", spin, FireballVfx.SphereMesh, Unlit());
                vein.transform.localScale = new Vector3(0.06f, 0.07f, 0.7f);
                block.SetColor("_BaseColor", LeafDark);
                vein.SetPropertyBlock(block);
            }
            else
            {
                // 長い羽根。根元が白く、先がオレンジ。
                var vane = FireballVfx.NewMeshObject("Vane", spin, FireballVfx.SphereMesh, Unlit());
                vane.transform.localScale = new Vector3(0.22f, 0.04f, 0.95f);
                vane.transform.localPosition = new Vector3(0f, 0f, -0.1f);
                block.SetColor("_BaseColor", FeatherDark);
                vane.SetPropertyBlock(block);
                var shaft = FireballVfx.NewMeshObject("Shaft", spin, FireballVfx.SphereMesh, Unlit());
                shaft.transform.localScale = new Vector3(0.05f, 0.06f, 1.1f);
                block.SetColor("_BaseColor", FeatherLight);
                shaft.SetPropertyBlock(block);
            }
            var trailObject = new GameObject("Trail");
            trailObject.transform.SetParent(transform, false);
            trail = trailObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = FireballVfx.Trail;
            trail.time = 0.18f;
            trail.widthMultiplier = kind == Kind.Leaf ? 0.25f : 0.18f;
            trail.minVertexDistance = 0.1f;
            Color c = kind == Kind.Leaf ? LeafLight : FeatherLight;
            trail.colorGradient = FireballVfx.FadeGradient(c, new Color(c.r, c.g, c.b, 0f));
        }

        private void Update()
        {
            float step = Mathf.Min(speed * Time.deltaTime, range - traveled);
            Vector3 from = transform.position;
            Vector3 to = from + direction * step;
            traveled += step;
            if (spin != null)
            {
                if (kind == Kind.Leaf) spin.Rotate(0f, 0f, 900f * Time.deltaTime, Space.Self);
                else spin.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(traveled * 0.8f) * 25f);
            }

            foreach (var col in Physics.OverlapCapsule(from, to, hitRadius, ~0, QueryTriggerInteraction.Collide))
            {
                if (!col.CompareTag("Player")) continue;
                var hp = col.GetComponentInParent<HealthComponent>();
                if (hp == null || hp.IsDead) continue;
                hp.TakeDamage(damage, owner, kind == Kind.Fire ? DamageType.Magical : DamageType.Physical);
                if (kind == Kind.Fire) FireHitEffect.Play(hp.gameObject);
                Burst(to);
                return;
            }
            // 地面・壁に当たるとはじける。魔物（自分を含む）と勇者のトリガーは通り抜ける。
            if (Physics.Linecast(from, to, out var hit, ~0, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<Monster>() == null && hit.collider.GetComponentInParent<BossBase>() == null
                && !hit.collider.CompareTag("Player"))
            {
                Burst(hit.point);
                return;
            }
            transform.position = to;
            if (traveled >= range) Burst(to);
        }

        private void Burst(Vector3 at)
        {
            switch (kind)
            {
                case Kind.Leaf: FireballVfx.SpawnSparks(at, -direction, 10, LeafLight, LeafDark); break;
                case Kind.Feather: FireballVfx.SpawnSparks(at, -direction, 8, FeatherLight, FeatherDark); break;
                default:
                    FireFlash.Spawn(at, 1.6f, 0.35f, 0f);
                    FireballVfx.SpawnSparks(at, -direction, 16);
                    break;
            }
            if (fire != null)
            {
                fire.ReleaseTail();
                Destroy(gameObject);
                return;
            }
            if (trail != null)
            {
                // 尾だけ少し残す。
                trail.transform.SetParent(null, true);
                trail.emitting = false;
                Destroy(trail.gameObject, trail.time + 0.05f);
            }
            Destroy(gameObject);
        }
    }
}
