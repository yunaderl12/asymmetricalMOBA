using System.Collections.Generic;
using UnityEngine;
using Game.Monster;
using Game.Core;

namespace Game.DemonKing
{
    public class DemonKingDeck : MonoBehaviour
    {
        [SerializeField] private List<MonsterData> deck = new List<MonsterData>();

        public int Count => deck != null ? deck.Count : 0;
        public IReadOnlyList<MonsterData> Deck => deck;

        private void Awake()
        {
            if (GameSession.Instance != null && GameSession.Instance.DemonDeck != null && GameSession.Instance.DemonDeck.Count > 0)
            {
                deck = new List<MonsterData>(GameSession.Instance.DemonDeck);
            }
        }

        public MonsterData GetByIndex(int i)
        {
            if (deck == null) return null;
            if (i < 0 || i >= deck.Count) return null;
            return deck[i];
        }
    }
}
