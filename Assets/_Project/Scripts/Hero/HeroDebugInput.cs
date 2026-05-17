using UnityEngine;
using UnityEngine.InputSystem;
using Game.Combat;

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
        }

        private void OnDisable()
        {
            if (inputs == null) return;
            inputs.Hero.DebugAddExp.performed -= OnAddExp;
            inputs.Hero.DebugWinHero.performed -= OnDebugWinHero;
            inputs.Hero.DebugLoseHero.performed -= OnDebugLoseHero;
            inputs.Hero.Disable();
        }

        private void OnAddExp(InputAction.CallbackContext _)
        {
            if (exp != null) exp.AddExp(debugExpAmount);
        }

        private void OnDebugWinHero(InputAction.CallbackContext _)
        {
            var spawners = GameObject.FindGameObjectsWithTag("Spawner");
            int killed = 0;
            foreach (var s in spawners)
            {
                var hc = s.GetComponent<HealthComponent>();
                if (hc != null && !hc.IsDead)
                {
                    hc.TakeDamage(hc.MaxHP * 2, gameObject);
                    killed++;
                }
            }
            Debug.Log($"[Debug] N: spawners killed = {killed}");
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
