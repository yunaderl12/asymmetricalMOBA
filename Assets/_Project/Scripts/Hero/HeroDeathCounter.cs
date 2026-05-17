using UnityEngine;

namespace Game.Hero
{
    public class HeroDeathCounter : MonoBehaviour
    {
        [SerializeField] private int currentDeathCount;

        public int CurrentDeathCount => currentDeathCount;

        public void IncrementDeathCount()
        {
            currentDeathCount++;
        }
    }
}
