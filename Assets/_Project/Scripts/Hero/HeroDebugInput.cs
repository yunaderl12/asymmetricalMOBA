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
        }

        private void OnDisable()
        {
            if (inputs == null) return;
            inputs.Hero.DebugAddExp.performed -= OnAddExp;
            inputs.Hero.Disable();
        }

        private void OnAddExp(InputAction.CallbackContext _)
        {
            if (exp != null) exp.AddExp(debugExpAmount);
        }
    }
}
