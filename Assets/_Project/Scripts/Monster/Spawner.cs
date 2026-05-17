using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Game.Combat;

namespace Game.Monster
{
    [RequireComponent(typeof(HealthComponent))]
    public class Spawner : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private SpawnerData data;
        [SerializeField] private List<MonsterData> inv = new List<MonsterData>();

        [Header("Refs")]
        [SerializeField] private Transform attackOrigin;
        [SerializeField] private GameObject monsterPrefab;
        [SerializeField] private MeshRenderer bodyRenderer;
        [SerializeField] private Material destroyedMaterial;
        [SerializeField] private LineRenderer attackLine;
        [SerializeField] private float attackLineDuration = 0.15f;

        [Header("Spawn Location")]
        [SerializeField] private float spawnRadiusMin = 2f;
        [SerializeField] private float spawnRadiusMax = 3f;

        public bool IsFunctional { get; private set; } = true;
        public IReadOnlyList<Monster> SpawnedMonsters => spawnedMonsters;

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

            if (inv.Count > data.inv)
            {
                Debug.LogWarning($"[Spawner] {name} inv ({inv.Count}) > data.inv ({data.inv}); truncating.");
                inv = inv.GetRange(0, data.inv);
            }

            StartCoroutine(SpawnRoutine());
            // Attack is driven by Update (frame-accurate range gating) instead of a coroutine.
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
                    hp.TakeDamage(data.atk);
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
            while (IsFunctional)
            {
                yield return new WaitForSeconds(data.spawnInterval);
                if (!IsFunctional) yield break;
                if (inv == null || inv.Count == 0) continue;

                var dataToSpawn = inv[invIndex];
                if (dataToSpawn == null)
                {
                    invIndex = (invIndex + 1) % inv.Count;
                    continue;
                }

                int sameCount = 0;
                foreach (var sm in spawnedMonsters)
                {
                    if (sm != null && sm.Data == dataToSpawn) sameCount++;
                }
                if (sameCount >= dataToSpawn.maxSpwn)
                {
                    continue;
                }

                if (monsterPrefab == null)
                {
                    Debug.LogWarning($"[Spawner] {name} monsterPrefab not set.");
                    continue;
                }

                var spawnPos = SampleSpawnPosition();
                if (!spawnPos.HasValue)
                {
                    continue;
                }

                var monsterGO = Instantiate(monsterPrefab, spawnPos.Value, Quaternion.identity);
                var monsterComp = monsterGO.GetComponent<Monster>();
                if (monsterComp != null)
                {
                    monsterComp.Initialize(dataToSpawn, this);
                    spawnedMonsters.Add(monsterComp);
                }
                invIndex = (invIndex + 1) % inv.Count;
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
