using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Hero;

namespace Game.Bot
{
    public class BotSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject botHeroPrefab;
        [SerializeField] private GameObject botDemonKingPrefab;
        [SerializeField] private List<HeroClassData> allHeroClasses = new List<HeroClassData>();
        [SerializeField] private Vector3[] heroPositions = new Vector3[]
        {
            new Vector3(-72f, 1f, 2f),
            new Vector3(-72f, 1f, -2f),
            new Vector3(-68f, 1f, 2f),
            new Vector3(-68f, 1f, -2f),
        };
        [SerializeField] private Vector3 demonKingPosition = new Vector3(78f, 1f, 0f);

        private void Start()
        {
            var session = GameSession.Instance;
            if (session == null) return;
            HideOppositeRoleScenePlayer(session.CurrentRole);

            var bots = session.CurrentMatchBots;
            if (bots == null || bots.Count == 0) { Debug.Log("[BotSpawner] No bots to spawn."); return; }

            int heroIdx = 0;
            int spawned = 0;
            foreach (var b in bots)
            {
                if (b.role == GameSession.PlayerRole.Hero && botHeroPrefab != null && heroIdx < heroPositions.Length)
                {
                    var pos = heroPositions[heroIdx];
                    var go = Instantiate(botHeroPrefab, pos, Quaternion.identity);
                    var bh = go.GetComponent<BotHero>();
                    if (bh != null)
                    {
                        bh.SetClass(b.heroClass);
                        bh.SetBotName(b.botName);
                    }
                    heroIdx++;
                    spawned++;
                }
                else if (b.role == GameSession.PlayerRole.DemonKing && botDemonKingPrefab != null)
                {
                    var go = Instantiate(botDemonKingPrefab, demonKingPosition, Quaternion.identity);
                    go.name = b.botName;
                    spawned++;
                }
            }
            Debug.Log($"[BotSpawner] Spawned {spawned} bots.");
        }

        private void HideOppositeRoleScenePlayer(GameSession.PlayerRole humanRole)
        {
            if (humanRole == GameSession.PlayerRole.Hero)
            {
                var dk = GameObject.FindWithTag("DemonKing");
                if (dk != null) { dk.SetActive(false); Debug.Log("[BotSpawner] Scene DemonKing deactivated (human=Hero)."); }
            }
            else if (humanRole == GameSession.PlayerRole.DemonKing)
            {
                var hero = HumanHeroMarker.Instance != null ? HumanHeroMarker.Instance.gameObject : GameObject.FindWithTag("Player");
                if (hero != null) { hero.SetActive(false); Debug.Log("[BotSpawner] Scene Hero deactivated (human=DemonKing)."); }
            }
        }
    }
}
