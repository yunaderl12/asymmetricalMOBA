using UnityEngine;

namespace Game.Hero
{
    public class HumanHeroMarker : MonoBehaviour
    {
        public static HumanHeroMarker Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
