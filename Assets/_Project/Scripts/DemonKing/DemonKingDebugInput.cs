using UnityEngine;
using UnityEngine.InputSystem;
using Game.Combat;
using Game.Core;

namespace Game.DemonKing
{
    [RequireComponent(typeof(DemonKingController))]
    public class DemonKingDebugInput : MonoBehaviour
    {
        private DemonKingController controller;
        private ManaComponent mana;
        private HealthComponent health;

        private void Awake()
        {
            controller = GetComponent<DemonKingController>();
            mana = GetComponent<ManaComponent>();
            health = GetComponent<HealthComponent>();
        }

        private void OnEnable()
        {
            var inputs = controller.Inputs;
            inputs.DemonKing.DebugUseMP.performed += OnUseMP;
            inputs.DemonKing.DebugSelfDamage.performed += OnSelfDamage;
            inputs.DemonKing.DebugConfirmPrep.performed += OnConfirmPrep;
            inputs.DemonKing.DebugSkipLevel.performed += OnSkipLevel;
        }

        private void OnDisable()
        {
            if (controller != null && controller.Inputs != null)
            {
                controller.Inputs.DemonKing.DebugUseMP.performed -= OnUseMP;
                controller.Inputs.DemonKing.DebugSelfDamage.performed -= OnSelfDamage;
                controller.Inputs.DemonKing.DebugConfirmPrep.performed -= OnConfirmPrep;
                controller.Inputs.DemonKing.DebugSkipLevel.performed -= OnSkipLevel;
            }
        }

        private void OnSkipLevel(InputAction.CallbackContext _)
        {
            var lvl = GetComponent<Game.Core.LevelComponent>();
            if (lvl != null) lvl.LevelUp();
        }

        private void OnConfirmPrep(InputAction.CallbackContext _)
        {
            if (MatchManager.Instance == null) return;
            if (MatchManager.Instance.CurrentState != MatchManager.MatchState.DemonPrepPhase) return;
            MatchManager.Instance.SkipPrepPhase();
        }

        private void OnUseMP(InputAction.CallbackContext _)
        {
            if (mana == null) return;
            bool ok = mana.TryUseMana(20);
            Debug.Log($"[DemonKing] TryUseMana(20) -> {(ok ? "OK" : "FAIL")}, MP={mana.CurrentMP}/{mana.MaxMP}");
        }

        private void OnSelfDamage(InputAction.CallbackContext _)
        {
            if (health == null) return;
            health.TakeDamage(50);
        }
    }
}
