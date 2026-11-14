using UnityEngine;
using UnityEngine.InputSystem;
using Game.Skill;

namespace Game.Hero
{
    /// 剣士の高速避け。A / D を素早く2回押すと、その方向へ短く滑る。スライディング中は出ない。
    /// 走っている最中なら、前へ進む勢いを保ったまま横へ避ける。
    public class QuickDodgePassive : HeroPassive
    {
        private const float GhostInterval = 0.04f;
        private const float GhostSeconds = 0.24f;
        private const float CameraShift = 0.14f;
        private const float CameraRollDegrees = 3.5f;
        // 2回押しが攻撃の終わり際などで出せなかったときに、出せるようになるまで待つ秒数。
        private const float BufferSeconds = 0.2f;

        private InputAction leftTap;
        private InputAction rightTap;
        private int lastSide;
        private double lastPressTime = double.NegativeInfinity;
        private int requestedSide;
        private float requestedAt;
        private float cooldownTimer;
        private bool dodging;
        private float dodgeTimer;
        private float nextGhost;
        private Vector3 direction;
        private Vector3 carry;
        private float side;

        protected override void Awake()
        {
            base.Awake();
            // 押した瞬間をイベントで受ける。毎フレーム押されているかを見るやり方だと、素早い2回押しの
            // 「離した瞬間」がフレームの間に収まって見えず、2回目と数えられないことがあった。
            leftTap = new InputAction("DodgeLeft", InputActionType.Button, "<Keyboard>/a");
            rightTap = new InputAction("DodgeRight", InputActionType.Button, "<Keyboard>/d");
            leftTap.performed += ctx => OnTap(-1, ctx.time);
            rightTap.performed += ctx => OnTap(1, ctx.time);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            leftTap.Enable();
            rightTap.Enable();
        }

        private void OnTap(int tapSide, double time)
        {
            if (tapSide == lastSide && time - lastPressTime <= Data.doubleTapSeconds)
            {
                requestedSide = tapSide;
                requestedAt = Time.unscaledTime;
                lastPressTime = double.NegativeInfinity; // 3回目の押しは新しい1回目として数える
                return;
            }
            lastSide = tapSide;
            lastPressTime = time;
        }

        private void Update()
        {
            if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;
            if (dodging) { TickDodge(); return; }
            if (requestedSide == 0) return;
            // 少なくとも1回は試してから、待ち時間を過ぎていたらあきらめる（重いフレームの直後でも取りこぼさない）。
            if (TryDodge(requestedSide) || Time.unscaledTime - requestedAt > BufferSeconds) requestedSide = 0;
        }

        private bool TryDodge(int dodgeSide)
        {
            if (cooldownTimer > 0f || !CanAct()) return false;
            if (Controller.IsSliding) { requestedSide = 0; return false; }
            if (!TryPay()) return false;
            cooldownTimer = Data.cooldown;
            dodging = true;
            dodgeTimer = 0f;
            nextGhost = 0f;
            side = dodgeSide;
            direction = transform.right * dodgeSide;
            // 走っていたら、前へ進む勢いをそのまま足す。
            carry = Controller.IsDashing ? Vector3.Project(Controller.LastMoveVelocity, transform.forward) : Vector3.zero;
            if (Vector3.Dot(carry, transform.forward) < 0f) carry = Vector3.zero;
            MeleeFx.DustBurst(transform.position, 0.3f, 10, 3f);
            return true;
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
            Controller.BurstVelocity = direction * (Data.dodgeDistance * (Traveled(t) - Traveled(before)) / Time.deltaTime) + carry;

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
            leftTap?.Disable();
            rightTap?.Disable();
            requestedSide = 0;
            if (dodging) EndDodge();
        }
    }
}
