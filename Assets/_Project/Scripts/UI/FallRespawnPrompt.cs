using UnityEngine;

namespace Game.UI
{
    public class FallRespawnPrompt : MonoBehaviour
    {
        public static FallRespawnPrompt Instance { get; private set; }

        [SerializeField] private GameObject root;

        private void Awake()
        {
            Instance = this;
            if (root != null) root.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Show() { if (root != null) root.SetActive(true); }
        public void Hide() { if (root != null) root.SetActive(false); }
    }
}
