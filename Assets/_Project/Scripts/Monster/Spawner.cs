using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Game.Combat;
using Game.Core;

namespace Game.Monster
{
    [RequireComponent(typeof(HealthComponent))]
    public class Spawner : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private SpawnerData data;
        [SerializeField] private List<MonsterData> inv = new List<MonsterData>();
        [SerializeField] private List<int> deckIndices = new List<int>();
        [SerializeField] private List<Vector3> waypointsToStartTown = new List<Vector3>();
        [SerializeField] private int spawnerIndex = -1;

        public IReadOnlyList<Vector3> WaypointsToStartTown => waypointsToStartTown;

        [Header("Refs")]
        [SerializeField] private Transform attackOrigin;
        [SerializeField] private GameObject monsterPrefab;
        [SerializeField] private MeshRenderer bodyRenderer;
        [SerializeField] private Material destroyedMaterial;
        [SerializeField] private LineRenderer attackLine;
        [SerializeField] private float attackLineDuration = 0.15f;

        [Header("Spawn Limit")]
        [SerializeField] private int maxConcurrentMonsters = 2;

        [Header("Spawn Location")]
        [SerializeField] private float spawnRadiusMin = 2f;
        [SerializeField] private float spawnRadiusMax = 3f;

        public bool IsFunctional { get; private set; } = true;
        public IReadOnlyList<Monster> SpawnedMonsters => spawnedMonsters;
        public IReadOnlyList<MonsterData> Inv => inv;
        public SpawnerData Data => data;

        public int CurrentCost
        {
            get
            {
                int sum = 0;
                if (inv != null) foreach (var m in inv) if (m != null) sum += m.cost;
                return sum;
            }
        }

        public int MaxCost
        {
            get
            {
                if (data == null) return 0;
                int dkLevel = DemonKingLevel.Current;
                int growth = Mathf.Max(0, dkLevel - 1) * Mathf.Max(0, data.costPerDkLevel);
                return data.maxCost + growth;
            }
        }

        /// Bird's-eye detection: a working spawner reveals heroes inside its detect range.
        public bool DetectsPosition(Vector3 position)
        {
            if (!IsFunctional || data == null) return false;
            return Vector3.Distance(transform.position, position) <= data.detectRange;
        }

        public bool CanAddMonster(MonsterData m)
        {
            if (m == null || data == null) return false;
            if (!m.IsUnlockedAt(DemonKingLevel.Current)) return false;
            return (MaxCost - CurrentCost) >= m.cost;
        }

        public bool AddToInv(MonsterData m)
        {
            if (!CanAddMonster(m)) { Debug.Log($"[Spawner] {name} cannot add {(m != null ? m.name : "null")}: cost {CurrentCost}/{MaxCost}, required {(m != null ? m.cost : 0)}, unlock Lv{(m != null ? m.unlockDkLevel : 0)} (DK Lv{DemonKingLevel.Current})"); return false; }
            inv.Add(m);
            Debug.Log($"[Spawner] {name} added {m.monsterName} (cost {CurrentCost}/{MaxCost})");
            return true;
        }

        private HealthComponent health;
        private readonly List<Monster> spawnedMonsters = new List<Monster>();
        private int invIndex;

        private void Awake()
        {
            health = GetComponent<HealthComponent>();
        }

        private void OnEnable()
        {
            if (health != null) health.OnDeath += OnDestroyedByDamage;
        }

        private void OnDisable()
        {
            if (health != null) health.OnDeath -= OnDestroyedByDamage;
        }

        private void Start()
        {
            if (data == null)
            {
                Debug.LogWarning($"[Spawner] {name} has no SpawnerData; disabling.");
                IsFunctional = false;
                return;
            }
            if (health != null) health.SetMaxHP(data.hp);
            var expReward = GetComponent<ExpReward>();
            if (expReward != null) expReward.Exp = data.exp;

            Game.Core.VisionRing.Create(transform, data.detectRange, new Color(0f, 1f, 1f, 0.8f));
            Game.Core.VisionRing.Create(transform, data.attackRange, new Color(1f, 0f, 0.3f, 0.8f));

            TryResolveInvFromDeck();

            int dkLevel = DemonKingLevel.Current;
            int lockedCount = inv.RemoveAll(m => m != null && !m.IsUnlockedAt(dkLevel));
            if (lockedCount > 0) Debug.LogWarning($"[Spawner] {name} dropped {lockedCount} monster(s) not yet unlocked at DK Lv{dkLevel}.");

            if (inv.Count > data.inv)
            {
                Debug.LogWarning($"[Spawner] {name} inv ({inv.Count}) > data.inv ({data.inv}); truncating.");
                inv = inv.GetRange(0, data.inv);
            }

            StartCoroutine(SpawnRoutine());
            // Attack is driven by Update (frame-accurate range gating) instead of a coroutine.
        }

        private void TryResolveInvFromDeck()
        {
            // Priority 1: GameSession.SpawnerInitialMobs[spawnerIndex]
            if (spawnerIndex >= 0 && Game.Core.GameSession.Instance != null)
            {
                var list = Game.Core.GameSession.Instance.SpawnerInitialMobs;
                if (list != null && spawnerIndex < list.Count && list[spawnerIndex] != null)
                {
                    inv = new List<MonsterData> { list[spawnerIndex] };
                    Debug.Log($"[Spawner] {name} inv resolved from GameSession.SpawnerInitialMobs[{spawnerIndex}]: {list[spawnerIndex].monsterName}");
                    return;
                }
            }

            // Priority 2: deckIndices via DemonKingDeck
            if (deckIndices == null || deckIndices.Count == 0) return;
            var dkGO = GameObject.FindWithTag("DemonKing");
            if (dkGO == null)
            {
                Debug.Log($"[Spawner] {name} has deckIndices but no DemonKing tag found; using Inspector inv fallback.");
                return;
            }
            var deck = dkGO.GetComponent<Game.DemonKing.DemonKingDeck>();
            if (deck == null)
            {
                Debug.Log($"[Spawner] {name} DemonKing has no DemonKingDeck; using Inspector inv fallback.");
                return;
            }
            var resolved = new List<MonsterData>();
            foreach (int i in deckIndices)
            {
                var md = deck.GetByIndex(i);
                if (md != null) resolved.Add(md);
            }
            if (resolved.Count > 0)
            {
                inv = resolved;
                Debug.Log($"[Spawner] {name} inv resolved from DemonKingDeck: {resolved.Count} entries.");
            }
        }

        private float attackCooldownTimer;

        private void Update()
        {
            if (!IsFunctional || data == null) return;
            if (attackCooldownTimer > 0f) attackCooldownTimer -= Time.deltaTime;
            if (attackCooldownTimer > 0f) return;

            var target = FindClosestPlayerInRange();
            if (target == null) return;

            Shoot(target);
            attackCooldownTimer = data.cool;
        }

        private GameObject FindClosestPlayerInRange()
        {
            GameObject closest = null;
            float closestDist = float.MaxValue;
            foreach (var p in GameObject.FindGameObjectsWithTag("Player"))
            {
                if (p == null) continue;
                float d = Vector3.Distance(transform.position, p.transform.position);
                if (d < closestDist) { closestDist = d; closest = p; }
            }
            if (closest == null) return null;
            if (closestDist > data.detectRange) return null;
            if (closestDist > data.attackRange) return null;
            return closest;
        }

        private Coroutine activeAttackLineRoutine;

        private void Shoot(GameObject target)
        {
            if (attackOrigin == null) return;
            var origin = attackOrigin.position;
            var dir = (target.transform.position + Vector3.up * 1.5f - origin).normalized;
            Debug.DrawRay(origin, dir * data.attackRange, Color.yellow, 0.2f);

            Vector3 endPoint = origin + dir * data.attackRange;
            if (Physics.Raycast(origin, dir, out var hit, data.attackRange))
            {
                endPoint = hit.point;
                var hp = hit.collider.GetComponentInParent<HealthComponent>();
                if (hp != null && hp.gameObject == target)
                {
                    hp.TakeDamage(data.atk, gameObject, DamageType.Physical);
                }
            }

            if (attackLine != null)
            {
                attackLine.SetPosition(0, origin);
                attackLine.SetPosition(1, endPoint);
                attackLine.enabled = true;
                if (activeAttackLineRoutine != null) StopCoroutine(activeAttackLineRoutine);
                activeAttackLineRoutine = StartCoroutine(HideAttackLineAfter(attackLineDuration));
            }
        }

        private IEnumerator HideAttackLineAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (attackLine != null) attackLine.enabled = false;
            activeAttackLineRoutine = null;
        }

        public void OnSpawnedMonsterDestroyed(Monster m)
        {
            spawnedMonsters.Remove(m);
        }

        private IEnumerator SpawnRoutine()
        {
            // Short retry delay used when we can't actually spawn (no inv, cap reached, etc.)
            float retryDelay = 0.5f;

            while (IsFunctional)
            {
                // Don't spawn until the match has actually started.
                if (MatchManager.Instance != null && MatchManager.Instance.CurrentState != MatchManager.MatchState.InMatch)
                {
                    yield return new WaitForSeconds(retryDelay);
                    continue;
                }
                if (inv == null || inv.Count == 0)
                {
                    yield return new WaitForSeconds(retryDelay);
                    continue;
                }

                var dataToSpawn = inv[invIndex];
                if (dataToSpawn == null)
                {
                    invIndex = (invIndex + 1) % inv.Count;
                    yield return null;
                    continue;
                }

                int aliveCount = 0;
                foreach (var sm in spawnedMonsters) if (sm != null) aliveCount++;
                if (maxConcurrentMonsters > 0 && aliveCount >= maxConcurrentMonsters)
                {
                    yield return new WaitForSeconds(retryDelay);
                    continue;
                }

                int sameCount = 0;
                foreach (var sm in spawnedMonsters)
                {
                    if (sm != null && sm.Data == dataToSpawn) sameCount++;
                }
                if (sameCount >= dataToSpawn.maxSpwn)
                {
                    // can't spawn more of this one right now; advance roster
                    invIndex = (invIndex + 1) % inv.Count;
                    yield return new WaitForSeconds(retryDelay);
                    continue;
                }

                if (monsterPrefab == null)
                {
                    Debug.LogWarning($"[Spawner] {name} monsterPrefab not set.");
                    yield return new WaitForSeconds(retryDelay);
                    continue;
                }

                var spawnPos = SampleSpawnPosition();
                if (!spawnPos.HasValue)
                {
                    yield return new WaitForSeconds(retryDelay);
                    continue;
                }

                var monsterGO = Instantiate(monsterPrefab, spawnPos.Value, Quaternion.identity);
                var monsterComp = monsterGO.GetComponent<Monster>();
                if (monsterComp != null)
                {
                    monsterComp.Initialize(dataToSpawn, this, waypointsToStartTown);
                    spawnedMonsters.Add(monsterComp);
                }

                // Spawn interval comes only from the monster's own SpwnCool.
                float perMonsterCool = Mathf.Max(0.05f, dataToSpawn.spwnCool);
                Debug.Log($"[Spawner] {name} spawned {dataToSpawn.monsterName}, next in {perMonsterCool:F1}s");

                invIndex = (invIndex + 1) % inv.Count;
                yield return new WaitForSeconds(perMonsterCool);
            }
        }

        private Vector3? SampleSpawnPosition()
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                var radius = Random.Range(spawnRadiusMin, spawnRadiusMax);
                var angle = Random.Range(0f, Mathf.PI * 2f);
                var candidate = transform.position + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                if (NavMesh.SamplePosition(candidate, out var hit, 2f, NavMesh.AllAreas))
                {
                    return hit.position;
                }
            }
            Debug.LogWarning($"[Spawner] {name} failed to find NavMesh position for spawn.");
            return null;
        }

        private void OnDestroyedByDamage()
        {
            IsFunctional = false;
            StopAllCoroutines();
            activeAttackLineRoutine = null;
            if (attackLine != null) attackLine.enabled = false;
            if (bodyRenderer != null && destroyedMaterial != null)
            {
                bodyRenderer.material = destroyedMaterial;
            }
            Debug.Log($"[Spawner] {name} destroyed. spawnedMonsters surviving: {spawnedMonsters.Count}");
        }
    }
}
