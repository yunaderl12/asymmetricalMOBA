using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Hero;
using Game.Skill;

namespace Game.Monster
{
    /// 森の広場のウェンディゴ。四つ足で広場を駆けて勇者を追う。
    ///  - 雄叫び: 勇者を初めて見つけたとき（その後は時々）、体を起こして吠える。まわりの森にいる勇者に森の呪い（視界が狭まる）をかける。
    ///  - 大跳躍（離れているとき）: 身をかがめてから、勇者の方へ前に大きく高く跳んで着地する。着地点のまわりの勇者は跳ね飛ばされる。
    ///  - 近くにいるときは近接の破壊攻撃:
    ///    引っ掻き: 片腕を振り上げて前の勇者を爪で薙ぎ、地面を引っ掻いてはがし、前へ瓦礫を扇状に飛ばす。
    ///    叩きつけ: 後ろ足で立ち上がり、両腕を前の地面へ叩きつける。前にいる勇者を大きく吹き飛ばし、地面を砕いて瓦礫をまき散らす。
    public class WendigoBoss : BossBase
    {
        public enum Attack { None, Roar, Pounce, Scratch, Smash }
        private enum State { Idle, Chase, Roar, PounceWindup, PounceAir, PounceRecover, ScratchWindup, ScratchSwing, ScratchRecover, SmashWindup, SmashSlam, SmashRecover }

        private const float WallMargin = 3f;
        private const float StopDistance = 5f;
        private const float RunFacingDegrees = 50f;
        private const float HitHeight = 4f;
        private const float RoarCurseShare = 0.35f;   // 雄叫びのこの時点で呪いをかける
        private const float RoarShake = 0.12f;
        private const float LandShake = 0.22f;
        private const float DisengageSeconds = 8f;
        private const float LandShortShare = 0.4f;      // 勇者の手前で降りる距離（モデルの大きさに対する割合）      // 勇者がこれだけいなければ、次に見つけたときまた吠える

        private WendigoData data;
        private State state = State.Idle;
        private Attack forced = Attack.None;
        private Attack lastAttack = Attack.None;
        private float stateTimer;
        private float attackTimer;
        private float roarTimer;
        private float lostTimer = float.PositiveInfinity;
        private bool roared;
        private bool cursedThisRoar;
        private float runPhase;
        private float runAmount;
        private float side = 1f;           // ArmP がモデルの +X 側なら 1
        private float scratchSide = 1f;    // 引っ掻く腕（1 = P）
        private Vector3 leapFrom;
        private Vector3 leapTo;
        private float leapAir;
        private float leapHeight;
        private readonly List<HealthComponent> buffer = new List<HealthComponent>();
        private readonly List<(string bone, Quaternion fix)> legFix = new List<(string, Quaternion)>();

        public void Force(Attack attack)
        {
            forced = attack;
            attackTimer = 0f;
            if (state == State.Idle && target != null) state = State.Chase;
        }

        protected override void OnInit()
        {
            data = (WendigoData)baseData;
            attackTimer = data.attackInterval;
            if (rig == null) return;
            var arm = rig.Get("ArmP");
            side = arm != null && model.InverseTransformPoint(arm.position).x < 0f ? -1f : 1f;
            BuildLegFix();
        }

        // 元のモデルは後ろ足の片方（N）を後ろへ伸ばしている。もう片方（P）の左右反転になるよう、N の腿とすねを回して直す。
        private void BuildLegFix()
        {
            rig.Begin();
            foreach (var (bone, child) in new[] { ("ThighN", "ShinN"), ("ShinN", "FootN") })
            {
                Transform b = rig.Get(bone), c = rig.Get(child);
                Transform pb = rig.Get(bone.Replace('N', 'P')), pc = rig.Get(child.Replace('N', 'P'));
                if (b == null || c == null || pb == null || pc == null) return;
                Vector3 have = model.InverseTransformDirection(c.position - b.position);
                Vector3 want = model.InverseTransformDirection(pc.position - pb.position);
                want.x = -want.x;
                var fix = Quaternion.FromToRotation(have, want);
                rig.Rotate(bone, fix);
                legFix.Add((bone, fix));
            }
        }

        // ---------------------------------------------------------------- behaviour

        protected override void Tick()
        {
            float dt = Time.deltaTime;
            runAmount = 0f;
            roarTimer -= dt;
            if (target == null) lostTimer += dt;
            else
            {
                if (lostTimer > DisengageSeconds) roared = false;
                lostTimer = 0f;
            }
            if (Stunned) return;
            stateTimer += dt;
            switch (state)
            {
                case State.Idle: TickIdle(dt); break;
                case State.Chase: TickChase(dt); break;
                case State.Roar: TickRoar(); break;
                case State.PounceWindup: TickPounceWindup(); break;
                case State.PounceAir: TickPounceAir(); break;
                case State.PounceRecover: Recover(data.pounceRecoverSeconds); break;
                case State.ScratchWindup: TickScratchWindup(); break;
                case State.ScratchSwing: TickScratchSwing(); break;
                case State.ScratchRecover: Recover(data.scratchRecoverSeconds); break;
                case State.SmashWindup: TickSmashWindup(); break;
                case State.SmashSlam: TickSmashSlam(); break;
                case State.SmashRecover: Recover(data.smashRecoverSeconds); break;
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
            if (target != null)
            {
                // 初めて見つけたら、まず吠える。
                if (!roared) { BeginRoar(); return; }
                Enter(State.Chase);
                return;
            }
            if (Flat(arenaCenter - transform.position).magnitude > 2f) Run(arenaCenter, dt, 0.5f);
        }

        private void TickChase(float dt)
        {
            if (target == null) { Enter(State.Idle); return; }
            if (!roared) { BeginRoar(); return; }
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
            if (d > StopDistance) Run(target.position, dt, 1f);
            else FaceTowards(target.position, data.turnSpeed);
        }

        private void Run(Vector3 goal, float dt, float pace)
        {
            FaceTowards(goal, data.turnSpeed);
            Vector3 to = Flat(goal - transform.position);
            if (Vector3.Angle(transform.forward, to) > RunFacingDegrees) { runAmount = 0.35f; return; }
            float step = Mathf.Min(to.magnitude, data.moveSpeed * pace * SpeedFactor * dt);
            transform.position = Grounded(ClampToArena(transform.position + transform.forward * step, WallMargin));
            runAmount = pace;
        }

        private Vector3 Grounded(Vector3 p)
        {
            p.y = FloorY(p);
            return p;
        }

        private Attack Choose(float d)
        {
            if (forced != Attack.None)
            {
                var f = forced;
                forced = Attack.None;
                return f;
            }
            // 離れていれば前へ大きく跳び、近ければ近接の破壊攻撃（引っ掻きか叩きつけ）。
            var options = new List<Attack>();
            if (roarTimer <= 0f) options.Add(Attack.Roar);
            if (d >= data.pounceMinRange)
            {
                if (d <= data.pounceMaxRange) options.Add(Attack.Pounce);
            }
            else
            {
                options.Add(Attack.Smash);
                if (d <= data.scratchMaxRange) options.Add(Attack.Scratch);
            }
            if (options.Count > 1) options.Remove(lastAttack);
            return options.Count > 0 ? options[Random.Range(0, options.Count)] : Attack.None;
        }

        private void Begin(Attack attack)
        {
            lastAttack = attack;
            switch (attack)
            {
                case Attack.Roar: BeginRoar(); break;
                case Attack.Pounce: Enter(State.PounceWindup); break;
                case Attack.Scratch:
                    scratchSide = -scratchSide;
                    Enter(State.ScratchWindup);
                    break;
                case Attack.Smash: Enter(State.SmashWindup); break;
            }
        }

        // ---- roar

        private void BeginRoar()
        {
            roared = true;
            cursedThisRoar = false;
            roarTimer = data.roarCooldown;
            Enter(State.Roar);
        }

        private void TickRoar()
        {
            if (target != null && stateTimer < data.roarSeconds * 0.3f) FaceTowards(target.position, data.turnSpeed);
            if (!cursedThisRoar && stateTimer >= data.roarSeconds * RoarCurseShare)
            {
                cursedThisRoar = true;
                Vector3 mouth = transform.position + transform.forward * data.modelScale * 0.5f + Vector3.up * data.modelScale * 0.6f;
                ShockRing.Spawn(transform.position + Vector3.up * 0.3f, 1f, data.roarRadius * 0.5f, 0.8f, 1.2f, new Color(0.55f, 0.75f, 0.6f, 0.8f));
                ShockRing.Spawn(mouth, 0.5f, 9f, 0.5f, 0.6f, new Color(0.85f, 0.95f, 0.9f, 0.9f));
                HeroFPSController.ShakeNear(transform.position, RoarShake, data.roarRadius, 1.1f);
                HeroesNear(transform.position, data.roarRadius, 30f, buffer);
                int count = 0;
                foreach (var hp in buffer)
                    if (ForestCurse.Apply(hp.gameObject, data.curseFogStart, data.curseFogEnd, data.curseFogColor)) count++;
                Debug.Log("[Wendigo] roar: cursed " + count + " hero(es)");
            }
            if (stateTimer < data.roarSeconds) return;
            attackTimer = data.attackInterval * 0.5f;
            Enter(target != null ? State.Chase : State.Idle);
        }

        // ---- pounce

        private void TickPounceWindup()
        {
            if (target != null) FaceTowards(target.position, data.turnSpeed * 1.5f);
            if (stateTimer < data.pounceWindupSeconds) return;
            leapFrom = transform.position;
            Vector3 aim = target != null ? target.position : transform.position + transform.forward * data.pounceMaxRange * 0.6f;
            Vector3 reach = Flat(aim - leapFrom);
            // 勇者の真上ではなく、すぐ手前に降りる（勇者が背中に乗らないように）。前足が勇者に届く。
            float shortBy = data.modelScale * LandShortShare;
            reach = reach.magnitude > shortBy ? reach.normalized * (reach.magnitude - shortBy) : Vector3.zero;
            if (reach.magnitude > data.pounceMaxRange) reach = reach.normalized * data.pounceMaxRange;
            leapTo = Grounded(ClampToArena(leapFrom + reach, WallMargin));
            // 遠くへ跳ぶほど高く長く飛ぶ。
            float share = Mathf.Clamp01(Flat(leapTo - leapFrom).magnitude / Mathf.Max(1f, data.pounceMaxRange));
            leapAir = Mathf.Lerp(data.pounceAirSeconds * 0.6f, data.pounceAirSeconds, share);
            leapHeight = Mathf.Lerp(data.pounceHeight * 0.45f, data.pounceHeight, share);
            EarthChips.Spawn(leapFrom, 2.5f, 12);
            MeleeFx.DustBurst(leapFrom, 2.5f, 18, 5f);
            HeroFPSController.ShakeNear(leapFrom, 0.08f, 30f, 0.25f);
            Enter(State.PounceAir);
        }

        private void TickPounceAir()
        {
            float t = Mathf.Clamp01(stateTimer / Mathf.Max(0.05f, leapAir));
            Vector3 p = Vector3.Lerp(leapFrom, leapTo, t);
            p.y += Mathf.Sin(t * Mathf.PI) * leapHeight;
            Vector3 travel = Flat(leapTo - leapFrom);
            if (travel.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(travel), data.turnSpeed * 2f * Time.deltaTime);
            transform.position = p;
            if (t < 1f) return;
            Land();
            Enter(State.PounceRecover);
        }

        private void Land()
        {
            Vector3 at = transform.position + transform.forward * data.modelScale * LandShortShare * 0.7f;
            HeroesNear(at, data.pounceHitRadius, HitHeight, buffer);
            foreach (var hp in buffer)
            {
                Vector3 away = Flat(hp.transform.position - at);
                away = away.sqrMagnitude > 0.01f ? away.normalized : transform.forward;
                Hit(hp, data.pounceDamageScale);
                if (hp == null || hp.IsDead) continue;
                SkillKnockback.Play(hp.gameObject, away, data.pounceKnockback);
                ImpactPunch.Play(hp.gameObject, away);
            }
            GroundDig.Dig(at + transform.forward * data.modelScale * 0.25f, data.pounceHitRadius * 0.6f, 0.3f);
            MeleeFx.DustBurst(at, data.pounceHitRadius * 0.6f, 28, 6f);
            EarthChips.Spawn(at, data.pounceHitRadius * 0.6f, 14);
            ShockRing.Spawn(at + Vector3.up * 0.2f, 0.5f, data.pounceHitRadius * 1.3f, 0.35f, 0.9f, new Color(0.6f, 0.5f, 0.38f, 0.9f));
            HeroFPSController.ShakeNear(at, LandShake, data.pounceHitRadius * 5f, 0.4f);
        }

        // ---- ground scratch

        private void TickScratchWindup()
        {
            if (target != null) FaceTowards(target.position, data.turnSpeed * 1.5f);
            if (stateTimer >= data.scratchWindupSeconds) Enter(State.ScratchSwing);
        }

        private void TickScratchSwing()
        {
            if (stateTimer < data.scratchSwingSeconds) return;
            Vector3 lateral = transform.right * side * scratchSide * data.modelScale * 0.18f;
            Vector3 torn = Grounded(transform.position + transform.forward * data.modelScale * 0.55f + lateral);
            // 爪そのものが前の勇者を薙ぐ。
            FrontHit(transform.position, data.scratchHitRange + data.modelScale * 0.3f, data.scratchHitArc, data.scratchHitDamageScale, data.scratchKnockback);
            GroundDig.Dig(torn, data.scratchDigRadius, data.scratchDigDepth);
            EarthChips.Spawn(torn, data.scratchDigRadius, 16);
            MeleeFx.DustBurst(torn, data.scratchDigRadius, 20, 5f);
            HeroFPSController.ShakeNear(torn, 0.08f, 25f, 0.3f);

            Vector3 aim = target != null ? Flat(target.position - torn) : transform.forward;
            if (aim.sqrMagnitude < 0.01f) aim = transform.forward;
            aim.Normalize();
            int count = Mathf.Max(1, data.debrisCount);
            int damage = Mathf.Max(1, Mathf.RoundToInt(CurrentAtk * data.debrisDamageScale));
            for (int i = 0; i < count; i++)
            {
                float yaw = (count == 1 ? 0f : (i / (float)(count - 1) - 0.5f) * data.debrisSpreadDegrees) + Random.Range(-4f, 4f);
                Vector3 dir = Quaternion.AngleAxis(yaw, Vector3.up) * aim;
                float speed = data.debrisSpeed * Random.Range(0.8f, 1.15f);
                // 届く距離が debrisRange になるくらいの角度で投げる（低く速い）。
                float up = Mathf.Clamp(14f * data.debrisRange / (2f * speed * speed), 0.08f, 0.6f) + Random.Range(0f, 0.12f);
                Vector3 velocity = (dir + Vector3.up * up).normalized * speed;
                Vector3 start = torn + Vector3.up * 0.6f + dir * 0.8f + Random.insideUnitSphere * 0.4f;
                WendigoDebris.Spawn(gameObject, start, velocity, damage, Random.Range(0.45f, 0.95f), data);
            }
            Enter(State.ScratchRecover);
        }

        // ---- smash

        private void TickSmashWindup()
        {
            if (target != null) FaceTowards(target.position, data.turnSpeed * 1.2f);
            if (stateTimer >= data.smashWindupSeconds) Enter(State.SmashSlam);
        }

        private void TickSmashSlam()
        {
            if (stateTimer < data.smashSlamSeconds) return;
            Vector3 at = Grounded(transform.position + transform.forward * data.modelScale * 0.6f);
            FrontHit(transform.position, data.modelScale * 0.6f + data.smashRadius, data.smashArcDegrees, data.smashDamageScale, data.smashKnockback, at, data.smashRadius);
            GroundDig.Dig(at, data.smashDigRadius, data.smashDigDepth);
            EarthChips.Spawn(at, data.smashDigRadius, 26);
            MeleeFx.DustBurst(at, data.smashDigRadius, 36, 7f);
            ShockRing.Spawn(at + Vector3.up * 0.2f, 0.6f, data.smashRadius * 1.4f, 0.4f, 1.0f, new Color(0.55f, 0.45f, 0.33f, 0.95f));
            HeroFPSController.ShakeNear(at, 0.3f, data.smashRadius * 6f, 0.5f);
            int count = Mathf.Max(0, data.smashDebrisCount);
            int damage = Mathf.Max(1, Mathf.RoundToInt(CurrentAtk * data.debrisDamageScale));
            for (int i = 0; i < count; i++)
            {
                float yaw = i * 360f / Mathf.Max(1, count) + Random.Range(-12f, 12f);
                Vector3 dir = Quaternion.AngleAxis(yaw, Vector3.up) * transform.forward;
                float speed = Mathf.Sqrt(14f * data.smashDebrisRange) * Random.Range(0.75f, 1.0f);
                Vector3 velocity = (dir + Vector3.up * Random.Range(0.6f, 1.1f)).normalized * speed;
                WendigoDebris.Spawn(gameObject, at + Vector3.up * 0.8f + dir * 1.2f, velocity, damage, Random.Range(0.5f, 1.1f), data);
            }
            Enter(State.SmashRecover);
        }

        // 前の扇形（from から range m、角度 arc 度）にいる勇者に当てて吹き飛ばす。center と radius があれば、その円の中も当たる。
        private void FrontHit(Vector3 from, float range, float arc, float damageScale, float knockback, Vector3? center = null, float radius = 0f)
        {
            HeroesNear(from, range, HitHeight, buffer);
            foreach (var hp in buffer)
            {
                Vector3 to = Flat(hp.transform.position - from);
                bool inArc = to.sqrMagnitude < 0.01f || Vector3.Angle(transform.forward, to) <= arc * 0.5f;
                bool inCircle = center.HasValue && Flat(hp.transform.position - center.Value).magnitude <= radius;
                if (!inArc && !inCircle) continue;
                Vector3 away = to.sqrMagnitude > 0.01f ? to.normalized : transform.forward;
                Hit(hp, damageScale);
                if (hp == null || hp.IsDead) continue;
                SkillKnockback.Play(hp.gameObject, away, knockback);
                ImpactPunch.Play(hp.gameObject, away);
            }
        }

        // ---------------------------------------------------------------- death

        protected override void DeathTick(float t)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            model.localPosition = Vector3.down * data.modelScale * 0.06f * k;
            model.localRotation = Quaternion.Euler(0f, 0f, 70f * k);
        }

        // ---------------------------------------------------------------- pose

        protected override void Pose()
        {
            foreach (var (bone, fix) in legFix) rig.Rotate(bone, fix);
            if (IsDead)
            {
                rig.Rotate("Neck", Vector3.right, 20f);
                rig.Rotate("Head", Vector3.right, 25f);
                return;
            }

            float time = Time.time;
            runPhase += Time.deltaTime * data.stepRate * Mathf.Max(runAmount, 0f) * Mathf.PI * 2f;
            float run = Mathf.Clamp01(runAmount);
            float drop = 0f;        // 体を沈める量（モデルの大きさに対する割合）
            float chest = Mathf.Sin(time * 1.4f) * 1.5f;  // 正で前へかがむ（息づかい）
            float neck = 0f, head = 0f;
            float armSwing = 0f;    // 正で腕を後ろへ、負で前へ
            float armSpread = 0f;   // 正で外へ開く
            float legSwing = 0f;    // 正で脚を後ろへ
            float scratchRaise = 0f;

            switch (state)
            {
                case State.Roar:
                {
                    float k = stateTimer / Mathf.Max(0.05f, data.roarSeconds);
                    float up = k < 0.25f ? Mathf.SmoothStep(0f, 1f, k / 0.25f) : k > 0.8f ? Mathf.SmoothStep(1f, 0f, (k - 0.8f) / 0.2f) : 1f;
                    chest -= 26f * up;
                    neck -= 12f * up;
                    head -= 28f * up - (k > 0.3f && k < 0.8f ? Mathf.Sin(time * 34f) * 4f : 0f);
                    armSpread = 38f * up;
                    armSwing = -35f * up;
                    legSwing = -10f * up;
                    break;
                }
                case State.PounceWindup:
                {
                    float k = Mathf.SmoothStep(0f, 1f, stateTimer / Mathf.Max(0.05f, data.pounceWindupSeconds));
                    drop = 0.07f * k;
                    chest += 10f * k;
                    head -= 8f * k;
                    armSwing = 22f * k;
                    legSwing = -18f * k;
                    break;
                }
                case State.PounceAir:
                {
                    float k = Mathf.Clamp01(stateTimer / Mathf.Max(0.05f, leapAir));
                    chest -= Mathf.Lerp(18f, 4f, k);
                    head -= 10f;
                    armSwing = Mathf.Lerp(-60f, -30f, k);
                    armSpread = 14f;
                    legSwing = Mathf.Lerp(45f, 20f, k);
                    break;
                }
                case State.PounceRecover:
                {
                    float k = Mathf.SmoothStep(0f, 1f, stateTimer / Mathf.Max(0.05f, data.pounceRecoverSeconds));
                    drop = 0.08f * (1f - k);
                    chest += 12f * (1f - k);
                    armSwing = -12f * (1f - k);
                    armSpread = 20f * (1f - k);
                    break;
                }
                case State.ScratchWindup:
                {
                    float k = Mathf.SmoothStep(0f, 1f, stateTimer / Mathf.Max(0.05f, data.scratchWindupSeconds));
                    chest -= 14f * k;
                    scratchRaise = k;
                    break;
                }
                case State.ScratchSwing:
                {
                    float k = Mathf.Clamp01(stateTimer / Mathf.Max(0.05f, data.scratchSwingSeconds));
                    chest += Mathf.Lerp(-14f, 12f, k);
                    scratchRaise = Mathf.Lerp(1f, -0.6f, k);
                    break;
                }
                case State.ScratchRecover:
                {
                    float k = Mathf.SmoothStep(0f, 1f, stateTimer / Mathf.Max(0.05f, data.scratchRecoverSeconds));
                    chest += 12f * (1f - k);
                    scratchRaise = -0.6f * (1f - k);
                    break;
                }
            }

            // 待機: 息をして、あたりを見回す。時々しぐさをする（0,1: 地面のにおいをかぐ、2: 頭を振る、3: 前足で地面をかく）。
            var idle = Idle(state == State.Idle && runAmount < 0.1f, 1.8f);
            float idleYaw = 0f, idleRoll = 0f, paw = 0f;
            if (idle.weight > 0f)
            {
                float sniff = idle.fidgetKind <= 1 ? idle.fidget : 0f;
                chest += idle.breath * 2.5f * idle.weight + 6f * sniff;
                neck += 10f * sniff;
                head += idle.lookPitch + 14f * sniff + Mathf.Sin(time * 22f) * 3.5f * sniff;
                idleYaw = idle.lookYaw * (1f - sniff) + (idle.fidgetKind == 2 ? Mathf.Sin(time * 15f) * 16f * idle.fidget : 0f);
                armSpread += (idle.breath * 2f + 3f) * idle.weight;
                idleRoll = idle.shift * 2.5f;
                paw = idle.fidgetKind == 3 ? idle.fidget : 0f;
            }

            // 叩きつけ: 後ろ足で立ち上がって両腕を高く上げ、前へ振り下ろす。
            float rear = 0f;
            if (state == State.SmashWindup)
            {
                float k = Mathf.SmoothStep(0f, 1f, stateTimer / Mathf.Max(0.05f, data.smashWindupSeconds));
                rear = k;
                chest -= 30f * k;
                head -= 12f * k;
                armSwing = -110f * k;
                armSpread = 16f * k;
                legSwing = -14f * k;
            }
            else if (state == State.SmashSlam)
            {
                float k = Mathf.Clamp01(stateTimer / Mathf.Max(0.05f, data.smashSlamSeconds));
                rear = 1f - k;
                chest += Mathf.Lerp(-30f, 18f, k);
                armSwing = Mathf.Lerp(-110f, -15f, k);
                armSpread = 16f * (1f - k);
                drop = 0.05f * k;
            }
            else if (state == State.SmashRecover)
            {
                float k = Mathf.SmoothStep(0f, 1f, stateTimer / Mathf.Max(0.05f, data.smashRecoverSeconds));
                chest += 18f * (1f - k);
                armSwing = -15f * (1f - k);
                drop = 0.05f * (1f - k);
            }

            // 駆け足: 前足と後ろ足が対角で組になって、前後へ大きく振る。
            float bob = Mathf.Abs(Mathf.Sin(runPhase)) * 0.025f * run;
            model.localPosition = Vector3.up * data.modelScale * (bob - drop + 0.07f * rear) - Vector3.forward * data.modelScale * 0.08f * rear;
            model.localRotation = Quaternion.Euler(Mathf.Sin(runPhase * 2f) * 3f * run - 24f * rear + idle.breath * 0.6f * idle.weight, 0f, Mathf.Sin(runPhase) * 2f * run + idleRoll);

            rig.Rotate("Chest", Vector3.right, chest);
            if (target != null && state == State.Chase)
            {
                // 首を勇者の方へ少し向ける。
                Vector3 local = model.InverseTransformDirection(Flat(target.position - transform.position));
                float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -30f, 30f);
                rig.Rotate("Neck", Vector3.up, yaw * 0.5f);
                rig.Rotate("Head", Vector3.up, yaw * 0.5f);
            }
            if (idleYaw != 0f)
            {
                rig.Rotate("Neck", Vector3.up, idleYaw * 0.45f);
                rig.Rotate("Head", Vector3.up, idleYaw * 0.55f);
            }
            rig.Rotate("Neck", Vector3.right, neck);
            rig.Rotate("Head", Vector3.right, head + Mathf.Sin(time * 0.9f) * 2f);

            foreach (var s in new[] { "P", "N" })
            {
                float sg = s == "P" ? side : -side;
                float offset = s == "P" ? 0f : Mathf.PI;
                float stride = Mathf.Sin(runPhase + offset);
                float lift = Mathf.Max(0f, Mathf.Cos(runPhase + offset));
                bool scratching = scratchRaise != 0f && (s == "P") == (scratchSide > 0f);
                float arm = armSwing + stride * 30f * run;
                float fore = -lift * 30f * run;
                float spread = armSpread;
                if (paw > 0f && s == "P")
                {
                    // 前足で地面をかく: 持ち上げて前へ出し、後ろへかき戻す。
                    float stroke = Mathf.Sin(time * 9f);
                    arm += -28f * paw + stroke * 12f * paw;
                    fore += -30f * paw;
                }
                if (scratching)
                {
                    // 引っ掻く腕: 前へ高く振り上げ、振り下ろして地面を後ろへかく。
                    arm += scratchRaise > 0f ? -85f * scratchRaise : -60f * scratchRaise;
                    fore += scratchRaise > 0f ? -40f * scratchRaise : 25f * scratchRaise;
                    spread += 12f * Mathf.Abs(scratchRaise);
                }
                rig.Rotate("Arm" + s, Vector3.right, arm);
                rig.Rotate("Arm" + s, Vector3.forward, spread * sg);
                rig.Rotate("Fore" + s, Vector3.right, fore);

                float leg = legSwing - Mathf.Sin(runPhase + offset + Mathf.PI) * 26f * run;
                float legLift = Mathf.Max(0f, Mathf.Cos(runPhase + offset + Mathf.PI)) * 30f * run;
                rig.Rotate("Thigh" + s, Vector3.right, leg);
                rig.Rotate("Shin" + s, Vector3.right, legLift);
            }
        }
    }
}
