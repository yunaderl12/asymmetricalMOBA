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

        private void Start()
        {
            // Make sure the screen-center crosshair exists once the human hero is in the scene.
            Game.UI.CrosshairUI.EnsureExists();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
