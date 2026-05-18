using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Game.UI;

namespace Game.Core
{
    public class MatchItemGimmick : MonoBehaviour
    {
        public static MatchItemGimmick Instance { get; private set; }

        [Header("Prefab")]
        [SerializeField] private GameObject powerItemPrefab;

        [Header("Timing")]
        [SerializeField] private float spawnTimeFromMatchStart = 600f;
        [SerializeField] private float debugSpawnTime = 30f;
        [SerializeField] private bool useDebugTime = false;

        [Header("Placement")]
        [SerializeField] private Vector2 mapXRange = new Vector2(-90f, 90f);
        [SerializeField] private Vector2 mapZRange = new Vector2(-50f, 50f);
        [SerializeField] private float exclusionRadius = 10f;
        [SerializeField] private int maxRetry = 10;

        private bool armed;
        private bool spawned;
        private float elapsedInMatch;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (MatchManager.Instance != null) MatchManager.Instance.OnStateChanged += OnStateChanged;
        }

        private void OnStateChanged(MatchManager.MatchState s)
        {
            if (s == MatchManager.MatchState.InMatch)
            {
                armed = true;
                elapsedInMatch = 0f;
                spawned = false;
            }
            else if (s == MatchManager.MatchState.Finished)
            {
                armed = false;
            }
        }

        private void Update()
        {
            if (!armed || spawned) return;
            elapsedInMatch += Time.deltaTime;
            float target = useDebugTime ? debugSpawnTime : spawnTimeFromMatchStart;
            if (elapsedInMatch >= target)
            {
                SpawnPowerItemNow();
                spawned = true;
            }
        }

        public GameObject SpawnPowerItemNow()
        {
            if (powerItemPrefab == null)
            {
                Debug.LogWarning("[MatchItemGimmick] powerItemPrefab not assigned");
                return null;
            }
            Vector3 pos;
            if (!TryPickSpawnPos(out pos))
            {
                Debug.LogWarning("[MatchItemGimmick] Failed to find valid spawn position");
                return null;
            }
            var inst = Instantiate(powerItemPrefab, pos + Vector3.up * 1.0f, Quaternion.identity);
            Debug.Log($"[PowerItem] Spawned at {pos}");
            if (PowerItemNotifierUI.Instance != null) PowerItemNotifierUI.Instance.ShowNotification("強力なアイテム出現！", 3f);
            return inst;
        }

        private bool TryPickSpawnPos(out Vector3 pos)
        {
            for (int i = 0; i < maxRetry; i++)
            {
                float x = Random.Range(mapXRange.x, mapXRange.y);
                float z = Random.Range(mapZRange.x, mapZRange.y);
                Vector3 candidate = new Vector3(x, 0f, z);
                if (NavMesh.SamplePosition(candidate, out var hit, 5f, NavMesh.AllAreas))
                {
                    if (IsFarFromImportant(hit.position))
                    {
                        pos = hit.position;
                        return true;
                    }
                }
            }
            pos = Vector3.zero;
            return false;
        }

        private bool IsFarFromImportant(Vector3 p)
        {
            string[] tags = new string[] { "StartTown", "DemonCastle", "Spawner" };
            foreach (string t in tags)
            {
                foreach (var go in GameObject.FindGameObjectsWithTag(t))
                {
                    if (Vector3.Distance(go.transform.position, p) < exclusionRadius) return false;
                }
            }
            return true;
        }
    }
}
