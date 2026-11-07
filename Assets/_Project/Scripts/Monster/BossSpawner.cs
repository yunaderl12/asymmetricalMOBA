using System.Collections.Generic;
using UnityEngine;
using Game.Core;

namespace Game.Monster
{
    /// 試合の始めに、山の広場へドラゴン、砂漠の広場へスコーピオン、森の広場へウェンディゴを1体ずつ出す（場所は StageMap.json）。
    /// 倒されたら BossData.respawnSeconds 後にまた出る（0 なら戻らない）。
    public class BossSpawner : MonoBehaviour
    {
        [SerializeField] private DragonData dragon;
        [SerializeField] private ScorpionData scorpion;
        [SerializeField] private WendigoData wendigo;

        private class Slot
        {
            public BossData data;
            public Vector3 center;
            public float radius;
            public BossBase boss;
            public float respawnAt = -1f;
        }

        private readonly List<Slot> slots = new List<Slot>();
        private bool started;

        public static BossSpawner Instance { get; private set; }

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
            var map = StageMap.Map;
            if (dragon != null && map.dragonPlaza != null && map.dragonPlaza.radius > 0f)
                slots.Add(new Slot { data = dragon, center = PlazaCenter(map.dragonPlaza), radius = map.dragonPlaza.radius });
            if (scorpion != null && map.scorpionPlaza != null && map.scorpionPlaza.radius > 0f)
                slots.Add(new Slot { data = scorpion, center = PlazaCenter(map.scorpionPlaza), radius = map.scorpionPlaza.radius });
            if (wendigo != null && map.forestPlaza != null && map.forestPlaza.radius > 0f)
                slots.Add(new Slot { data = wendigo, center = PlazaCenter(map.forestPlaza), radius = map.forestPlaza.radius });
            // 橋のたもとの結界。その地方のボスを倒すまで、勇者は橋へ進めない。
            var names = new Dictionary<string, string>();
            if (dragon != null) names["dragon"] = dragon.bossName;
            if (scorpion != null) names["scorpion"] = scorpion.bossName;
            if (wendigo != null) names["wendigo"] = wendigo.bossName;
            BossGate.BuildAll(names);

            // 魔王ロビーで中ボスを出さないことにしたバイオームは、広場を空にして結界も初めから開けておく。
            var session = GameSession.Instance;
            if (session != null && session.PlacementChosen)
            {
                slots.RemoveAll(slot =>
                {
                    int b = GameSession.PlacementIndex(BiomeOf(slot.data));
                    if (b < 0 || session.BiomeBossOn[b]) return false;
                    BossGate.OpenSilently(KeyOf(slot.data));
                    return true;
                });
            }
        }

        public static StageMap.Biome BiomeOf(BossData data) =>
            data is DragonData ? StageMap.Biome.Mountain : data is ScorpionData ? StageMap.Biome.Desert : data is WendigoData ? StageMap.Biome.Forest : StageMap.Biome.None;

        public static string KeyOf(BossData data) =>
            data is DragonData ? "dragon" : data is ScorpionData ? "scorpion" : data is WendigoData ? "wendigo" : "";

        private static Vector3 PlazaCenter(StageMap.Plaza plaza)
        {
            // 床の高さ。山の広場は山のメッシュの上、砂漠の広場は平らな地面の上。
            float y = float.MinValue;
            foreach (var hit in Physics.RaycastAll(new Vector3(plaza.x, 300f, plaza.z), Vector3.down, 400f, ~0, QueryTriggerInteraction.Ignore))
            {
                var c = hit.collider;
                if (c.CompareTag("Player") || c.CompareTag("Monster") || c.name == "Field_TreeColliders" || c.name == "Field_Barrier") continue;
                y = Mathf.Max(y, hit.point.y);
            }
            return new Vector3(plaza.x, y > float.MinValue ? y : plaza.height, plaza.z);
        }

        private void Update()
        {
            if (MatchManager.Instance != null && MatchManager.Instance.CurrentState != MatchManager.MatchState.InMatch) return;
            foreach (var slot in slots)
            {
                if (slot.boss != null) continue;
                if (!started || (slot.respawnAt >= 0f && Time.time >= slot.respawnAt)) Spawn(slot);
            }
            started = true;
        }

        private void Spawn(Slot slot)
        {
            slot.respawnAt = -1f;
            var go = new GameObject(slot.data.bossName);
            go.transform.position = slot.center;
            go.transform.rotation = Quaternion.LookRotation(Vector3.back);
            BossBase boss = slot.data is DragonData ? go.AddComponent<DragonBoss>()
                : slot.data is WendigoData ? go.AddComponent<WendigoBoss>()
                : (BossBase)go.AddComponent<ScorpionBoss>();
            boss.Init(slot.data, slot.center, slot.radius, this);
            slot.boss = boss;
        }

        public void OnBossDefeated(BossBase boss)
        {
            foreach (var slot in slots)
            {
                if (slot.boss != boss) continue;
                slot.boss = null;
                BossGate.OpenFor(KeyOf(slot.data));
                slot.respawnAt = slot.data.respawnSeconds > 0f ? Time.time + slot.data.respawnSeconds : float.MaxValue;
            }
        }

        /// デバッグ用: 倒されて待っているボスを今すぐ出す。
        public int RespawnNow()
        {
            int count = 0;
            foreach (var slot in slots)
            {
                if (slot.boss != null) continue;
                Spawn(slot);
                count++;
            }
            return count;
        }
    }
}
