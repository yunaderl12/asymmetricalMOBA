using UnityEngine;
using UnityEngine.AI;
using Game.Combat;

namespace Game.Monster
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(HealthComponent))]
    public class Monster : MonoBehaviour
    {
        [SerializeField] private MonsterData data;
        [SerializeField] private string targetTag = "StartTown";
        [SerializeField] private float arriveDistance = 1.0f;

        public MonsterData Data => data;
        public Spawner SpawnedBy { get; private set; }

        private NavMeshAgent agent;
        private HealthComponent health;
        private Transform targetTransform;
        private bool destroyed;
        private bool initialized;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
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

        private void Start()
        {
            if (!initialized)
            {
                if (data != null) Initialize(data, null);
                else Debug.LogWarning($"[Monster] {name} has no data and Initialize() was never called.");
            }
        }

        public void Initialize(MonsterData newData, Spawner spawner)
        {
            data = newData;
            SpawnedBy = spawner;
            initialized = true;
            ApplyData();
            AcquireTarget();
        }

        private void ApplyData()
        {
            if (data == null) return;
            if (agent != null) agent.speed = data.spd;
            if (health != null) health.SetMaxHP(data.hp);
        }

        private void AcquireTarget()
        {
            var target = GameObject.FindWithTag(targetTag);
            if (target == null)
            {
                Debug.LogWarning($"[Monster] target tag '{targetTag}' not found; halting {name}.");
                if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
                return;
            }
            targetTransform = target.transform;
            if (agent != null && agent.isOnNavMesh)
            {
                agent.SetDestination(targetTransform.position);
            }
            else
            {
                Debug.LogWarning($"[Monster] {name} not on NavMesh; cannot SetDestination.");
            }
        }

        private void Update()
        {
            if (destroyed || agent == null) return;
            if (agent.pathPending) return;
            if (!agent.hasPath) return;
            if (agent.remainingDistance < arriveDistance)
            {
                Debug.Log($"[Monster] {name} reached StartTown");
                destroyed = true;
                Destroy(gameObject);
            }
        }

        private void OnDied()
        {
            destroyed = true;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (SpawnedBy != null) SpawnedBy.OnSpawnedMonsterDestroyed(this);
        }
    }
}
