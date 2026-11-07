using UnityEngine;
using UnityEngine.InputSystem;
using Game.UI;

namespace Game.Core
{
    /// 奈落（橋や島から落ちた先の深い川・海、または Y < fallThresholdY）に落ちたとき。
    /// 勇者は死んだのと同じ扱いにして、RespawnManager のリスポーン待ち（死んでいるあいだの視点つき）を通してまちへ戻す。
    /// 魔王は死亡扱いにせず、そのまま復活地点へ戻す（深い水）／Backspace で戻す（Y < fallThresholdY）。
    public class FallRespawn : MonoBehaviour
    {
        [SerializeField] private float fallThresholdY = -10f;

        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private CharacterController controller;
        private Game.Combat.HealthComponent health;
        private bool isHero;
        private bool fallen;

        private void Start()
        {
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            controller = GetComponent<CharacterController>();
            health = GetComponent<Game.Combat.HealthComponent>();
            isHero = GetComponent<Game.Hero.HumanHeroMarker>() != null;
        }

        private void Update()
        {
            if (isHero && health != null && health.IsDead) return;
            if (!fallen)
            {
                // 橋や島から海に落ちたら、勇者は死亡（リスポーン待ちのあと、まちへ）。魔王はそのまま復活地点へ戻る。
                if (transform.position.y < BalanceConfig.Instance.deepWaterFallHeight && StageMap.IsDeepWater(transform.position))
                {
                    WaterRipple.Spawn(transform.position, BalanceConfig.Instance.rippleRadius * 3f);
                    if (FallToDeath()) return;
                    Respawn();
                    return;
                }
                if (transform.position.y < fallThresholdY)
                {
                    if (FallToDeath()) return;
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

        // 勇者なら HP を0にして死亡扱いにする（倒した相手はなし）。
        private bool FallToDeath()
        {
            if (!isHero || health == null) return false;
            if (!health.IsDead) health.TakeDamage(Mathf.Max(1, health.CurrentHP), null);
            Debug.Log($"[FallRespawn] {name} fell into the abyss and died.");
            return true;
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
