using System.Collections.Generic;
using UnityEngine;
using Game.Monster;

namespace Game.UI
{
    /// マッチロビー（魔王側）で使える魔物・橋の番人・最初の配置の Cost 上限。画面は MatchLobby が作る。
    public class DemonLoadoutPanel : MonoBehaviour
    {
        [Header("Available Monster Pool")]
        [SerializeField] private List<MonsterData> availableMonsters = new List<MonsterData>();

        [Header("Bridge Guardians (3)")]
        [SerializeField] private List<MonsterData> availableGuardians = new List<MonsterData>();

        [Header("Spawner Initial")]
        [SerializeField] private SpawnerData initialSpawnerData;
        [SerializeField] private int fallbackMaxCost = 2;

        public List<MonsterData> AvailableMonsters => availableMonsters;
        public List<MonsterData> AvailableGuardians => availableGuardians;
        public int InitialMaxCost => initialSpawnerData != null ? initialSpawnerData.maxCost : fallbackMaxCost;
    }
}
