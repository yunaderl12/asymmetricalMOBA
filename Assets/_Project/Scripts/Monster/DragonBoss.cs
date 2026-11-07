using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Skill;

namespace Game.Monster
{
    /// 山の広場のドラゴン。地上には降りず、広場の上を旋回しながら、
    ///  - 炎: 勇者の斜め上で止まり、首を反らしてから円すい形の炎を吐く。炎は勇者をゆっくり追う。
    ///  - 急降下: 勇者の足元に赤い輪を出し、その地点へまっすぐ突っ込む。着地の衝撃で周りを吹き飛ばし、しばらく地上に残る（近接の勇者が殴れる隙）。
    /// を交互に使う。
    public class DragonBoss : BossBase
    {
        public enum Attack { None, Breath, Dive }
        private enum State { Circle, BreathMove, BreathWindup, Breathing, DiveTelegraph, Diving, Grounded, Climb, Landing, Resting }

        private const float RestAfterSeconds = 4f;   // 勇者がこれだけいなければ広場に降りて休む
        private const float WakeSeconds = 0.7f;      // 休んでいて勇者に気づいてから飛び立つまで

        private const float WallMargin = 6f;
        private const float MoveTimeout = 2.5f;
        private const float TelegraphTrackShare = 0.7f; // 予告のこの割合までは狙いを勇者に合わせ直す
        private const float TelegraphRingInterval = 0.32f;
        private const float HeroChestHeight = 1.2f;
        private const float HitHeight = 5f;
        private static readonly Color WarnColor = new Color(1f, 0.18f, 0.08f, 1f);

        private DragonData data;
        private State state = State.Circle;
        private Attack nextAttack = Attack.Breath;
        private Attack forced = Attack.None;
        private float stateTimer;
        private float attackTimer;
        private float circleAngle;
        private float flapPhase;
        private float flapRate;
        private float bank;
        private Vector3 velocity;
        private Vector3 lastPosition;
        private Vector3 hoverPoint;
        private Vector3 divePoint;
        private Vector3 diveStart;
        private Vector3 breathDir;
        private float nextTick;
        private float ringTimer;
        private float restTimer;
        private float wakeTimer = -1f;
        private float wingSide = 1f;  // WingP がモデルの +X 側なら 1
        private float legSide = 1f;
        private Transform head;
        private ParticleSystem flame;
        private ParticleSystem ember;
        private Light mouthLight;
        private readonly List<HealthComponent> buffer = new List<HealthComponent>();
        private readonly HashSet<HealthComponent> burned = new HashSet<HealthComponent>();

        /// デバッグ用: 次に使う技を決める。
        public void Force(Attack attack)
        {
            forced = attack;
            attackTimer = 0f;
        }

        protected override void OnInit()
        {
            data = (DragonData)baseData;
            flapRate = data.flapRate;
            circleAngle = Random.value * Mathf.PI * 2f;
            transform.position = CirclePoint(circleAngle);
            lastPosition = transform.position;
            attackTimer = data.attackInterval;
            if (rig != null)
            {
                head = rig.Get("Head");
                wingSide = SideOf("WingP");
                legSide = SideOf("LegFrontP");
            }
            BuildFlame();
            IgnoreHeroBodies();
        }

        // 真上から降りてくるので、勇者が体の上に乗ってしまわないよう、勇者の体とはぶつからないようにする（攻撃は当たる）。
        private void IgnoreHeroBodies()
        {
            var own = GetComponent<Collider>();
            if (own == null) return;
            foreach (var hero in GameObject.FindGameObjectsWithTag("Player"))
                foreach (var c in hero.GetComponentsInChildren<CharacterController>(true))
                    Physics.IgnoreCollision(own, c, true);
        }

        private float SideOf(string bone)
        {
            var t = rig.Get(bone);
            return t != null && model.InverseTransformPoint(t.position).x < 0f ? -1f : 1f;
        }

        // ---------------------------------------------------------------- behaviour

        protected override void Tick()
        {
            float dt = Time.deltaTime;
            if (Stunned)
            {
                // 空中で羽ばたいたまま止まる。地上ならそのまま。
                StopBreath();
                velocity = Vector3.zero;
                return;
            }
            stateTimer += dt;
            switch (state)
            {
                case State.Circle: TickCircle(dt); break;
                case State.BreathMove: TickBreathMove(dt); break;
                case State.BreathWindup: TickBreathWindup(); break;
                case State.Breathing: TickBreathing(dt); break;
                case State.DiveTelegraph: TickDiveTelegraph(dt); break;
                case State.Diving: TickDiving(dt); break;
                case State.Grounded: TickGrounded(); break;
                case State.Climb: TickClimb(dt); break;
                case State.Landing: TickLanding(dt); break;
                case State.Resting: TickResting(dt); break;
            }
            velocity = (transform.position - lastPosition) / Mathf.Max(dt, 1e-4f);
            lastPosition = transform.position;
        }

        private void Enter(State next)
        {
            state = next;
            stateTimer = 0f;
        }

        private Vector3 CirclePoint(float angle)
        {
            var p = arenaCenter + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * data.circleRadius;
            p.y = arenaCenter.y + data.circleAltitude;
            return p;
        }

        private void TickCircle(float dt)
        {
            flapRate = data.flapRate;
            float speed = data.flySpeed * SpeedFactor;
            circleAngle += speed / Mathf.Max(1f, data.circleRadius) * dt;
            Vector3 goal = CirclePoint(circleAngle);
            MoveTowards(goal, speed * 1.4f, dt);
            Vector3 heading = Flat(goal - transform.position);
            if (heading.sqrMagnitude < 0.01f) heading = new Vector3(-Mathf.Sin(circleAngle), 0f, Mathf.Cos(circleAngle));
            FaceTowards(transform.position + heading, 140f);

            if (target == null)
            {
                attackTimer = Mathf.Max(attackTimer, 0.6f);
                restTimer += dt;
                if (restTimer >= RestAfterSeconds) Enter(State.Landing);
                return;
            }
            restTimer = 0f;
            attackTimer -= dt;
            if (attackTimer > 0f) return;
            var attack = forced != Attack.None ? forced : nextAttack;
            forced = Attack.None;
            nextAttack = attack == Attack.Breath ? Attack.Dive : Attack.Breath;
            if (attack == Attack.Breath) BeginBreath(); else BeginDive();
        }

        private void MoveTowards(Vector3 goal, float speed, float dt)
        {
            transform.position = Vector3.MoveTowards(transform.position, goal, speed * dt);
        }

        // ---- fire breath

        private void BeginBreath()
        {
            Vector3 away = Flat(transform.position - target.position);
            if (away.sqrMagnitude < 1f) away = -transform.forward;
            hoverPoint = ClampToArena(target.position + away.normalized * data.breathDistance, WallMargin);
            hoverPoint.y = arenaCenter.y + data.breathAltitude;
            Enter(State.BreathMove);
        }

        private void TickBreathMove(float dt)
        {
            flapRate = data.flapRate * 1.3f;
            MoveTowards(hoverPoint, data.flySpeed * 1.3f * SpeedFactor, dt);
            if (target != null) FaceTowards(target.position, 200f);
            if ((transform.position - hoverPoint).sqrMagnitude < 0.5f || stateTimer > MoveTimeout) Enter(State.BreathWindup);
        }

        private void TickBreathWindup()
        {
            flapRate = data.flapRate * 1.6f;
            if (target != null) FaceTowards(target.position, 160f);
            if (ember != null && !ember.isPlaying) ember.Play();
            if (stateTimer < data.breathWindupSeconds) return;
            breathDir = AimAt(target != null ? target.position : transform.position + transform.forward * 10f - Vector3.up * 8f);
            nextTick = 0f;
            burned.Clear();
            flame.Play();
            Enter(State.Breathing);
        }

        private Vector3 Mouth()
        {
            if (head == null) return transform.position + transform.forward * data.modelScale * 0.25f;
            return head.position + model.forward * data.modelScale * 0.09f - model.up * data.modelScale * 0.02f;
        }

        private Vector3 AimAt(Vector3 point)
        {
            Vector3 d = point + Vector3.up * HeroChestHeight * 0.5f - Mouth();
            return d.sqrMagnitude > 0.01f ? d.normalized : transform.forward;
        }

        private void TickBreathing(float dt)
        {
            flapRate = data.flapRate * 1.6f;
            if (target != null)
            {
                FaceTowards(target.position, data.breathTurnSpeed * 1.5f);
                breathDir = Vector3.RotateTowards(breathDir, AimAt(target.position), data.breathTurnSpeed * Mathf.Deg2Rad * dt, 0f);
            }
            if (stateTimer >= nextTick)
            {
                nextTick += data.breathTickSeconds;
                BurnCone();
            }
            if (stateTimer < data.breathSeconds) return;
            StopBreath();
            attackTimer = data.attackInterval;
            Enter(State.Circle);
        }

        private void BurnCone()
        {
            Vector3 mouth = Mouth();
            HeroesNear(mouth, data.breathRange, 999f, buffer);
            foreach (var hp in buffer)
            {
                Vector3 to = hp.transform.position + Vector3.up * HeroChestHeight - mouth;
                if (to.magnitude > data.breathRange || Vector3.Angle(breathDir, to) > data.breathHalfAngle + Mathf.Atan2(1f, to.magnitude) * Mathf.Rad2Deg) continue;
                // 岩や壁の陰に隠れた勇者には届かない。
                if (Physics.Linecast(mouth, hp.transform.position + Vector3.up * HeroChestHeight, out var block, ~0, QueryTriggerInteraction.Ignore)
                    && block.collider.GetComponentInParent<HealthComponent>() == null && block.collider.name != "Field_TreeColliders") continue;
                Hit(hp, data.breathDamageScale);
                if (hp == null || hp.IsDead) continue;
                StatusEffectController.Apply(hp.gameObject, StatusEffectType.DamageOverTime, data.burnSeconds, data.burnDamagePerTick, gameObject);
                if (burned.Add(hp)) FireHitEffect.PlayBurn(hp.gameObject, data.burnSeconds);
            }
        }

        private void StopBreath()
        {
            if (flame != null && flame.isPlaying) flame.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (ember != null && ember.isPlaying) ember.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        // ---- dive

        private void BeginDive()
        {
            divePoint = GroundPointUnder(target.position);
            ringTimer = 0f;
            Enter(State.DiveTelegraph);
        }

        private Vector3 GroundPointUnder(Vector3 p)
        {
            p = ClampToArena(p, 2f);
            return new Vector3(p.x, FloorY(p), p.z);
        }

        // 狙った地点の後ろ上へ移りながら、地面に縮んでいく赤い輪を何度も出して落ちる場所を知らせる。
        private void TickDiveTelegraph(float dt)
        {
            flapRate = data.flapRate * 1.5f;
            if (target != null && stateTimer < data.diveTelegraphSeconds * TelegraphTrackShare) divePoint = GroundPointUnder(target.position);
            Vector3 back = Flat(transform.position - divePoint);
            if (back.sqrMagnitude < 1f) back = -transform.forward;
            Vector3 perch = ClampToArena(divePoint + back.normalized * 16f, WallMargin);
            perch.y = divePoint.y + data.circleAltitude;
            MoveTowards(perch, data.flySpeed * 1.5f * SpeedFactor, dt);
            FaceTowards(divePoint, 220f);

            ringTimer -= dt;
            if (ringTimer <= 0f)
            {
                ringTimer = TelegraphRingInterval;
                ShockRing.Spawn(divePoint, data.diveImpactRadius * 1.15f, data.diveImpactRadius * 0.15f, TelegraphRingInterval * 1.6f, 0.9f, WarnColor);
            }
            if (stateTimer < data.diveTelegraphSeconds) return;
            diveStart = transform.position;
            Enter(State.Diving);
        }

        private float LandedHeight => data.modelScale * 0.22f; // 原点（胴の中ほど）から足までの高さ

        private void TickDiving(float dt)
        {
            Vector3 goal = divePoint + Vector3.up * LandedHeight;
            transform.position = Vector3.MoveTowards(transform.position, goal, data.diveSpeed * dt);
            FaceTowards(divePoint, 360f);
            if ((transform.position - goal).sqrMagnitude > 0.01f) return;
            Impact();
            Enter(State.Grounded);
        }

        private void Impact()
        {
            HeroesNear(divePoint, data.diveImpactRadius, HitHeight, buffer);
            foreach (var hp in buffer)
            {
                Vector3 away = Flat(hp.transform.position - divePoint);
                if (away.sqrMagnitude < 0.01f) away = transform.forward;
                Hit(hp, data.diveDamageScale);
                if (hp == null || hp.IsDead) continue;
                SkillKnockback.Play(hp.gameObject, away, data.diveKnockback);
                ImpactPunch.Play(hp.gameObject, away);
            }
            ShockRing.Spawn(divePoint, 0.5f, data.diveImpactRadius * 1.4f, 0.45f, 1.2f, new Color(1f, 0.65f, 0.3f, 1f));
            EarthChips.Spawn(divePoint, data.diveImpactRadius * 0.7f, 26);
            GroundCracks.Spawn(divePoint, data.diveImpactRadius, 1.6f);
        }

        private void TickGrounded()
        {
            flapRate = data.flapRate * 0.35f;
            if (target != null) FaceTowards(target.position, 60f);
            if (stateTimer >= data.diveGroundSeconds) Enter(State.Climb);
        }

        private void TickClimb(float dt)
        {
            flapRate = data.flapRate * 1.8f;
            circleAngle = Mathf.Atan2(transform.position.z - arenaCenter.z, transform.position.x - arenaCenter.x);
            Vector3 goal = CirclePoint(circleAngle);
            float speed = Vector3.Distance(diveStart, goal) / Mathf.Max(0.2f, data.diveClimbSeconds) + 2f;
            MoveTowards(goal, speed, dt);
            if ((transform.position - goal).sqrMagnitude > 0.5f && stateTimer < data.diveClimbSeconds * 1.5f) return;
            attackTimer = data.attackInterval;
            Enter(State.Circle);
        }

        // ---- rest (勇者がいないあいだ)

        // 広場の真ん中へゆっくり降りる。途中で勇者が来たら、また舞い上がる。
        private void TickLanding(float dt)
        {
            flapRate = data.flapRate * 1.1f;
            if (target != null) { TakeOff(); return; }
            Vector3 ground = new Vector3(arenaCenter.x, FloorY(arenaCenter) + LandedHeight, arenaCenter.z);
            Vector3 flat = Flat(ground - transform.position);
            Vector3 goal = flat.magnitude > 2f ? new Vector3(ground.x, Mathf.Max(ground.y, transform.position.y - dt * 4f), ground.z) : ground;
            MoveTowards(goal, data.flySpeed * 0.8f * SpeedFactor, dt);
            if (flat.sqrMagnitude > 1f) FaceTowards(ground, 120f);
            if ((transform.position - ground).sqrMagnitude > 0.04f) return;
            EarthChips.Spawn(ground - Vector3.up * LandedHeight, 3f, 10);
            wakeTimer = -1f;
            Enter(State.Resting);
        }

        private void TickResting(float dt)
        {
            flapRate = data.flapRate * 0.25f;
            restTimer = 0f;
            if (target == null) { wakeTimer = -1f; return; }
            if (wakeTimer < 0f) wakeTimer = 0f;
            wakeTimer += dt;
            FaceTowards(target.position, 90f);
            if (wakeTimer >= WakeSeconds) TakeOff();
        }

        private void TakeOff()
        {
            restTimer = 0f;
            wakeTimer = -1f;
            diveStart = transform.position;
            attackTimer = Mathf.Max(attackTimer, data.attackInterval * 0.5f);
            Enter(State.Climb);
        }

        // ---------------------------------------------------------------- death

        protected override void OnDeathStart()
        {
            StopBreath();
        }

        // 力なく広場へ落ちる。
        protected override void DeathTick(float t)
        {
            float floor = FloorY(transform.position) + LandedHeight * 0.6f;
            Vector3 p = transform.position;
            p.y = Mathf.MoveTowards(p.y, floor, Mathf.Lerp(4f, 30f, t) * Time.deltaTime);
            transform.position = p;
            model.localRotation = Quaternion.Euler(Mathf.Lerp(0f, 20f, t), 0f, Mathf.Lerp(0f, 35f, t));
        }

        // ---------------------------------------------------------------- pose

        protected override void Pose()
        {
            if (IsDead) { PoseWings(-30f, 10f, 0f); return; }
            bool resting = state == State.Resting;
            bool grounded = (state == State.Grounded || resting) && !Stunned;
            bool diving = state == State.Diving;
            flapPhase += Time.deltaTime * flapRate * (diving ? 0f : 1f) * Mathf.PI * 2f;

            // 胴の傾き: 旋回では内側へ傾き、急降下では頭を下げる。
            Vector3 localVel = transform.InverseTransformDirection(velocity);
            float targetBank = Mathf.Clamp(-localVel.x * 1.6f, -30f, 30f);
            bank = Mathf.Lerp(bank, targetBank, 1f - Mathf.Exp(-4f * Time.deltaTime));
            float pitch = diving ? 35f : state == State.Climb ? -20f : Mathf.Clamp(-velocity.y * 1.2f, -20f, 20f);
            float bob = grounded ? 0f : Mathf.Sin(flapPhase) * 0.25f;
            // 休んでいるとき: 翼をたたんで息をし、首を回して見回す。
            // 時々しぐさをする（0,1: 翼を大きく広げて伸びをする、2: 首を振って鼻を鳴らす、3: 尾を大きく振る）。
            var idle = Idle(resting && wakeTimer < 0f, 2.2f);
            float stretch = idle.fidgetKind <= 1 ? idle.fidget : 0f;
            float snort = idle.fidgetKind == 2 ? idle.fidget : 0f;
            float swish = idle.fidgetKind == 3 ? idle.fidget : 0f;
            if (resting) bob = -idle.breath * 0.06f * data.modelScale / 14f;
            model.localRotation = Quaternion.Euler(pitch + idle.breath * 0.8f * idle.weight, 0f, bank + idle.shift * 1.5f);
            model.localPosition = Vector3.up * -bob;

            if (diving) PoseWings(-35f, -40f, 0f);           // 翼を後ろへたたむ
            else if (resting)
            {
                // たたんだ翼（息に合わせてわずかに動く）。伸びのときは大きく広げてゆっくり戻す。
                float fold = Mathf.Lerp(1f, 0f, Mathf.Clamp01(wakeTimer < 0f ? 0f : wakeTimer / WakeSeconds));
                PoseWings(Mathf.Lerp(15f, -28f, fold) + 58f * stretch + idle.breath * 2f, Mathf.Lerp(0f, 12f, fold) + 30f * stretch, 2f + 12f * stretch);
            }
            else if (grounded) PoseWings(15f, 0f, 14f);      // 地上ではゆっくり
            else PoseWings(10f, 8f, 38f);

            float tuck = grounded ? 0f : diving ? 80f : 62f;
            foreach (var s in new[] { "P", "N" })
            {
                rig.Rotate("LegFront" + s, Vector3.right, tuck);
                rig.Rotate("LegBack" + s, Vector3.right, tuck + 10f);
            }

            float sway = Mathf.Sin(Time.time * 1.3f) * 9f * (1f + idle.weight * 0.4f) + Mathf.Sin(Time.time * 5f) * 22f * swish;
            rig.Rotate("Tail1", Vector3.up, sway * 0.6f);
            rig.Rotate("Tail2", Vector3.up, sway);
            rig.Rotate("Tail3", Vector3.up, sway * 1.4f);
            if (!grounded) rig.Rotate("Tail1", Vector3.right, -8f);

            float neck = 0f;
            if (state == State.BreathWindup) neck = -28f * Mathf.SmoothStep(0f, 1f, stateTimer / Mathf.Max(0.01f, data.breathWindupSeconds));
            else if (state == State.Breathing) neck = Mathf.Clamp(Vector3.SignedAngle(Flat(model.forward), breathDir, model.right) , -10f, 75f) - pitch;
            else if (diving) neck = -15f;
            if (idle.weight > 0f)
            {
                float shake = Mathf.Sin(Time.time * 14f) * 14f * snort;
                rig.Rotate("Neck", Vector3.up, idle.lookYaw * 0.5f + shake * 0.4f);
                rig.Rotate("Head", Vector3.up, idle.lookYaw * 0.5f + shake * 0.6f);
                neck += idle.lookPitch - 12f * stretch + idle.breath * 2f * idle.weight;
            }
            rig.Rotate("Neck", Vector3.right, neck * 0.5f);
            rig.Rotate("Head", Vector3.right, neck * 0.5f);

            if (flame != null)
            {
                flame.transform.SetPositionAndRotation(Mouth(), Quaternion.LookRotation(state == State.Breathing ? breathDir : model.forward));
                ember.transform.position = Mouth();
                if (mouthLight != null)
                {
                    bool lit = state == State.Breathing || state == State.BreathWindup;
                    mouthLight.enabled = lit;
                    mouthLight.intensity = state == State.Breathing ? 9f + Mathf.PerlinNoise(Time.time * 12f, 0f) * 6f : 3f;
                }
            }
        }

        /// 羽ばたき。rootBias / tipBias は角度の中心（正で上）、amp は振れ幅。
        private void PoseWings(float rootBias, float tipBias, float amp)
        {
            float s = Mathf.Sin(flapPhase);
            float lag = Mathf.Sin(flapPhase - 0.7f);
            float root = rootBias + s * amp;
            float tip = tipBias + lag * amp * 0.6f;
            rig.Rotate("WingP", Vector3.forward, root * wingSide);
            rig.Rotate("WingTipP", Vector3.forward, tip * wingSide);
            rig.Rotate("WingN", Vector3.forward, -root * wingSide);
            rig.Rotate("WingTipN", Vector3.forward, -tip * wingSide);
        }

        // ---------------------------------------------------------------- fire

        private void BuildFlame()
        {
            float scale = data.modelScale / 14f;
            flame = FireballVfx.NewParticles("DragonBreath", transform);
            var main = flame.main;
            main.loop = true;
            main.duration = 1f;
            float life = 0.55f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.8f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(data.breathRange / life * 0.85f, data.breathRange / life);
            main.startSize = new ParticleSystem.MinMaxCurve(0.9f * scale, 1.6f * scale);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.maxParticles = 600;
            main.startColor = new Color(1f, 0.85f, 0.45f);
            var emission = flame.emission;
            emission.rateOverTime = 170f;
            var shape = flame.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = data.breathHalfAngle * 0.8f;
            shape.radius = 0.4f * scale;
            var size = flame.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.5f, 2.6f), new Keyframe(1f, 4.2f)));
            var color = flame.colorOverLifetime;
            color.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.95f, 0.7f), 0f), new GradientColorKey(new Color(1f, 0.55f, 0.12f), 0.35f), new GradientColorKey(new Color(0.75f, 0.12f, 0.04f), 0.75f), new GradientColorKey(new Color(0.15f, 0.08f, 0.06f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.6f), new GradientAlphaKey(0f, 1f) });
            color.color = g;
            var rot = flame.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
            var noise = flame.noise;
            noise.enabled = true;
            noise.strength = 1.5f;
            noise.frequency = 0.6f;
            flame.gameObject.SetActive(true);

            ember = FireballVfx.NewParticles("DragonEmber", transform);
            var em = ember.main;
            em.loop = true;
            em.startLifetime = 0.5f;
            em.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
            em.startSize = new ParticleSystem.MinMaxCurve(0.25f * scale, 0.6f * scale);
            em.startColor = new Color(1f, 0.6f, 0.15f);
            em.gravityModifier = -0.3f;
            var eem = ember.emission;
            eem.rateOverTime = 40f;
            var esh = ember.shape;
            esh.enabled = true;
            esh.shapeType = ParticleSystemShapeType.Sphere;
            esh.radius = 0.4f * scale;
            ember.gameObject.SetActive(true);

            var lightGo = new GameObject("MouthLight");
            lightGo.transform.SetParent(flame.transform, false);
            lightGo.transform.localPosition = Vector3.forward * 3f;
            mouthLight = lightGo.AddComponent<Light>();
            mouthLight.type = LightType.Point;
            mouthLight.color = new Color(1f, 0.55f, 0.2f);
            mouthLight.range = 22f;
            mouthLight.enabled = false;
        }
    }
}
