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

        [Header("Magic Class")]
        [SerializeField] private ManaComponent mana;
        [SerializeField] private GameObject magicProjectilePrefab;

        /// 通常攻撃を出した瞬間（1人称の腕の振り用）。
        public event System.Action Attacked;

        public int Damage => damage;
        public void SetDamage(int value) { damage = value; }

        private HeroInputActions inputs;
        private float cooldownTimer;
        private HeroRegen regen;
        private HeroClassInitializer classInit;
        private CharacterModelAnimator modelAnim;

        private bool IsAnimLocked()
        {
            if (modelAnim == null) modelAnim = GetComponent<CharacterModelAnimator>();
            return modelAnim != null && (modelAnim.IsRespawnAnimPlaying || modelAnim.CastLocked);
        }

        private void Awake()
        {
            inputs = new HeroInputActions();
            regen = GetComponent<HeroRegen>();
            classInit = GetComponent<HeroClassInitializer>();
            if (mana == null) mana = GetComponent<ManaComponent>();
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
            if (Game.UI.ConsoleUI.IsConsoleActive) return;
            if (IsAnimLocked()) return;
            if (StatusEffectController.IsStunnedObj(gameObject)) return;
            if (cooldownTimer > 0f || cameraTransform == null) return;

            int mpCost = (classInit != null && classInit.ClassData != null) ? classInit.ClassData.basicAttackMpCost : 0;
            if (mpCost > 0)
            {
                if (mana == null || mana.CurrentMP < mpCost)
                {
                    Debug.Log($"[HeroAttack] not enough MP ({(mana != null ? mana.CurrentMP : 0)}/{mpCost})");
                    return;
                }
                mana.TryUseMana(mpCost);
            }

            cooldownTimer = attackInterval;
            if (regen != null) regen.NotifyActed();
            Attacked?.Invoke();

            bool isMage = classInit != null && classInit.ClassData != null && classInit.ClassData.className == "Mage";
            if (isMage && magicProjectilePrefab != null) FireMagicProjectile();
            else DoRaycastAttack();
        }

        private void DoRaycastAttack()
        {
            var origin = cameraTransform.position;
            var dir = cameraTransform.forward;
            Debug.DrawRay(origin, dir * range, Color.red, 0.2f);
            if (Physics.Raycast(origin, dir, out var hit, range))
            {
                var hp = hit.collider.GetComponentInParent<HealthComponent>();
                if (hp != null) hp.TakeDamage(damage, gameObject, DamageType.Physical);
            }
        }

        private void FireMagicProjectile()
        {
            var origin = cameraTransform.position + cameraTransform.forward * 0.5f;
            var dir = cameraTransform.forward;
            var go = Instantiate(magicProjectilePrefab, origin, Quaternion.LookRotation(dir));
            var mp = go.GetComponent<MagicProjectile>();
            int dmg = classInit != null ? Mathf.Max(1, classInit.CurrentMAtk) : damage;
            if (mp != null) mp.Init(gameObject, dmg, dir);
        }
    }
}
