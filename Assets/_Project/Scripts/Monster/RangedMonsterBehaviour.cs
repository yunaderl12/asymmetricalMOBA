using UnityEngine;

namespace Game.Monster
{
    /// 遠くから撃つ雑魚魔物（ドライアド・ハーピー・イフリート）の共通の流れ。
    /// 勇者が attackRange に入って向き合ったら、止まって溜め（attackWindup 秒）→ 撃つ → 少し構えを残して動き出す。
    /// 間合い（keepDistance で止まり、retreatDistance より近いと下がる）は Monster が受け持つ。
    public abstract class RangedMonsterBehaviour : MonsterBehaviour
    {
        protected enum Phase { Free, Windup, Recover }

        private const float RecoverSeconds = 0.35f;
        private const float AimDegrees = 25f;

        protected Phase phase = Phase.Free;
        protected float phaseTime;
        /// 溜めの進み具合（0〜1）。撃ったあとは 1 から 0 へ戻る。
        protected float Charge;
        private Transform aimed;

        /// 弾の出る位置。
        protected abstract Vector3 Muzzle { get; }
        protected abstract void Fire(Vector3 from, Vector3 direction);

        /// 撃ってよい状態か（ハーピーは飛び上がってから撃つ）。
        protected virtual bool ReadyToShoot => true;

        protected override void Tick(float dt)
        {
            phaseTime += dt;
            var target = Target;
            switch (phase)
            {
                case Phase.Free:
                    Charge = Mathf.MoveTowards(Charge, 0f, dt / RecoverSeconds);
                    if (target == null || cooldown > 0f || !ReadyToShoot) return;
                    Vector3 to = FlatTo(target.position);
                    if (to.magnitude > data.attackRange) return;
                    FaceTowards(target.position);
                    if (Vector3.Angle(transform.forward, to) > AimDegrees) return;
                    aimed = target;
                    monster.HoldMovement = true;
                    phase = Phase.Windup;
                    phaseTime = 0f;
                    OnWindupStart();
                    break;
                case Phase.Windup:
                    if (aimed != null) FaceTowards(aimed.position, 360f);
                    Charge = Mathf.Clamp01(phaseTime / Mathf.Max(0.05f, data.attackWindup));
                    OnWindup(Charge);
                    if (phaseTime < data.attackWindup) return;
                    Vector3 from = Muzzle;
                    Vector3 aim = aimed != null ? Chest(aimed) : from + transform.forward * 10f;
                    Fire(from, (aim - from).normalized);
                    cooldown = data.attackCooldown;
                    phase = Phase.Recover;
                    phaseTime = 0f;
                    break;
                case Phase.Recover:
                    Charge = Mathf.Clamp01(1f - phaseTime / RecoverSeconds);
                    if (phaseTime < RecoverSeconds) return;
                    monster.HoldMovement = false;
                    phase = Phase.Free;
                    break;
            }
        }

        protected virtual void OnWindupStart() { }
        protected virtual void OnWindup(float charge) { }

        protected override void Interrupt()
        {
            base.Interrupt();
            if (phase == Phase.Windup) OnWindupCancelled();
            phase = Phase.Free;
            cooldown = Mathf.Max(cooldown, data.attackCooldown * 0.5f);
        }

        protected virtual void OnWindupCancelled() { }

        /// 扇形に何本か撃つ（ドライアドの木の葉）。
        protected void FireFan(MonsterBolt.Kind kind, Vector3 from, Vector3 direction)
        {
            int count = Mathf.Max(1, data.projectileCount);
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0f : i / (count - 1f) - 0.5f;
                Vector3 dir = Quaternion.AngleAxis(t * data.projectileSpread, Vector3.up) * direction;
                MonsterBolt.Spawn(kind, gameObject, from, dir, data.projectileSpeed, data.attackRange + 4f,
                    data.projectileHitRadius, Damage(data.attackDamageScale));
            }
        }
    }
}
