using UnityEngine;
using Game.Combat;

namespace Game.Bot
{
    public class BotDemonKing : MonoBehaviour
    {
        private HealthComponent health;

        private void Awake()
        {
            health = GetComponent<HealthComponent>();
        }
    }
}
