using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Game.Combat;
using Game.Core;

namespace Game.Monster
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(HealthComponent))]
    public class Monster : MonoBehaviour
    {
        private enum BehaviorState { FollowingPath, ChasingHero }

        [SerializeField] private MonsterData data;
        [SerializeField] private string targetTag = "StartTown";
        [SerializeField] private string heroTag = "Player";
        [SerializeField] private float arriveDistance = 1.0f;
        [SerializeField] private float waypointReachDistance = 1.5f;
        [SerializeField] private int arriveDamage = 10;

        [Header("Aggro")]
        [SerializeField] private float aggroRadius = 10f;
        [SerializeField] private float deaggroRadius = 10f;
        [SerializeField] private float heroLookupInterval = 0.5f;
        [SerializeField] private float chaseRefreshInterval = 0.2f;

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

        private readonly Queue<Vector3> remainingWaypoints = new Queue<Vector3>();
        private Vector3 finalTarget;
        private bool finalTargetValid;
        private BehaviorState currentState = BehaviorState.FollowingPath;

        private GameObject heroGO;
        private HealthComponent heroHealth;
        private Transform heroTransform;
        private float heroLookupTimer;
        private float chaseRefreshTimer;

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
                if (data != null) Initialize(data, null, null);
                else Debug.LogWarning($"[Monster] {name} has no data and Initialize() was never called.");
            }
        }

        public void Initialize(MonsterData newData, Spawner spawner)
        {
            Initialize(newData, spawner, null);
        }

        public void Initialize(MonsterData newData, Spawner spawner, IReadOnlyList<Vector3> waypoints)
        {
            data = newData;
            SpawnedBy = spawner;
            initialized = true;
            ApplyData();

            remainingWaypoints.Clear();
            if (waypoints != null)
            {
                foreach (var w in waypoints) remainingWaypoints.Enqueue(w);
            }

            AcquireFinalTarget();
            SetNextDestination();

            VisionRing.Create(transform, aggroRadius, new Color(1f, 1f, 0f, 0.8f));
            VisionRing.Create(transform, deaggroRadius, new Color(1f, 0.5f, 0f, 0.8f));
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

        private void AcquireFinalTarget()
        {
            var target = GameObject.FindWithTag(targetTag);
            if (target == null)
            {
                Debug.LogWarning($"[Monster] target tag '{targetTag}' not found; halting {name}.");
                if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
                finalTargetValid = false;
                return;
            }
            targetTransform = target.transform;
            finalTarget = targetTransform.position;
            finalTargetValid = true;
        }

        private void SetNextDestination()
        {
            if (agent == null || !agent.isOnNavMesh) return;
            Vector3 dest = remainingWaypoints.Count > 0 ? remainingWaypoints.Peek() : (finalTargetValid ? finalTarget : transform.position);
            agent.SetDestination(dest);
        }

        private void Update()
        {
            if (destroyed || agent == null) return;

            RefreshHeroRef();
            EvaluateState();

            if (currentState == BehaviorState.ChasingHero)
            {
                chaseRefreshTimer -= Time.deltaTime;
                if (chaseRefreshTimer <= 0f && heroTransform != null && agent.isOnNavMesh)
                {
                    agent.SetDestination(heroTransform.position);
                    chaseRefreshTimer = chaseRefreshInterval;
                }
                return;
            }

            if (agent.pathPending) return;
            if (!agent.hasPath) return;
            if (agent.remainingDistance < arriveDistance)
            {
                if (remainingWaypoints.Count > 0)
                {
                    remainingWaypoints.Dequeue();
                    SetNextDestination();
                    return;
                }
                if (finalTargetValid && agent.remainingDistance < arriveDistance) OnReachTarget();
            }
            else if (remainingWaypoints.Count > 0 && agent.remainingDistance < waypointReachDistance)
            {
                remainingWaypoints.Dequeue();
                SetNextDestination();
            }
        }

        private void RefreshHeroRef()
        {
            heroLookupTimer -= Time.deltaTime;
            if (heroGO != null && !heroGO.activeInHierarchy) heroGO = null;
            if (heroGO != null) return;
            if (heroLookupTimer > 0f) return;
            heroLookupTimer = heroLookupInterval;
            heroGO = GameObject.FindWithTag(heroTag);
            if (heroGO != null)
            {
                heroTransform = heroGO.transform;
                heroHealth = heroGO.GetComponent<HealthComponent>();
            }
            else
            {
                heroTransform = null;
                heroHealth = null;
            }
        }

        private void EvaluateState()
        {
            bool heroValid = heroTransform != null && (heroHealth == null || !heroHealth.IsDead);

            if (currentState == BehaviorState.FollowingPath)
            {
                if (!heroValid) return;
                float d = Vector3.Distance(transform.position, heroTransform.position);
                if (d <= aggroRadius)
                {
                    currentState = BehaviorState.ChasingHero;
                    chaseRefreshTimer = 0f;
                    Debug.Log($"[Monster] {name} aggro on Hero (d={d:F1})");
                }
                return;
            }

            // ChasingHero
            if (!heroValid)
            {
                ResumePath("hero invalid");
                return;
            }
            float dist = Vector3.Distance(transform.position, heroTransform.position);
            if (dist > deaggroRadius)
            {
                ResumePath($"deaggro distance={dist:F1}");
            }
        }

        private void ResumePath(string reason)
        {
            currentState = BehaviorState.FollowingPath;
            SetNextDestination();
            Debug.Log($"[Monster] {name} deaggro, resuming path ({reason})");
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
