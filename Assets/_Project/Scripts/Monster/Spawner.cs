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
        [SerializeField] private SpawnerTowerVisual towerVisual;

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
        // 出現演出中でまだ生成していない魔物。上限の判定に含める。
        private readonly List<MonsterData> pendingSpawns = new List<MonsterData>();
        private readonly List<SpawnPortal> activePortals = new List<SpawnPortal>();
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
        private bool charging;
        private float chargeTimer;

        private void Update()
        {
            if (!IsFunctional || data == null) return;
            if (attackCooldownTimer > 0f) attackCooldownTimer -= Time.deltaTime;
            if (charging)
            {
                UpdateCharge();
                return;
            }
            if (attackCooldownTimer > 0f) return;
            if (FindClosestPlayerInRange() == null) return;

            charging = true;
            chargeTimer = 0f;
            attackCooldownTimer = data.cool;
            if (towerVisual != null) towerVisual.SetCharge(0f);
        }

        // 溜め終わった時点で一番近い勇者を狙う。範囲から誰もいなくなっていたら撃たない。
        private void UpdateCharge()
        {
            chargeTimer += Time.deltaTime;
            float duration = Mathf.Max(0.01f, data.attackChargeSeconds);
            if (towerVisual != null) towerVisual.SetCharge(chargeTimer / duration);
            if (chargeTimer < duration) return;

            charging = false;
            var target = FindClosestPlayerInRange();
            if (target == null)
            {
                if (towerVisual != null) towerVisual.Cancel();
                return;
            }
            if (towerVisual != null) towerVisual.Release();
            Shoot(target);
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

        private void Shoot(GameObject target)
        {
            if (attackOrigin == null) return;
            var origin = attackOrigin.position;
            var dir = (target.transform.position + Vector3.up * 1.5f - origin).normalized;
            Debug.DrawRay(origin, dir * data.attackRange, Color.yellow, 0.2f);

            Vector3 endPoint = origin + dir * data.attackRange;
            if (TryFindLaserHit(origin, dir, out var hit))
            {
                endPoint = hit.point;
                var hp = hit.collider.GetComponentInParent<HealthComponent>();
                if (hp != null && hp.gameObject == target)
                {
                    hp.TakeDamage(data.atk, gameObject, DamageType.Physical);
                }
            }
            SpawnerLaser.Spawn(origin, endPoint);
        }

        // 高い水晶から撃つので、自分の塔と味方の魔物はすり抜ける。
        private bool TryFindLaserHit(Vector3 origin, Vector3 dir, out RaycastHit nearest)
        {
            nearest = default;
            float best = float.MaxValue;
            foreach (var hit in Physics.RaycastAll(origin, dir, data.attackRange))
            {
                if (hit.distance >= best || hit.transform.IsChildOf(transform)) continue;
                if (hit.collider.GetComponentInParent<Monster>() != null) continue;
                best = hit.distance;
                nearest = hit;
            }
            return best < float.MaxValue;
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
                aliveCount += pendingSpawns.Count;
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
                foreach (var pending in pendingSpawns)
                {
                    if (pending == dataToSpawn) sameCount++;
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

                BeginSpawn(dataToSpawn, spawnPos.Value);

                // Spawn interval comes only from the monster's own SpwnCool (counted from the warning circle).
                float perMonsterCool = Mathf.Max(0.05f, dataToSpawn.spwnCool);
                Debug.Log($"[Spawner] {name} spawned {dataToSpawn.monsterName}, next in {perMonsterCool:F1}s");

                invIndex = (invIndex + 1) % inv.Count;
                yield return new WaitForSeconds(perMonsterCool);
            }
        }

        /// 警告の円とポータルを出し、ポータルが開いたら魔物を生成して這い出させる。
        private void BeginSpawn(MonsterData dataToSpawn, Vector3 position)
        {
            var cfg = BalanceConfig.Instance;
            activePortals.RemoveAll(p => p == null);
            pendingSpawns.Add(dataToSpawn);
            activePortals.Add(SpawnPortal.Begin(position, cfg.spawnPortalRadius, () =>
            {
                pendingSpawns.Remove(dataToSpawn);
                var monsterGO = Instantiate(monsterPrefab, position, Quaternion.identity);
                var monsterComp = monsterGO.GetComponent<Monster>();
                if (monsterComp == null) return;
                monsterComp.Initialize(dataToSpawn, this, waypointsToStartTown);
                spawnedMonsters.Add(monsterComp);
                MonsterEmerge.Begin(monsterComp, cfg.spawnEmergeSeconds, dataToSpawn.modelScale);
            }));
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
            charging = false;
            if (towerVisual != null) towerVisual.SetDestroyed();
            // まだ魔物が出ていないポータルは閉じる。
            foreach (var portal in activePortals)
            {
                if (portal != null) portal.Cancel();
            }
            activePortals.Clear();
            pendingSpawns.Clear();
            Debug.Log($"[Spawner] {name} destroyed. spawnedMonsters surviving: {spawnedMonsters.Count}");
        }
    }
}
