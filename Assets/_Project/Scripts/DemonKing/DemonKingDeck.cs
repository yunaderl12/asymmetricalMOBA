using System.Collections.Generic;
using UnityEngine;
using Game.Monster;

namespace Game.DemonKing
{
    public class DemonKingDeck : MonoBehaviour
    {
        [SerializeField] private List<MonsterData> deck = new List<MonsterData>();

        public int Count => deck != null ? deck.Count : 0;
        public IReadOnlyList<MonsterData> Deck => deck;

        public MonsterData GetByIndex(int i)
        {
            if (deck == null) return null;
            if (i < 0 || i >= deck.Count) return null;
            return deck[i];
        }
    }
}
