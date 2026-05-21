using UnityEngine;

namespace Game.Core
{
    public class RoleSwitcher : MonoBehaviour
    {
        [SerializeField] private PrepPhaseManager prepPhaseManager;

        public void Trigger()
        {
            if (prepPhaseManager != null) prepPhaseManager.SwitchRole();
        }

        public PrepPhaseManager PrepPhaseManager => prepPhaseManager;
    }
}
