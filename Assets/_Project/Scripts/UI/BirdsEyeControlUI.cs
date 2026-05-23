using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Compatibility bootstrapper. The interactive birds-eye view is now driven
    /// by BirdsEyeMapUI (full-screen UI minimap). This component just ensures a
    /// BirdsEyeMapUI exists in the scene so old scene references keep working
    /// without manual scene editing. Its own legacy panel (root/listParent) is
    /// hidden permanently.
    /// </summary>
    public class BirdsEyeControlUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform listParent;
        [SerializeField] private UnityEngine.UI.Text statusText;
        [SerializeField] private Sprite buttonSprite;

        private void Awake()
        {
            if (root != null) root.SetActive(false);
        }

        private void Start()
        {
            if (FindObjectOfType<BirdsEyeMapUI>() == null)
            {
                gameObject.AddComponent<BirdsEyeMapUI>();
                Debug.Log("[BirdsEyeControlUI] auto-added BirdsEyeMapUI");
            }
        }
    }
}
