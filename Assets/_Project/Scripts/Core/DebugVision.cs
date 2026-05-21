using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Core
{
    public class DebugVision : MonoBehaviour
    {
        public static bool IsActive { get; private set; }
        public static event Action<bool> OnToggled;

        private void Update()
        {
            if (Game.UI.ConsoleUI.IsConsoleActive) return;
            if (Keyboard.current == null) return;
            if (Keyboard.current.digit9Key.wasPressedThisFrame)
            {
                IsActive = !IsActive;
                Debug.Log("[DebugVision] toggled -> " + IsActive);
                if (OnToggled != null) OnToggled(IsActive);
            }
        }
    }
}
