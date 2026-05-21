using UnityEngine;
using UnityEngine.InputSystem;
using Game.UI;

namespace Game.Core
{
    public class FallRespawn : MonoBehaviour
    {
        [SerializeField] private float fallThresholdY = -10f;

        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private CharacterController controller;
        private bool fallen;

        private void Start()
        {
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            controller = GetComponent<CharacterController>();
        }

        private void Update()
        {
            if (!fallen)
            {
                if (transform.position.y < fallThresholdY)
                {
                    fallen = true;
                    if (FallRespawnPrompt.Instance != null) FallRespawnPrompt.Instance.Show();
                    Debug.Log($"[FallRespawn] {name} fell below y={fallThresholdY}; showing respawn prompt.");
                }
                return;
            }

            if (Game.UI.ConsoleUI.IsConsoleActive) return;
            if (Keyboard.current != null && Keyboard.current.backspaceKey.wasPressedThisFrame)
            {
                Respawn();
            }
        }

        private void Respawn()
        {
            if (controller != null)
            {
                controller.enabled = false;
                transform.position = spawnPosition;
                transform.rotation = spawnRotation;
                controller.enabled = true;
            }
            else
            {
                transform.position = spawnPosition;
                transform.rotation = spawnRotation;
            }
            fallen = false;
            if (FallRespawnPrompt.Instance != null) FallRespawnPrompt.Instance.Hide();
            Debug.Log($"[FallRespawn] {name} respawned at {spawnPosition}");
        }
    }
}
