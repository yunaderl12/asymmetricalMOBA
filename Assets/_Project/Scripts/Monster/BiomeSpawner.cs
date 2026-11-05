using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using Game.Core;

namespace Game.Monster
{
    /// 森・山・砂漠で魔物を自然に湧かせる。魔物ごとにバイオーム内の最大数まで、勇者から離れた場所に湧く。塔とは独立している。
    public class BiomeSpawner : MonoBehaviour
    {
        private const float RaycastTop = 200f;
        private const float NavMeshSnapDistance = 2f;
        private const float TownGateSnapDistance = 8f;
        private const float RetrySeconds = 0.5f;

        [SerializeField] private BiomeSpawnData table;
        [SerializeField] private GameObject monsterPrefab;

        private class Slot
        {
            public StageMap.Biome biome;
            public MonsterData monster;
            public int pending;
            public float nextTime;
            public readonly List<Monster> alive = new List<Monster>();
        }

        private readonly List<Slot> slots = new List<Slot>();

        private void Awake()
        {
            if (table == null || monsterPrefab == null)
            {
                Debug.LogWarning("[BiomeSpawner] table or monsterPrefab is not set.");
                enabled = false;
                return;
            }
            foreach (var entry in table.entries)
                foreach (var monster in entry.monsters)
                    if (monster != null) slots.Add(new Slot { biome = entry.biome, monster = monster });
        }

        private void Update()
        {
            if (MatchManager.Instance != null && MatchManager.Instance.CurrentState != MatchManager.MatchState.InMatch) return;
            foreach (var slot in slots)
            {
                if (Time.time < slot.nextTime || !slot.monster.IsUnlockedAt(DemonKingLevel.Current)) continue;
                slot.alive.RemoveAll(m => m == null);
                if (slot.alive.Count + slot.pending >= slot.monster.maxSpwn) continue;
                Vector3 position;
                if (!TryFindPlace(slot.biome, out position))
                {
                    slot.nextTime = Time.time + RetrySeconds;
                    continue;
                }
                BeginSpawn(slot, position);
                slot.nextTime = Time.time + Mathf.Max(0.05f, slot.monster.spwnCool);
            }
        }

        private void BeginSpawn(Slot slot, Vector3 position)
        {
            var config = BalanceConfig.Instance;
            slot.pending++;
            SpawnPortal.Begin(position, config.spawnPortalRadius, () =>
            {
                slot.pending--;
                var go = Instantiate(monsterPrefab, position, Quaternion.identity);
                var monster = go.GetComponent<Monster>();
                if (monster == null) return;
                monster.Initialize(slot.monster, null, null);
                slot.alive.Add(monster);
                MonsterEmerge.Begin(monster, config.spawnEmergeSeconds, slot.monster.modelScale);
            });
        }

        private static bool TryFindPlace(StageMap.Biome biome, out Vector3 position)
        {
            var config = BalanceConfig.Instance;
            var heroes = GameObject.FindGameObjectsWithTag("Player");
            for (int i = 0; i < config.biomeSpawnAttempts; i++)
            {
                Vector3 point = StageMap.RandomPointIn(biome);
                if (StageMap.BiomeAt(point) != biome || StageMap.DistanceToWater(point) < config.biomeSpawnMinWaterDistance) continue;
                RaycastHit ground;
                if (!Physics.Raycast(point + Vector3.up * RaycastTop, Vector3.down, out ground, RaycastTop * 2f, ~0, QueryTriggerInteraction.Ignore)) continue;
                NavMeshHit hit;
                if (!NavMesh.SamplePosition(ground.point, out hit, NavMeshSnapDistance, NavMesh.AllAreas)) continue;
                if (IsNearHero(hit.position, heroes, config.biomeSpawnMinHeroDistance) || !CanWalkToTown(hit.position)) continue;
                position = hit.position;
                return true;
            }
            position = Vector3.zero;
            return false;
        }

        private static bool IsNearHero(Vector3 point, GameObject[] heroes, float distance)
        {
            foreach (var hero in heroes)
                if (hero != null && Vector3.Distance(point, hero.transform.position) < distance) return true;
            return false;
        }

        // 山の上などの孤立した足場には湧かせない。
        private static bool CanWalkToTown(Vector3 point)
        {
            var path = new NavMeshPath();
            var gate = new Vector3(StageMap.Map.townGateX, 0f, StageMap.Map.townGateZ);
            NavMeshHit gateHit;
            if (!NavMesh.SamplePosition(gate, out gateHit, TownGateSnapDistance, NavMesh.AllAreas)) return false;
            return NavMesh.CalculatePath(point, gateHit.position, NavMesh.AllAreas, path)
                && path.status == NavMeshPathStatus.PathComplete;
        }

        public string Describe()
        {
            var text = new StringBuilder();
            foreach (var slot in slots)
            {
                slot.alive.RemoveAll(m => m == null);
                text.AppendLine(slot.biome + " " + slot.monster.monsterName + " " + slot.alive.Count + "+" + slot.pending + "/" + slot.monster.maxSpwn + (slot.monster.IsUnlockedAt(DemonKingLevel.Current) ? "" : " (locked Lv" + slot.monster.unlockDkLevel + ")"));
            }
            return text.ToString();
        }
    }
}
