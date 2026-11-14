using UnityEngine;
using Game.Combat;

namespace Game.Hero
{
    /// 自分が操作する勇者に付くパッシブの共通部分。
    public abstract class HeroPassive : MonoBehaviour
    {
        protected PassiveData Data { get; private set; }
        protected HeroFPSController Controller { get; private set; }
        protected CharacterController Body { get; private set; }
        protected HealthComponent Health { get; private set; }
        protected HeroInputActions Inputs { get; private set; }

        private ManaComponent mana;
        private HeroRegen regen;
        private CharacterModelAnimator modelAnim;

        protected CharacterModelAnimator ModelAnim
        {
            get
            {
                if (modelAnim == null) modelAnim = GetComponent<CharacterModelAnimator>();
                return modelAnim;
            }
        }

        public static HeroPassive Attach(GameObject hero, PassiveData data)
        {
            HeroPassive passive = null;
            switch (data.type)
            {
                case PassiveType.QuickDodge: passive = hero.AddComponent<QuickDodgePassive>(); break;
                case PassiveType.ChargedSlash: passive = hero.AddComponent<ChargedSlashPassive>(); break;
                case PassiveType.DoubleJump: passive = hero.AddComponent<DoubleJumpPassive>(); break;
                case PassiveType.BlockCircle: passive = hero.AddComponent<BlockCirclePassive>(); break;
                case PassiveType.HealRipple: passive = hero.AddComponent<HealRipplePassive>(); break;
                case PassiveType.Cleanse: passive = hero.AddComponent<CleansePassive>(); break;
                case PassiveType.LightStep: passive = hero.AddComponent<LightStepPassive>(); break;
                case PassiveType.Unshakable: passive = hero.AddComponent<UnshakablePassive>(); break;
                case PassiveType.LastStand: passive = hero.AddComponent<LastStandPassive>(); break;
                case PassiveType.Rage: passive = hero.AddComponent<RagePassive>(); break;
            }
            if (passive != null) passive.Init(data);
            Debug.Log($"[Passive] {hero.name}: {data.passiveName}");
            return passive;
        }

        private void Init(PassiveData data)
        {
            Data = data;
            OnInit();
        }

        protected virtual void OnInit() { }

        /// HUD に出すパッシブのゲージ（怒り・背水の構えなど）。名前が null ならゲージを出さない。
        public virtual string GaugeLabel => null;
        /// ゲージの溜まり具合（0〜1）。
        public virtual float GaugeFill => 0f;
        /// 効果が出ている・満タンのとき true（ゲージが光る）。
        public virtual bool GaugeLit => false;

        /// 自分が持っているパッシブ（なければ null）。
        public static HeroPassive Of(GameObject hero) => hero != null ? hero.GetComponent<HeroPassive>() : null;

        protected virtual void Awake()
        {
            Controller = GetComponent<HeroFPSController>();
            Body = GetComponent<CharacterController>();
            Health = GetComponent<HealthComponent>();
            mana = GetComponent<ManaComponent>();
            regen = GetComponent<HeroRegen>();
            Inputs = new HeroInputActions();
        }

        protected virtual void OnEnable()
        {
            Inputs.Hero.Enable();
        }

        protected virtual void OnDisable()
        {
            Inputs.Hero.Disable();
        }

        /// 操作を受け付けられる状態か（準備時間と試合終了後は Controller が止まっている）。
        protected bool CanAct()
        {
            if (Game.UI.InGameMenu.BlocksGameInput) return false;
            if (Controller == null || !Controller.enabled || Controller.Suspended || Controller.SkillMotion || Controller.MovementLocked) return false;
            if (Health != null && Health.IsDead) return false;
            if (StatusEffectController.IsStunnedObj(gameObject)) return false;
            return ModelAnim == null || !ModelAnim.IsRespawnAnimPlaying;
        }

        /// MPが足りれば消費する（消費0のパッシブはそのまま使える）。
        protected bool TryPay()
        {
            if (Data.mpCost > 0)
            {
                if (mana == null || !mana.TryUseMana(Data.mpCost)) return false;
            }
            if (regen != null) regen.NotifyActed();
            return true;
        }
    }
}
