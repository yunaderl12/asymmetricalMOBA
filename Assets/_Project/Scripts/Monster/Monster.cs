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
        private enum BehaviorState { Wandering, FollowingPath, ChasingHero }

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
        /// 橋の番人のように、別のコンポーネントが動かす魔物。うろつきや進軍はしない。
        public bool ExternallyControlled { get; set; }
        /// ポータルから這い出している間は何もしない（MonsterEmerge が管理する）。
        public bool IsEmerging { get; set; }
        /// 攻撃を受けた直後のごく短いのけぞり。スタンとは別物で、スタン耐性を作らない。
        public bool IsFlinching => hitReaction != null && hitReaction.IsFlinching;

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
        private MonsterHitReaction hitReaction;
        private TerrainWalker terrain;
        private Vector3 wanderCenter;
        private float wanderWait;

        private bool UsesHate => hate != null && ((data != null && data.useHate) || MonsterHate.ForceAll);

        private void Awake()
        {
            gameObject.AddComponent<Game.Core.GroundFollower>();
            terrain = TerrainWalker.For(gameObject);
            terrain.SandSlows = false; // 砂漠に住んでいるので砂地でも遅くならない
            terrain.FactorChanged += ApplySpeed;
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<HealthComponent>();
            mods = GetComponent<StatModifierContainer>();
            hate = GetComponent<MonsterHate>();
            if (hate == null) hate = gameObject.AddComponent<MonsterHate>();
            hitReaction = gameObject.AddComponent<MonsterHitReaction>();
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
            HealthComponent.AnyDamage += OnAnyDamage;
            if (mods != null) mods.OnModifiersChanged += OnModsChanged;
        }

        private void OnDisable()
        {
            if (health != null) health.OnDeath -= OnDied;
            HealthComponent.AnyDamage -= OnAnyDamage;
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
            MonsterBehaviour.Attach(this);

            remainingWaypoints.Clear();
            if (waypoints != null)
            {
                foreach (var w in waypoints) remainingWaypoints.Enqueue(w);
            }

            AcquireFinalTarget();
            wanderCenter = spawner != null ? spawner.transform.position : transform.position;
            if (DemonKingLevel.IsFinalPhase)
            {
                currentState = BehaviorState.FollowingPath;
                SetNextDestination();
            }
            else currentState = BehaviorState.Wandering;

            VisionRing.Create(transform, aggroRadius, new Color(1f, 1f, 0f, 0.8f));
            VisionRing.Create(transform, deaggroRadius, new Color(1f, 0.5f, 0f, 0.8f));
        }

        private void ApplyData()
        {
            if (data == null) return;
            levelMultiplier = DemonKingLevel.MonsterStatMultiplier(DemonKingLevel.Current);
            if (data.noticeRadius > 0f)
            {
                aggroRadius = data.noticeRadius;
                deaggroRadius = Mathf.Max(deaggroRadius, data.noticeRadius * 1.3f);
            }
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
            anim.NominalSpeed = data.spd;
            anim.ConfigureDeath(data.deathFallSeconds, data.deathHoldSeconds, data.deathParticleCount);
            anim.ConfigureBlastDeath(data.deathBlastSeconds, data.deathBlastSpeed);
            if (data.customRig) anim.UseCustomRig();
            else anim.EnableIdleLife();
        }

        private void ApplySpeed()
        {
            if (data == null || agent == null) return;
            float mul = mods != null ? mods.TotalSpdMultiplier : 1f;
            agent.speed = data.spd * mul * speedScale * (terrain != null ? terrain.SpeedFactor : 1f);
        }

        private float speedScale = 1f;

        /// 技の部品が足の速さを変える（ミイラのよろめき歩きなど）。1 = MonsterData.spd のまま。
        public void SetSpeedScale(float scale)
        {
            if (Mathf.Approximately(scale, speedScale)) return;
            speedScale = scale;
            ApplySpeed();
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

        private float alertFaceRemaining;

        private void FaceAlertTarget()
        {
            if (alertFaceRemaining <= 0f) return;
            alertFaceRemaining -= Time.deltaTime;
            bool facing = alertFaceRemaining > 0f && heroTransform != null;
            agent.updateRotation = !facing;
            if (!facing) return;
            Vector3 to = heroTransform.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), BalanceConfig.Instance.monsterAlertTurnSpeed * Time.deltaTime);
        }

        private void Update()
        {
            if (destroyed || agent == null) return;
            if (IsEmerging || ExternallyControlled)
            {
                // ほかの処理が魔物を動かすあいだは、向き直りをやめて向きを NavMeshAgent に返す。
                if (alertFaceRemaining > 0f) { alertFaceRemaining = 0f; agent.updateRotation = true; }
                return;
            }
            FaceAlertTarget();

            if (StatusEffectController.IsStunnedObj(gameObject) || IsFlinching)
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

            if (currentState == BehaviorState.Wandering)
            {
                if (DemonKingLevel.IsFinalPhase)
                {
                    currentState = BehaviorState.FollowingPath;
                    SetNextDestination();
                    return;
                }
                TickWander();
                return;
            }

            if (currentState == BehaviorState.ChasingHero)
            {
                TickChase();
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

        /// 勇者の中心からこの距離で止まる。近接の魔物は体が触れない程度、遠距離の魔物は MonsterData.keepDistance。
        public float KeepDistance
        {
            get
            {
                if (data != null && data.keepDistance > 0f) return data.keepDistance;
                float body = agent != null ? agent.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z) : 0.5f;
                return body + HeroBodyRadius + BalanceConfig.Instance.monsterKeepGap;
            }
        }

        /// 勇者がこれより近づいてきたら下がる。
        private float RetreatDistance => data != null && data.retreatDistance > 0f ? data.retreatDistance : KeepDistance * 0.8f;

        /// 追っている勇者（いなければ null）。攻撃の部品が使う。
        public Transform ChaseTarget => currentState == BehaviorState.ChasingHero ? heroTransform : null;

        /// 攻撃の部品が動きを止めたいとき（溜め・突進など）に true にする。
        public bool HoldMovement { get; set; }

        private const float HeroBodyRadius = 0.5f;

        // 勇者に重ならないよう、KeepDistance で止まって向き合い、入り込まれたら少し下がる。
        private void TickChase()
        {
            if (heroTransform == null || !agent.isOnNavMesh) return;
            atTarget = false;
            Vector3 to = heroTransform.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            float keep = KeepDistance;

            if (HoldMovement)
            {
                agent.isStopped = true;
                return;
            }
            if (dist < RetreatDistance && dist > 0.01f)
            {
                agent.isStopped = true;
                agent.ResetPath();
                Vector3 away = -to / dist;
                agent.Move(away * agent.speed * 0.6f * Time.deltaTime);
                Face(to);
                return;
            }
            if (dist <= keep)
            {
                agent.isStopped = true;
                Face(to);
                return;
            }
            chaseRefreshTimer -= Time.deltaTime;
            if (agent.isStopped || chaseRefreshTimer <= 0f)
            {
                agent.isStopped = false;
                agent.stoppingDistance = keep * 0.95f;
                agent.SetDestination(heroTransform.position);
                chaseRefreshTimer = chaseRefreshInterval;
            }
        }

        private void Face(Vector3 to)
        {
            if (to.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 540f * Time.deltaTime);
        }

        // 最終フェーズまでは、スポナーの周りをうろうろする。
        private void TickWander()
        {
            if (!agent.isOnNavMesh || agent.pathPending) return;
            if (agent.hasPath && agent.remainingDistance > arriveDistance) return;
            wanderWait -= Time.deltaTime;
            if (wanderWait > 0f) return;

            var config = BalanceConfig.Instance;
            wanderWait = Random.Range(config.monsterWanderWaitMin, config.monsterWanderWaitMax);
            Vector2 offset = Random.insideUnitCircle * config.monsterWanderRadius;
            Vector3 point = wanderCenter + new Vector3(offset.x, 0f, offset.y);
            if (NavMesh.SamplePosition(point, out var hit, 4f, NavMesh.AllAreas))
            {
                agent.isStopped = false;
                agent.SetDestination(hit.position);
            }
        }

        // 正面の視野に入っている勇者だけに気づく。
        private bool InSight(Transform target)
        {
            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.01f) return true;
            return Vector3.Angle(transform.forward, toTarget) <= BalanceConfig.Instance.monsterSightDegrees * 0.5f;
        }

        private void OnAnyDamage(HealthComponent target, int amount, GameObject attacker)
        {
            if (target != health || attacker == null || destroyed || ExternallyControlled || currentState == BehaviorState.ChasingHero) return;
            if (!attacker.CompareTag(heroTag)) return;
            heroGO = attacker;
            heroTransform = attacker.transform;
            heroHealth = attacker.GetComponent<HealthComponent>();
            Notice();
        }

        private void Notice()
        {
            currentState = BehaviorState.ChasingHero;
            chaseRefreshTimer = 0f;
            // 「！」が出ているあいだは、見つけた勇者の方へすばやく向き直って、向いたまま追う。
            alertFaceRemaining = BalanceConfig.Instance.monsterAlertFaceSeconds;
            if (agent != null) AlertMark.Show(transform, agent.height + BalanceConfig.Instance.monsterAlertMarkLift);
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
                if (currentState != BehaviorState.ChasingHero && !InSight(go.transform)) continue;
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

            if (currentState != BehaviorState.ChasingHero)
            {
                if (!heroValid) return;
                float d = Vector3.Distance(transform.position, heroTransform.position);
                if (d <= aggroRadius && InSight(heroTransform))
                {
                    Notice();
                    Debug.Log($"[Monster] {name} noticed a hero (d={d:F1})");
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
            HoldMovement = false;
            if (agent.isOnNavMesh) { agent.stoppingDistance = 0f; agent.isStopped = false; }
            if (DemonKingLevel.IsFinalPhase)
            {
                currentState = BehaviorState.FollowingPath;
                SetNextDestination();
            }
            else
            {
                currentState = BehaviorState.Wandering;
                wanderWait = 0f;
                if (agent.isOnNavMesh) agent.SetDestination(wanderCenter);
            }
            Debug.Log($"[Monster] {name} deaggro, resuming ({reason})");
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
