using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Core
{
    public class RoleSwitcher : MonoBehaviour
    {
        [SerializeField] private PrepPhaseManager prepPhaseManager;

        private void Update()
        {
            if (Keyboard.current == null || prepPhaseManager == null) return;
            if (Keyboard.current.tKey.wasPressedThisFrame)
            {
                prepPhaseManager.SwitchRole();
            }
        }
    }
}
