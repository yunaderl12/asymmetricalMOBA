using UnityEngine;
using Game.Skill;

namespace Game.Hero
{
    /// 剣士の高速避け。A / D を素早く2回押すと、その方向へ短く滑る。
    public class QuickDodgePassive : HeroPassive
    {
        private const float GhostInterval = 0.04f;
        private const float GhostSeconds = 0.24f;
        private const float CameraShift = 0.14f;
        private const float CameraRollDegrees = 3.5f;

        private int heldSide;
        private int lastSide;
        private float lastPressTime = float.NegativeInfinity;
        private float cooldownTimer;
        private bool dodging;
        private float dodgeTimer;
        private float nextGhost;
        private Vector3 direction;
        private float side;

        private void Update()
        {
            if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;
            if (dodging) { TickDodge(); return; }

            float x = Inputs.Hero.Move.ReadValue<Vector2>().x;
            int current = x > 0.5f ? 1 : x < -0.5f ? -1 : 0;
            if (current != 0 && current != heldSide)
            {
                if (current == lastSide && Time.time - lastPressTime <= Data.doubleTapSeconds) TryDodge(current);
                lastSide = current;
                lastPressTime = Time.time;
            }
            heldSide = current;
        }

        private void TryDodge(int dodgeSide)
        {
            if (cooldownTimer > 0f || !CanAct() || !TryPay()) return;
            cooldownTimer = Data.cooldown;
            dodging = true;
            dodgeTimer = 0f;
            nextGhost = 0f;
            side = dodgeSide;
            direction = transform.right * dodgeSide;
            MeleeFx.DustBurst(transform.position, 0.3f, 10, 3f);
        }

        private void TickDodge()
        {
            float before = dodgeTimer / Data.dodgeSeconds;
            if (before >= 1f || !CanAct())
            {
                EndDodge();
                return;
            }
            dodgeTimer += Time.deltaTime;
            float t = Mathf.Min(dodgeTimer / Data.dodgeSeconds, 1f);
            // 終わりに向けて速度を落とす。フレームレートによらず同じ距離を進むよう、このフレームで進む量から速度を出す。
            Controller.BurstVelocity = direction * (Data.dodgeDistance * (Traveled(t) - Traveled(before)) / Time.deltaTime);

            float lean = Mathf.Sin(t * Mathf.PI);
            Controller.SetCameraKick(new Vector3(-side * CameraShift * lean, 0f, 0f), 0f, -side * CameraRollDegrees * lean);

            if (dodgeTimer >= nextGhost && ModelAnim != null)
            {
                AfterImage.Spawn(ModelAnim.ModelRoot, MeleeFx.SteelBlue, GhostSeconds);
                nextGhost = dodgeTimer + GhostInterval;
            }
        }

        // 速度 (1 - t^2) を積分した、全体を 1 とする移動量。
        private static float Traveled(float t)
        {
            return 1.5f * (t - t * t * t / 3f);
        }

        private void EndDodge()
        {
            dodging = false;
            Controller.BurstVelocity = Vector3.zero;
            Controller.SetCameraKick(Vector3.zero, 0f, 0f);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (dodging) EndDodge();
        }
    }
}
