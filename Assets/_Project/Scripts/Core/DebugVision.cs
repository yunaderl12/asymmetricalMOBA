using System;
using UnityEngine;

namespace Game.Core
{
    public class DebugVision : MonoBehaviour
    {
        public static DebugVision Instance { get; private set; }
        public static bool IsActive { get; private set; }
        public static event Action<bool> OnToggled;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static void Toggle()
        {
            IsActive = !IsActive;
            Debug.Log("[DebugVision] toggled -> " + IsActive);
            if (OnToggled != null) OnToggled(IsActive);
        }
    }
}
