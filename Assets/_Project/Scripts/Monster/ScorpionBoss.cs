using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Skill;

namespace Game.Monster
{
    /// 砂漠の広場のスコーピオン。広場の中を歩いて勇者を追い、距離に合わせて3つの技を使い分ける。
    ///  - 毒針: 尾を後ろへ引いてから振り下ろし、毒の弾を前へ扇状に飛ばす。当たると毒（継続ダメージ）。
    ///  - 突進: 脚で地面をかいてから、向いた方へまっすぐ走る。ぶつかった勇者は跳ね飛ばされる。広場の縁で止まる。
    ///  - 十字斬り: 両方の爪を広げて振り上げ、前で交差させて振り下ろし、X の形の斬撃を前へ飛ばす。
    public class ScorpionBoss : BossBase
    {
        public enum Attack { None, Sting, Charge, Slash }
        private enum State { Idle, Chase, StingWindup, StingRecover, ChargeWindup, Charging, ChargeRecover, SlashWindup, SlashSwing, SlashRecover }

        private const float WallMargin = 4f;
        private const float StopDistance = 7f;
        private const float WalkFacingDegrees = 40f;
        private const float HeroChestHeight = 1.2f;
        private const float HitHeight = 4f;

        private ScorpionData data;
        private State state = State.Idle;
        private Attack forced = Attack.None;
        private Attack lastAttack = Attack.None;
        private float stateTimer;
        private float attackTimer;
        private float walkPhase;
        private float walkAmount;
        private float travelled;
        private Vector3 chargeDir;
        private float side = 1f; // ArmP がモデルの +X 側なら 1
        private Transform stingerTip;
        private readonly List<HealthComponent> buffer = new List<HealthComponent>();
        private readonly HashSet<HealthComponent> chargeHit = new HashSet<HealthComponent>();

        public void Force(Attack attack)
        {
            forced = attack;
            attackTimer = 0f;
        }

        protected override void OnInit()
        {
            data = (ScorpionData)baseData;
            attackTimer = data.attackInterval;
            if (rig != null)
            {
                var arm = rig.Get("ArmP");
                side = arm != null && model.InverseTransformPoint(arm.position).x < 0f ? -1f : 1f;
                stingerTip = rig.Get("Stinger");
            }
        }

        // ---------------------------------------------------------------- behaviour

        protected override void Tick()
        {
            float dt = Time.deltaTime;
            walkAmount = 0f;
            if (Stunned) return;
            stateTimer += dt;
            switch (state)
            {
                case State.Idle: TickIdle(dt); break;
                case State.Chase: TickChase(dt); break;
                case State.StingWindup: TickStingWindup(); break;
                case State.StingRecover: Recover(data.stingRecoverSeconds); break;
                case State.ChargeWindup: TickChargeWindup(); break;
                case State.Charging: TickCharging(dt); break;
                case State.ChargeRecover: Recover(data.chargeRecoverSeconds); break;
                case State.SlashWindup: TickSlashWindup(); break;
                case State.SlashSwing: TickSlashSwing(); break;
                case State.SlashRecover: Recover(data.slashRecoverSeconds); break;
            }
        }

        private void Enter(State next)
        {
            state = next;
            stateTimer = 0f;
        }

        private void Recover(float seconds)
        {
            if (stateTimer < seconds) return;
            attackTimer = data.attackInterval;
            Enter(target != null ? State.Chase : State.Idle);
        }

        private void TickIdle(float dt)
        {
            if (target != null) { Enter(State.Chase); return; }
            // 誰もいなければ広場の真ん中へ戻る。
            if (Flat(arenaCenter - transform.position).magnitude > 2f) Walk(arenaCenter, dt);
        }

        private void TickChase(float dt)
        {
            if (target == null) { Enter(State.Idle); return; }
            float d = Flat(target.position - transform.position).magnitude;
            attackTimer -= dt;
            if (attackTimer <= 0f)
            {
                var attack = Choose(d);
                if (attack != Attack.None)
                {
                    Begin(attack);
                    return;
                }
            }
            if (d > StopDistance) Walk(target.position, dt);
            else FaceTowards(target.position, data.turnSpeed);
        }

        private void Walk(Vector3 goal, float dt)
        {
            FaceTowards(goal, data.turnSpeed);
            Vector3 to = Flat(goal - transform.position);
            if (Vector3.Angle(transform.forward, to) > WalkFacingDegrees) { walkAmount = 0.4f; return; }
            float step = Mathf.Min(to.magnitude, data.moveSpeed * SpeedFactor * dt);
            transform.position = Grounded(ClampToArena(transform.position + transform.forward * step, WallMargin));
            walkAmount = 1f;
        }

        private Vector3 Grounded(Vector3 p)
        {
            p.y = FloorY(p);
            return p;
        }

        /// 距離で使える技を集め、同じ技が続かないように選ぶ。
        private Attack Choose(float d)
        {
            if (forced != Attack.None)
            {
                var f = forced;
                forced = Attack.None;
                return f;
            }
            var options = new List<Attack>();
            if (d <= data.slashMaxRange) options.Add(Attack.Slash);
            if (d <= data.chargeMaxRange && d > StopDistance) options.Add(Attack.Charge);
            if (d >= data.stingMinRange && d <= data.stingMaxRange) options.Add(Attack.Sting);
            if (options.Count > 1) options.Remove(lastAttack);
            return options.Count > 0 ? options[Random.Range(0, options.Count)] : Attack.None;
        }

        private void Begin(Attack attack)
        {
            lastAttack = attack;
            switch (attack)
            {
                case Attack.Sting: Enter(State.StingWindup); break;
                case Attack.Charge: Enter(State.ChargeWindup); break;
                case Attack.Slash: Enter(State.SlashWindup); break;
            }
        }

        // ---- poison sting

        private void TickStingWindup()
        {
            if (target != null) FaceTowards(target.position, data.turnSpeed * 1.5f);
            if (stateTimer < data.stingWindupSeconds) return;
            Vector3 from = StingerPoint();
            Vector3 aim = target != null ? (target.position + Vector3.up * HeroChestHeight - from).normalized : transform.forward;
            int shots = Mathf.Max(1, data.stingShots);
            for (int i = 0; i < shots; i++)
            {
                float yaw = (i - (shots - 1) * 0.5f) * data.stingSpreadDegrees;
                Vector3 dir = Quaternion.AngleAxis(yaw, Vector3.up) * aim;
                ScorpionVenom.Spawn(gameObject, from, dir, Mathf.RoundToInt(CurrentAtk * data.stingDamageScale), data);
            }
            Enter(State.StingRecover);
        }

        private Vector3 StingerPoint()
        {
            if (stingerTip != null) return stingerTip.position + model.forward * data.modelScale * 0.12f;
            return transform.position + Vector3.up * data.modelScale * 0.8f + transform.forward * 2f;
        }

        // ---- charge

        private void TickChargeWindup()
        {
            if (target != null && stateTimer < data.chargeWindupSeconds * 0.75f) FaceTowards(target.position, data.turnSpeed * 1.5f);
            walkAmount = 0.6f; // 脚で地面をかく
            if (stateTimer < data.chargeWindupSeconds) return;
            chargeDir = transform.forward;
            travelled = 0f;
            chargeHit.Clear();
            Enter(State.Charging);
        }

        private void TickCharging(float dt)
        {
            walkAmount = 2.2f;
            float step = data.chargeSpeed * SpeedFactor * dt;
            Vector3 next = transform.position + chargeDir * step;
            Vector3 clamped = ClampToArena(next, WallMargin);
            bool atEdge = (Flat(clamped - next)).sqrMagnitude > 0.0001f;
            transform.position = Grounded(clamped);
            travelled += step;
            if (Random.value < 0.5f) EarthChips.Spawn(transform.position, 2.5f, 2);

            Vector3 front = transform.position + chargeDir * data.modelScale * 0.3f;
            HeroesNear(front, data.chargeHitRadius, HitHeight, buffer);
            foreach (var hp in buffer)
            {
                if (!chargeHit.Add(hp)) continue;
                Vector3 lateral = Vector3.Cross(Vector3.up, chargeDir);
                Vector3 away = (chargeDir + lateral * Mathf.Sign(Vector3.Dot(hp.transform.position - transform.position, lateral) + 0.01f) * 0.8f).normalized;
                Hit(hp, data.chargeDamageScale);
                if (hp == null || hp.IsDead) continue;
                SkillKnockback.Play(hp.gameObject, away, data.chargeKnockback);
                ImpactPunch.Play(hp.gameObject, away);
            }
            if (travelled < data.chargeDistance && !atEdge) return;
            ShockRing.Spawn(transform.position + chargeDir * 3f, 0.5f, 5f, 0.35f, 0.8f, new Color(0.95f, 0.75f, 0.45f, 1f));
            Enter(State.ChargeRecover);
        }

        // ---- cross slash

        private void TickSlashWindup()
        {
            if (target != null) FaceTowards(target.position, data.turnSpeed * 1.5f);
            if (stateTimer >= data.slashWindupSeconds) Enter(State.SlashSwing);
        }

        private void TickSlashSwing()
        {
            if (stateTimer < data.slashSwingSeconds) return;
            Vector3 start = transform.position + transform.forward * data.modelScale * 0.45f + Vector3.up * data.slashHalfSize * 0.8f;
            ScorpionCrossSlash.Spawn(gameObject, start, transform.forward, Mathf.RoundToInt(CurrentAtk * data.slashDamageScale), data);
            Enter(State.SlashRecover);
        }

        // ---------------------------------------------------------------- death

        protected override void DeathTick(float t)
        {
            model.localPosition = Vector3.down * Mathf.SmoothStep(0f, data.modelScale * 0.12f, t);
            model.localRotation = Quaternion.Euler(0f, 0f, Mathf.SmoothStep(0f, 18f, t));
        }

        // ---------------------------------------------------------------- pose

        private IdleMotion idleNow;

        protected override void Pose()
        {
            if (IsDead)
            {
                foreach (var s in new[] { "P", "N" })
                {
                    float sg = s == "P" ? side : -side;
                    rig.Rotate("LegFront" + s, Vector3.forward, sg * 25f);
                    rig.Rotate("LegRear" + s, Vector3.forward, sg * 25f);
                }
                rig.Rotate("Tail1", Vector3.right, 25f);
                return;
            }

            // 待機: 体を上下させて息をし、爪をゆっくり開閉し、体ごと少し向きを変えて見回す。
            // 時々しぐさをする（0: 爪をカチカチ鳴らす、1: 尾を高く上げて震わせる、2: 両爪を振り上げて威嚇、3: その場で足踏み）。
            idleNow = Idle(state == State.Idle && walkAmount < 0.1f, 1.6f);
            float shuffle = idleNow.fidgetKind == 3 ? idleNow.fidget * 0.45f : 0f;

            // 脚: 左右と前後で互い違いに、前後へ振って持ち上げる。
            walkPhase += Time.deltaTime * data.stepRate * Mathf.Max(walkAmount + shuffle, 0f) * Mathf.PI * 2f;
            float amount = Mathf.Clamp01(walkAmount + shuffle);
            foreach (var s in new[] { "P", "N" })
            {
                float sg = s == "P" ? side : -side;
                float offset = s == "P" ? 0f : Mathf.PI;
                PoseLeg("LegFront" + s, sg, walkPhase + offset, amount);
                PoseLeg("LegRear" + s, sg, walkPhase + offset + Mathf.PI, amount);
            }

            // 胴: 歩くと少し揺れ、突進では頭を下げる。
            float bob = Mathf.Abs(Mathf.Sin(walkPhase)) * 0.12f * amount;
            float lean = state == State.Charging ? 8f : state == State.ChargeWindup ? -6f : 0f;
            bob += idleNow.breath * 0.04f * idleNow.weight;
            model.localPosition = Vector3.up * bob;
            model.localRotation = Quaternion.Euler(lean + idleNow.lookPitch * 0.25f, idleNow.lookYaw * 0.3f, Mathf.Sin(walkPhase) * 2f * amount + idleNow.shift * 1.5f);

            PoseTail();
            PoseClaws();
        }

        private void PoseLeg(string bone, float sg, float phase, float amount)
        {
            float swing = Mathf.Sin(phase) * 22f * amount;
            float lift = Mathf.Max(0f, Mathf.Cos(phase)) * 16f * amount;
            rig.Rotate(bone, Vector3.up, swing * sg);
            rig.Rotate(bone, Vector3.forward, lift * sg);
        }

        private void PoseTail()
        {
            float sway = Mathf.Sin(Time.time * 1.6f) * 5f;
            float pull = 0f; // 負で後ろへ引く、正で前へ振り下ろす
            if (state == State.StingWindup) pull = -22f * Mathf.SmoothStep(0f, 1f, stateTimer / Mathf.Max(0.01f, data.stingWindupSeconds));
            else if (state == State.StingRecover)
            {
                float k = stateTimer / Mathf.Max(0.01f, data.stingRecoverSeconds);
                pull = k < 0.25f ? Mathf.Lerp(-22f, 30f, k / 0.25f) : Mathf.Lerp(30f, 0f, (k - 0.25f) / 0.75f);
            }
            else if (state == State.Charging) pull = 10f;
            // 待機のしぐさ: 尾を高く持ち上げて、先を小刻みに震わせる。
            float raiseTail = idleNow.fidgetKind == 1 ? idleNow.fidget : 0f;
            pull -= 18f * raiseTail;
            sway *= 1f + idleNow.weight;
            rig.Rotate("Tail1", Vector3.up, sway * 0.5f);
            rig.Rotate("Tail1", Vector3.right, pull * 0.35f);
            rig.Rotate("Tail2", Vector3.right, pull * 0.3f);
            rig.Rotate("Tail3", Vector3.right, pull * 0.3f);
            rig.Rotate("Tail4", Vector3.right, pull * 0.25f + Mathf.Sin(Time.time * 2.1f) * 3f);
            rig.Rotate("Stinger", Vector3.right, pull * 0.4f + Mathf.Sin(Time.time * 30f) * 6f * raiseTail);
        }

        // 爪: 広げる（spread、外向きの水平の回転）と振り上げる（raise）を組み合わせる。
        private void PoseClaws()
        {
            float spread = 0f, raise = 0f, pincer = Mathf.Sin(Time.time * 2.4f) * 6f;
            if (idleNow.weight > 0f)
            {
                // 爪をゆっくり開いて閉じ、見回す向きへ少し寄せる。しぐさでカチカチ鳴らすか、両爪を振り上げる。
                pincer = Mathf.Sin(Time.time * 1.1f) * 9f * idleNow.weight + pincer * (1f - idleNow.weight);
                if (idleNow.fidgetKind == 0) pincer += Mathf.Abs(Mathf.Sin(Time.time * 14f)) * 22f * idleNow.fidget;
                if (idleNow.fidgetKind == 2) { raise += 30f * idleNow.fidget; spread += 26f * idleNow.fidget; pincer += 16f * idleNow.fidget; }
                spread += idleNow.breath * 3f * idleNow.weight;
            }
            switch (state)
            {
                case State.SlashWindup:
                {
                    float k = Mathf.SmoothStep(0f, 1f, stateTimer / Mathf.Max(0.01f, data.slashWindupSeconds));
                    spread = 40f * k;
                    raise = 38f * k;
                    pincer = 20f * k;
                    break;
                }
                case State.SlashSwing:
                {
                    float k = Mathf.Clamp01(stateTimer / Mathf.Max(0.01f, data.slashSwingSeconds));
                    spread = Mathf.Lerp(40f, -28f, k);
                    raise = Mathf.Lerp(38f, -8f, k);
                    pincer = Mathf.Lerp(20f, -5f, k);
                    break;
                }
                case State.SlashRecover:
                {
                    float k = Mathf.SmoothStep(0f, 1f, stateTimer / Mathf.Max(0.01f, data.slashRecoverSeconds));
                    spread = Mathf.Lerp(-28f, 0f, k);
                    raise = Mathf.Lerp(-8f, 0f, k);
                    break;
                }
                case State.ChargeWindup:
                case State.Charging:
                    spread = 18f;
                    raise = 10f;
                    pincer = 18f;
                    break;
            }
            foreach (var s in new[] { "P", "N" })
            {
                float sg = s == "P" ? side : -side;
                rig.Rotate("Arm" + s, Vector3.up, spread * sg);
                rig.Rotate("Arm" + s, Vector3.forward, raise * sg);
                rig.Rotate("Pincer" + s, Vector3.up, pincer * sg);
            }
        }
    }
}
