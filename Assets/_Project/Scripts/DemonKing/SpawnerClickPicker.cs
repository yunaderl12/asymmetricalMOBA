using UnityEngine;

namespace Game.DemonKing
{
    /// <summary>
    /// DEPRECATED: 3D click handling was replaced by the UI map (BirdsEyeMapUI).
    /// Kept as an empty MonoBehaviour so existing prefab/scene references stay
    /// valid; remove from prefab when convenient.
    /// </summary>
    [RequireComponent(typeof(DemonKingViewSwitcher))]
    public class SpawnerClickPicker : MonoBehaviour
    {
        [SerializeField] private Camera birdsEyeCamera;
        [SerializeField] private float pickRange = 200f;
        [SerializeField] private LayerMask hitMask = ~0;
    }
}
