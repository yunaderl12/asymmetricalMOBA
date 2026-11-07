using UnityEngine;
using Game.Combat;
using Game.Skill;

namespace Game.Hero
{
    /// 剣士の斬撃。このパッシブの間は通常攻撃がボタンを離した瞬間に出る。
    /// すぐ離せば通常攻撃、長押しして溜まってから離せば前方へ斬撃を飛ばす。
    public class ChargedSlashPassive : HeroPassive
    {
        private const float PoseDelay = 0.2f; // 普通のクリックで構えが動かないように待つ
        private const float SwingSeconds = 0.14f;
        private const float SparkleInterval = 0.1f;
        private const float WaveStartDistance = 1.8f;

        private static readonly Vector3 RaisedPosition = new Vector3(0.32f, -0.14f, 0.4f);
        private static readonly Vector3 RaisedEuler = new Vector3(-12f, 22f, -28f);
        private static readonly Vector3 SwungPosition = new Vector3(-0.22f, -0.26f, 0.44f);
        private static readonly Vector3 SwungEuler = new Vector3(85f, -35f, 50f);
        private static readonly Vector3 BladeInView = new Vector3(0.67f, 0.15f, 0.75f); // カメラから見た剣先のあたり

        private HeroAttack attack;
        private HeroViewModel view;
        private Transform cameraTransform;
        private StatModifierContainer mods;
        private bool holding;
        private bool cancelled;
        private bool ready;
        private float holdTimer;
        private float cooldownTimer;
        private float swingTimer;
        private float nextSparkle;

        protected override void OnInit()
        {
            attack = GetComponent<HeroAttack>();
            if (attack != null) attack.AttackOnPress = false;
            mods = GetComponent<StatModifierContainer>();
            var cam = GetComponentInChildren<Camera>(true);
            if (cam != null) cameraTransform = cam.transform;
        }

        private bool Usable => CanAct() && attack != null && attack.enabled && (ModelAnim == null || !ModelAnim.CastLocked);

        private void Update()
        {
            if (view == null) view = GetComponent<HeroViewModel>();
            if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;
            TickSwing();

            bool pressed = Inputs.Hero.Attack.IsPressed();
            if (pressed && !holding)
            {
                holding = true;
                cancelled = false;
                ready = false;
                holdTimer = 0f;
            }
            if (!holding) return;

            // スキルや気絶で構えが解けたら、押し直すまで何も出さない。
            if (!cancelled && !Usable)
            {
                cancelled = true;
                ready = false;
                EndPose();
            }
            if (cancelled)
            {
                if (!pressed) holding = false;
                return;
            }

            if (pressed) TickHold();
            else Release();
        }

        private void TickHold()
        {
            holdTimer += Time.deltaTime;
            if (holdTimer < PoseDelay) return;

            float k = Mathf.Clamp01((holdTimer - PoseDelay) / Mathf.Max(0.01f, Data.holdSeconds - PoseDelay));
            if (view != null) view.PoseRight(RaisedPosition, RaisedEuler);
            if (ModelAnim != null) ModelAnim.SetMeleePose(-0.5f * k, 0f, k, transform.position + transform.up * 2f + transform.right * 0.7f - transform.forward * 0.2f);

            if (!ready && holdTimer >= Data.holdSeconds && cooldownTimer <= 0f)
            {
                ready = true;
                nextSparkle = 0f;
                SparkleStar.Spawn(BladePoint(), 0.5f, 0.25f, Vector3.zero);
            }
            if (ready && Time.time >= nextSparkle)
            {
                nextSparkle = Time.time + SparkleInterval;
                SparkleStar.Spawn(BladePoint() + Random.insideUnitSphere * 0.09f, 0.14f, 0.2f, Vector3.up * 0.25f);
            }
        }

        private void Release()
        {
            holding = false;
            bool fire = ready && TryPay();
            ready = false;
            if (!fire)
            {
                // 溜まる前に離したとき（溜まっていてもMPが足りないとき）は通常攻撃。
                EndPose();
                attack.TryBasicAttack();
                return;
            }
            cooldownTimer = Data.cooldown;
            swingTimer = SwingSeconds;
            Fire();
        }

        private void Fire()
        {
            Vector3 aim = Controller.AimForward;
            Vector3 eye = cameraTransform != null ? cameraTransform.position : transform.position + Vector3.up * 1.5f;
            Vector3 up = cameraTransform != null ? cameraTransform.up : Vector3.up;
            float atk = attack.Damage * (mods != null ? mods.TotalAtkMultiplier : 1f);
            SlashWave.Spawn(gameObject, eye + aim * WaveStartDistance, aim, up, Mathf.RoundToInt(atk * Data.slashDamageScale), Data.slashSpeed, Data.slashRange, Data.slashHitRadius);
            if (ModelAnim != null) ModelAnim.PlayAttack();
            Debug.Log("[Passive] charged slash fired");
        }

        private void TickSwing()
        {
            if (swingTimer <= 0f) return;
            swingTimer -= Time.deltaTime;
            if (swingTimer <= 0f)
            {
                EndPose();
                return;
            }
            if (view != null) view.PoseRight(SwungPosition, SwungEuler);
            if (ModelAnim != null) ModelAnim.SetMeleePose(0.5f, 0f, 0f, Vector3.zero);
        }

        private void EndPose()
        {
            if (ModelAnim != null) ModelAnim.ClearMeleePose();
        }

        // 自分には1人称の剣のあたり、それ以外の視点では右手の上あたりに光を出す。
        private Vector3 BladePoint()
        {
            if (cameraTransform != null) return cameraTransform.TransformPoint(BladeInView);
            return transform.position + Vector3.up * 2f + transform.right * 0.5f;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            holding = ready = false;
            swingTimer = 0f;
            EndPose();
        }

        private void OnDestroy()
        {
            if (attack != null) attack.AttackOnPress = true;
        }
    }
}
