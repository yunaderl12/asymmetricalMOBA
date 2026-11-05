using UnityEngine;
using UnityEngine.AI;
using Game.Core;

namespace Game.Monster
{
    /// 試合の始めに、各橋の前へ番人を1体ずつ立てる。倒されても戻らない。
    public class BridgeGuardianSpawner : MonoBehaviour
    {
        private const float PostSnapDistance = 10f;

        [SerializeField] private MonsterData defaultGuardian;
        [SerializeField] private GameObject monsterPrefab;
        [Tooltip("Distance from the foot of the bridge to where the guardian stands (m).")]
        [SerializeField] private float postDistance = 10f;

        private void Start()
        {
            if (monsterPrefab == null) { Debug.LogWarning("[BridgeGuardianSpawner] monsterPrefab is not set."); return; }
            var starts = StageMap.Map.bridgeStarts;
            var castle = new Vector3(StageMap.Map.castleX, 0f, StageMap.Map.castleZ);
            for (int i = 0; i < GameSession.BRIDGE_COUNT && i * 2 + 1 < starts.Length; i++)
            {
                var data = GameSession.Instance != null ? GameSession.Instance.BridgeGuardians[i] : null;
                if (data == null) data = defaultGuardian;
                if (data == null) continue;
                var foot = new Vector3(starts[i * 2], 0f, starts[i * 2 + 1]);
                Spawn(data, foot, (castle - foot).normalized, i);
            }
        }

        private void Spawn(MonsterData data, Vector3 foot, Vector3 towardCastle, int index)
        {
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(foot - towardCastle * postDistance, out hit, PostSnapDistance, NavMesh.AllAreas))
            {
                Debug.LogWarning("[BridgeGuardianSpawner] no place to stand at bridge " + index);
                return;
            }
            var facing = Quaternion.LookRotation(-towardCastle);
            var go = Instantiate(monsterPrefab, hit.position, facing);
            go.name = "BridgeGuardian_" + index;
            var monster = go.GetComponent<Monster>();
            if (monster == null) return;
            var guardian = go.AddComponent<BridgeGuardian>();
            monster.ExternallyControlled = true;
            monster.Initialize(data, null, null);
            guardian.Init(monster, hit.position, facing);
        }
    }
}
