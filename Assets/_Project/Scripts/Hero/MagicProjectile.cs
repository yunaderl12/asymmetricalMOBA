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

        public void Init(GameObject ownerObj, int dmg, Vector3 direction)
        {
            owner = ownerObj;
            damage = dmg;
            transform.forward = direction.normalized;
        }

        private void Update()
        {
            float step = speed * Time.deltaTime;
            transform.position += transform.forward * step;
            traveled += step;
            if (traveled >= maxDistance) Destroy(gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (owner != null && other.transform.IsChildOf(owner.transform)) return;
            if (other.CompareTag("Player")) return; // skip friendly heroes
            var hp = other.GetComponentInParent<HealthComponent>();
            if (hp != null && !hp.IsDead)
            {
                hp.TakeDamage(damage, owner, DamageType.Magical);
                Destroy(gameObject);
            }
        }
    }
}
