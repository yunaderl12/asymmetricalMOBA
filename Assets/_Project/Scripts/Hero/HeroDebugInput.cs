using UnityEngine;
using UnityEngine.InputSystem;
using Game.Combat;
using Game.Core;

namespace Game.Hero
{
    public class HeroDebugInput : MonoBehaviour
    {
        [SerializeField] private int debugExpAmount = 50;

        private HeroInputActions inputs;
        private ExpComponent exp;

        private void Awake()
        {
            if (inputs == null) inputs = new HeroInputActions();
            exp = GetComponent<ExpComponent>();
        }

        private void OnEnable()
        {
            if (inputs == null) inputs = new HeroInputActions();
            inputs.Hero.Enable();
            inputs.Hero.DebugAddExp.performed += OnAddExp;
            inputs.Hero.DebugWinHero.performed += OnDebugWinHero;
            inputs.Hero.DebugLoseHero.performed += OnDebugLoseHero;
            inputs.Hero.DebugSpawnPowerItem.performed += OnDebugSpawnPowerItem;
        }

        private void OnDisable()
        {
            if (inputs == null) return;
            inputs.Hero.DebugAddExp.performed -= OnAddExp;
            inputs.Hero.DebugWinHero.performed -= OnDebugWinHero;
            inputs.Hero.DebugLoseHero.performed -= OnDebugLoseHero;
            inputs.Hero.DebugSpawnPowerItem.performed -= OnDebugSpawnPowerItem;
            inputs.Hero.Disable();
        }

        private void OnDebugSpawnPowerItem(InputAction.CallbackContext _)
        {
            if (MatchItemGimmick.Instance == null) { Debug.LogWarning("[Debug] F: MatchItemGimmick not in scene"); return; }
            MatchItemGimmick.Instance.SpawnPowerItemNow();
        }

        private void OnAddExp(InputAction.CallbackContext _)
        {
            if (exp != null) exp.AddExp(debugExpAmount);
        }

        private void OnDebugWinHero(InputAction.CallbackContext _)
        {
            var castle = GameObject.FindWithTag("DemonCastle");
            if (castle == null) { Debug.LogWarning("[Debug] N: DemonCastle not found"); return; }
            var core = castle.GetComponent<HealthComponent>();
            if (core == null) core = castle.GetComponentInChildren<HealthComponent>();
            if (core != null && !core.IsDead)
            {
                core.TakeDamage(core.MaxHP * 2, gameObject);
                Debug.Log("[Debug] N: DemonCastle_Core killed");
            }
            else Debug.LogWarning("[Debug] N: DemonCastle has no HealthComponent");
        }

        private void OnDebugLoseHero(InputAction.CallbackContext _)
        {
            var st = GameObject.FindWithTag("StartTown");
            if (st == null) { Debug.LogWarning("[Debug] B: StartTown not found"); return; }
            var core = st.GetComponent<HealthComponent>();
            if (core == null) core = st.GetComponentInChildren<HealthComponent>();
            if (core != null && !core.IsDead)
            {
                core.TakeDamage(core.MaxHP * 2, gameObject);
                Debug.Log("[Debug] B: StartTown_Core killed");
            }
        }
    }
}
