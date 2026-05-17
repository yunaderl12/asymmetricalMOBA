using UnityEngine;
using UnityEngine.InputSystem;
using Game.Combat;

namespace Game.Hero
{
    public class HeroAttack : MonoBehaviour
    {
        [Header("Aim")]
        [SerializeField] private Transform cameraTransform;

        [Header("Combat")]
        [SerializeField] private float range = 10f;
        [SerializeField] private int damage = 10;
        [SerializeField] private float attackInterval = 0.3f;

        private HeroInputActions inputs;
        private float cooldownTimer;

        private void Awake()
        {
            inputs = new HeroInputActions();
        }

        private void OnEnable()
        {
            if (inputs == null) inputs = new HeroInputActions();
            inputs.Hero.Enable();
            inputs.Hero.Attack.performed += OnAttackPerformed;
        }

        private void OnDisable()
        {
            if (inputs == null) return;
            inputs.Hero.Attack.performed -= OnAttackPerformed;
            inputs.Hero.Disable();
        }

        private void Update()
        {
            if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;
        }

        private void OnAttackPerformed(InputAction.CallbackContext _)
        {
            if (cooldownTimer > 0f || cameraTransform == null) return;
            cooldownTimer = attackInterval;

            var origin = cameraTransform.position;
            var dir = cameraTransform.forward;
            Debug.DrawRay(origin, dir * range, Color.red, 0.2f);

            if (Physics.Raycast(origin, dir, out var hit, range))
            {
                var hp = hit.collider.GetComponentInParent<HealthComponent>();
                if (hp != null) hp.TakeDamage(damage);
            }
        }
    }
}
