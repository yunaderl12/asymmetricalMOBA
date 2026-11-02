using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Game.Combat;
using Game.Core;
using Game.Hero;

namespace Game.Monster
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(HealthComponent))]
    public class Monster : MonoBehaviour, IDefenseSource
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
        /// ポータルから這い出している間は何もしない（MonsterEmerge が管理する）。
        public bool IsEmerging { get; set; }

        public int CurrentAtk
        {
            get
            {
                if (data == null) return 0;
                float mul = mods != null ? mods.TotalAtkMultiplier : 1f;
                return Mathf.Max(0, Mathf.RoundToInt(data.atk * levelMultiplier * mul));
            }
        }

        /// HP / Atk multiplier from the Demon King level at spawn time.
        public float LevelMultiplier => levelMultiplier;
        private float levelMultiplier = 1f;
        private bool wasStunned;

        public int Df => data != null ? Mathf.RoundToInt(data.df * (mods != null ? mods.TotalDfMultiplier : 1f)) : 0;
        public int MDf => data != null ? data.mDf : 0;

        private NavMeshAgent agent;
        private HealthComponent health;
        private StatModifierContainer mods;
        private Transform targetTransform;
        private const float DeathDestroyMargin = 0.1f;

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
        private MonsterHate hate;

        private bool UsesHate => hate != null && ((data != null && data.useHate) || MonsterHate.ForceAll);

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<HealthComponent>();
            mods = GetComponent<StatModifierContainer>();
            hate = GetComponent<MonsterHate>();
            if (hate == null) hate = gameObject.AddComponent<MonsterHate>();
            // Trigger events need a Rigidbody on one side. The town core has none, so without this
            // ContactDamage never hit it (heroes worked only because CharacterController counts).
            if (GetComponent<Rigidbody>() == null)
            {
                var rb = gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
            }
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
            AttachModel();

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
            levelMultiplier = DemonKingLevel.MonsterStatMultiplier(DemonKingLevel.Current);
            ApplySpeed();
            if (health != null) health.SetMaxHP(Mathf.RoundToInt(data.hp * levelMultiplier));
            var expReward = GetComponent<ExpReward>();
            if (expReward != null) expReward.Exp = data.exp;
        }

        /// Replaces the prefab's Body shape with MonsterData.modelPath; run / attack clips come from the shared Knight controller.
        private void AttachModel()
        {
            if (data == null || string.IsNullOrEmpty(data.modelPath) || GetComponent<CharacterModelAnimator>() != null) return;
            var anim = gameObject.AddComponent<CharacterModelAnimator>();
            anim.Configure(data.modelPath, data.modelMaterialPath, data.modelScale, false, false, false);
            anim.ConfigureDeath(data.deathFallSeconds, data.deathHoldSeconds, data.deathParticleCount);
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
            if (destroyed || agent == null || IsEmerging) return;

            if (StatusEffectController.IsStunnedObj(gameObject))
            {
                if (agent.isOnNavMesh) agent.isStopped = true;
                wasStunned = true;
                return;
            }
            if (wasStunned)
            {
                wasStunned = false;
                if (agent.isOnNavMesh && !atTarget) agent.isStopped = false;
            }

            RefreshHeroRef();
            EvaluateState();

            if (currentState == BehaviorState.ChasingHero)
            {
                chaseRefreshTimer -= Time.deltaTime;
                if (chaseRefreshTimer <= 0f && heroTransform != null && agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                    atTarget = false;
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
            if (heroGO != null && heroLookupTimer > 0f) return;
            heroLookupTimer = heroLookupInterval;
            heroGO = SelectHero();
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

        /// Nearest living hero in range. Smart monsters (UsesHate) prefer the hero with the most hate,
        /// and keep their current target unless another hero's hate exceeds it by hateSwitchRatio.
        private GameObject SelectHero()
        {
            float radius = currentState == BehaviorState.ChasingHero ? deaggroRadius : aggroRadius;
            bool useHate = UsesHate;
            GameObject nearest = null, top = null;
            float nearestDist = float.MaxValue, topHate = 0f;
            bool currentInRange = false;

            foreach (var go in GameObject.FindGameObjectsWithTag(heroTag))
            {
                if (!go.activeInHierarchy) continue;
                var hc = go.GetComponent<HealthComponent>();
                if (hc != null && hc.IsDead) continue;
                float d = Vector3.Distance(transform.position, go.transform.position);
                if (d > radius) continue;
                if (go == heroGO) currentInRange = true;
                if (d < nearestDist) { nearestDist = d; nearest = go; }
                if (useHate)
                {
                    float h = hate.Get(go);
                    if (h > topHate) { topHate = h; top = go; }
                }
            }

            if (top == null) return nearest;
            if (currentInRange && top != heroGO
                && topHate < hate.Get(heroGO) * BalanceConfig.Instance.hateSwitchRatio)
                return heroGO;
            return top;
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

        private bool atTarget;

        private void OnReachTarget()
        {
            if (atTarget) return;
            atTarget = true;
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            Debug.Log($"[Monster] {name} reached {targetTag}, holding position");
        }

        private void OnDied()
        {
            destroyed = true;
            if (SpawnedBy != null) SpawnedBy.OnSpawnedMonsterDestroyed(this);

            var modelAnim = GetComponent<CharacterModelAnimator>();
            if (IsEmerging || modelAnim == null || modelAnim.ModelRoot == null)
            {
                Destroy(gameObject);
                return;
            }

            // 倒れて離散するまで残すが、もう動かず、攻撃せず、攻撃も受けない。
            if (agent != null) agent.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            var bar = transform.Find("HealthBar");
            if (bar != null) bar.gameObject.SetActive(false);
            Destroy(gameObject, modelAnim.DeathSequenceSeconds + DeathDestroyMargin);
        }

        private void OnDestroy()
        {
            if (SpawnedBy != null) SpawnedBy.OnSpawnedMonsterDestroyed(this);
        }
    }
}
