using UnityEngine;
using Game.Combat;

namespace Game.Hero
{
    public class MagicProjectile : MonoBehaviour
    {
        [SerializeField] private float speed = 15f;
        [SerializeField] private float maxDistance = 20f;
        [SerializeField] private int damage = 15;

        private GameObject owner;
        private float traveled;
        private MagicBoltVisual visual;

        public void Init(GameObject ownerObj, int dmg, Vector3 direction)
        {
            owner = ownerObj;
            damage = dmg;
            transform.forward = direction.normalized;
            var body = GetComponent<MeshRenderer>();
            if (body != null) body.enabled = false;
            visual = MagicBoltVisual.Attach(gameObject);
        }

        private void Update()
        {
            float step = speed * Time.deltaTime;
            transform.position += transform.forward * step;
            traveled += step;
            if (traveled >= maxDistance) End(false);
        }

        private void End(bool impact)
        {
            if (visual != null) visual.Release();
            if (impact) MagicBoltVisual.Impact(transform.position, transform.forward);
            Destroy(gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (owner != null && other.transform.IsChildOf(owner.transform)) return;
            if (other.CompareTag("Player")) return; // skip friendly heroes
            var hp = other.GetComponentInParent<HealthComponent>();
            if (hp != null && !hp.IsDead)
            {
                hp.TakeDamage(damage, owner, DamageType.Magical);
                End(true);
            }
            // 壁や地面ではそこで消える。
            else if (hp == null && !other.isTrigger) End(true);
        }
    }
}
