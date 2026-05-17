using UnityEngine;
using Game.Combat;

namespace Game.Monster
{
    [RequireComponent(typeof(HealthComponent))]
    public class DummyEnemy : MonoBehaviour
    {
        private HealthComponent health;

        private void Awake()
        {
            health = GetComponent<HealthComponent>();
        }

        private void OnEnable()
        {
            if (health != null) health.OnDeath += OnDied;
        }

        private void OnDisable()
        {
            if (health != null) health.OnDeath -= OnDied;
        }

        private void OnDied()
        {
            Destroy(gameObject);
        }
    }
}
