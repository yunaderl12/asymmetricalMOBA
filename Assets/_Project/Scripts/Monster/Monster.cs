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
        [SerializeField] private int arriveDamage = 10;

        public MonsterData Data => data;
        public Spawner SpawnedBy { get; private set; }

        public int CurrentAtk
        {
            get
            {
                if (data == null) return 0;
                float mul = mods != null ? mods.TotalAtkMultiplier : 1f;
                return Mathf.Max(0, Mathf.RoundToInt(data.atk * mul));
            }
        }

        private NavMeshAgent agent;
        private HealthComponent health;
        private StatModifierContainer mods;
        private Transform targetTransform;
        private bool destroyed;
        private bool initialized;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<HealthComponent>();
            mods = GetComponent<StatModifierContainer>();
        }

        private void OnEnable()
        {
            if (health != null) health.OnDeath += OnDied;
            if (mods != null) mods.OnModifiersChanged += OnModsChanged;
        }

        private void OnDisable()
        {
            if (health != null) health.OnDeath -= OnDied;
            if (mods != null) mods.OnModifiersChanged -= OnModsChanged;
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
            ApplySpeed();
            if (health != null) health.SetMaxHP(data.hp);
            var expReward = GetComponent<ExpReward>();
            if (expReward != null) expReward.Exp = data.exp;
        }

        private void ApplySpeed()
        {
            if (data == null || agent == null) return;
            float mul = mods != null ? mods.TotalSpdMultiplier : 1f;
            agent.speed = data.spd * mul;
        }

        private void OnModsChanged()
        {
            ApplySpeed();
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
                OnReachTarget();
            }
        }

        private void OnReachTarget()
        {
            destroyed = true;
            var target = GameObject.FindWithTag(targetTag);
            if (target != null)
            {
                var coreHC = target.GetComponent<HealthComponent>();
                if (coreHC == null) coreHC = target.GetComponentInChildren<HealthComponent>();
                if (coreHC != null) coreHC.TakeDamage(arriveDamage, gameObject);
            }
            Debug.Log($"[Monster] {name} reached {targetTag}, dealt {arriveDamage} dmg");
            Destroy(gameObject);
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
