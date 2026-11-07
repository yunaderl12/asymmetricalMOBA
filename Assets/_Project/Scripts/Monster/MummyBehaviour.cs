using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Skill;

namespace Game.Monster
{
    /// 砂漠のミイラ。ゾンビのように体を左右に揺らし、足を引きずってよろよろ歩く（速さが波打つ）。
    /// 技1: 離れた勇者へ包帯を一本まっすぐ飛ばす。当たると勇者を引き寄せて少しのあいだ動けなくし、そのまま両手でつかみかかる。
    /// 技2: 近くの勇者には両手を振り上げてつかみかかる（正面の扇形）。
    public class MummyBehaviour : MonsterBehaviour
    {
        private enum Phase { Free, BandageWindup, BandageFly, BandagePull, BandageBack, ClawWindup, ClawStrike, ClawRecover }

        private const float BandageWindupSeconds = 0.5f;
        private const float BandageHitRadius = 0.55f;
        private const float BandageBackSeconds = 0.25f;
        private const float PullSeconds = 0.45f;
        private const float ClawWindupSeconds = 0.45f;
        private const float ClawStrikeSeconds = 0.15f;
        private const float ClawRecoverSeconds = 0.55f;
        private const float ClawArcDegrees = 110f;
        private const float AimDegrees = 20f;
        private static readonly Color Cloth = new Color(0.93f, 0.89f, 0.76f);
        private static Material lineMaterial;

        private Phase phase = Phase.Free;
        private float phaseTime;
        private float grabCooldown = 2f;
        private Transform aimed;
        private HealthComponent grabbed;
        private Vector3 tip;
        private Vector3 flyDirection;
        private float flown;
        private LineRenderer bandage;
        private float stride;
        private float sway;

        protected override void Tick(float dt)
        {
            phaseTime += dt;
            if (grabCooldown > 0f) grabCooldown -= dt;
            // よろめき歩き: 足を出すたびに速くなり、引きずるあいだは遅くなる。
            stride += dt * (2.2f + 2.4f * Pace);
            float lurch = (Mathf.Sin(stride * 2f) + 1f) * 0.5f;
            monster.SetSpeedScale(Mathf.Lerp(data.lurchSpeedScale.x, data.lurchSpeedScale.y, lurch * lurch));
            sway = Mathf.Sin(stride);

            var target = Target;
            switch (phase)
            {
                case Phase.Free:
                    if (target == null) return;
                    float dist = FlatTo(target.position).magnitude;
                    if (cooldown <= 0f && dist <= data.clawRange)
                    {
                        aimed = target;
                        Begin(Phase.ClawWindup);
                        return;
                    }
                    if (grabCooldown <= 0f && dist >= data.grabMinRange && dist <= data.grabRange)
                    {
                        FaceTowards(target.position);
                        if (Vector3.Angle(transform.forward, FlatTo(target.position)) > AimDegrees) return;
                        aimed = target;
                        Begin(Phase.BandageWindup);
                    }
                    break;
                case Phase.BandageWindup:
                    if (aimed != null) FaceTowards(aimed.position, 300f);
                    if (phaseTime < BandageWindupSeconds) return;
                    tip = Hand();
                    Vector3 aim = aimed != null ? Chest(aimed) : tip + transform.forward * data.grabRange;
                    flyDirection = (aim - tip).normalized;
                    flown = 0f;
                    ShowBandage(true);
                    grabCooldown = data.grabCooldown;
                    Begin(Phase.BandageFly);
                    break;
                case Phase.BandageFly:
                    FlyBandage(dt);
                    break;
                case Phase.BandagePull:
                    if (grabbed != null) tip = Chest(grabbed.transform);
                    if (aimed != null) FaceTowards(aimed.position, 720f);
                    if (phaseTime < PullSeconds) return;
                    ShowBandage(false);
                    Begin(Phase.ClawWindup);
                    break;
                case Phase.BandageBack:
                    tip = Vector3.Lerp(tip, Hand(), Mathf.Clamp01(phaseTime / BandageBackSeconds));
                    if (phaseTime < BandageBackSeconds) return;
                    ShowBandage(false);
                    End();
                    break;
                case Phase.ClawWindup:
                    if (aimed != null) FaceTowards(aimed.position, 400f);
                    if (phaseTime < ClawWindupSeconds) return;
                    Begin(Phase.ClawStrike);
                    Claw();
                    break;
                case Phase.ClawStrike:
                    if (phaseTime < ClawStrikeSeconds) return;
                    Begin(Phase.ClawRecover);
                    break;
                case Phase.ClawRecover:
                    if (phaseTime < ClawRecoverSeconds) return;
                    cooldown = data.clawCooldown;
                    End();
                    break;
            }
        }

        private void Begin(Phase next)
        {
            phase = next;
            phaseTime = 0f;
            monster.HoldMovement = next != Phase.Free;
        }

        private void End()
        {
            grabbed = null;
            aimed = null;
            Begin(Phase.Free);
        }

        protected override void Interrupt()
        {
            base.Interrupt();
            ShowBandage(false);
            grabbed = null;
            phase = Phase.Free;
        }

        private Vector3 Hand()
        {
            return rig != null && rig.Has("HandR") ? BonePosition("HandR") : transform.position + Vector3.up * 1.2f + transform.forward * 0.6f;
        }

        // 包帯の先をまっすぐ伸ばす。通り道の勇者に当たったら引き寄せる。届かなければ巻き戻す。
        private void FlyBandage(float dt)
        {
            float step = Mathf.Min(data.grabSpeed * dt, data.grabRange + 2f - flown);
            Vector3 from = tip;
            Vector3 to = tip + flyDirection * step;
            flown += step;
            foreach (var col in Physics.OverlapCapsule(from, to, BandageHitRadius, ~0, QueryTriggerInteraction.Collide))
            {
                if (!col.CompareTag("Player")) continue;
                var hp = col.GetComponentInParent<HealthComponent>();
                if (hp == null || hp.IsDead) continue;
                Grab(hp);
                return;
            }
            if (Physics.Linecast(from, to, out var hit, ~0, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<Monster>() == null && !hit.collider.CompareTag("Player"))
            {
                tip = hit.point;
                Begin(Phase.BandageBack);
                return;
            }
            tip = to;
            if (flown >= data.grabRange + 2f) Begin(Phase.BandageBack);
        }

        private void Grab(HealthComponent hp)
        {
            grabbed = hp;
            aimed = hp.transform;
            tip = Chest(hp.transform);
            hp.TakeDamage(Damage(data.grabDamageScale), gameObject, DamageType.Physical);
            if (hp.IsDead)
            {
                Begin(Phase.BandageBack);
                return;
            }
            StatusEffectController.Apply(hp.gameObject, StatusEffectType.Stun, data.grabStunSeconds, 0f, gameObject);
            // 両手が届くところまで引き寄せる。
            Vector3 toMummy = FlatTo(hp.transform.position) * -1f;
            float pull = toMummy.magnitude - Mathf.Max(monster.KeepDistance, data.clawRange * 0.6f);
            if (pull > 0f) SkillKnockback.Play(hp.gameObject, toMummy, pull);
            Begin(Phase.BandagePull);
        }

        // 正面の扇形にいる勇者をまとめてつかむ。
        private void Claw()
        {
            var hit = new HashSet<HealthComponent>();
            foreach (var col in Physics.OverlapSphere(transform.position + Vector3.up, data.clawRange + 0.5f, ~0, QueryTriggerInteraction.Collide))
            {
                if (!col.CompareTag("Player")) continue;
                var hp = col.GetComponentInParent<HealthComponent>();
                if (hp == null || hp.IsDead || !hit.Add(hp)) continue;
                Vector3 to = FlatTo(hp.transform.position);
                if (to.magnitude > data.clawRange + 0.6f || Vector3.Angle(transform.forward, to) > ClawArcDegrees * 0.5f) continue;
                hp.TakeDamage(Damage(data.clawDamageScale), gameObject, DamageType.Physical);
                SkillKnockback.Play(hp.gameObject, to, 1.2f);
            }
        }

        private void ShowBandage(bool show)
        {
            if (show && bandage == null)
            {
                var go = new GameObject("Bandage");
                go.transform.SetParent(transform, false);
                bandage = go.AddComponent<LineRenderer>();
                if (lineMaterial == null) lineMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "MummyBandage" };
                bandage.sharedMaterial = lineMaterial;
                bandage.positionCount = 8;
                bandage.widthMultiplier = 0.2f;
                bandage.startColor = bandage.endColor = Cloth;
                bandage.numCapVertices = 2;
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", Cloth);
                bandage.SetPropertyBlock(block);
            }
            if (bandage != null) bandage.enabled = show;
        }

        private void OnDestroy()
        {
            if (grabbed != null) grabbed = null;
        }

        protected override void Pose(float dt)
        {
            if (bandage != null && bandage.enabled)
            {
                // 手から先まで、少し波打つ包帯。
                Vector3 a = Hand();
                for (int i = 0; i < bandage.positionCount; i++)
                {
                    float t = i / (bandage.positionCount - 1f);
                    Vector3 p = Vector3.Lerp(a, tip, t);
                    p += Vector3.up * Mathf.Sin(t * Mathf.PI * 3f + time * 18f) * 0.12f * (1f - t) * t * 4f;
                    bandage.SetPosition(i, p);
                }
            }

            float p01 = Pace;
            float windupBandage = phase == Phase.BandageWindup ? Mathf.Clamp01(phaseTime / BandageWindupSeconds) : 0f;
            float throwOut = phase == Phase.BandageFly || phase == Phase.BandagePull ? 1f : 0f;
            float clawRaise = phase == Phase.ClawWindup ? Mathf.Clamp01(phaseTime / ClawWindupSeconds) : 0f;
            float clawDown = phase == Phase.ClawStrike ? 1f : (phase == Phase.ClawRecover ? 1f - Mathf.Clamp01(phaseTime / ClawRecoverSeconds) : 0f);
            float claw = Mathf.Max(clawRaise, 0f);

            // 体: 左右に大きく揺れ、頭がぐらぐらする。
            Rotate("Body", Vector3.forward, 9f * sway * (0.4f + p01));
            Rotate("Body", Vector3.up, 6f * sway);
            Rotate("Chest", Vector3.right, 6f * Wave(1.1f) - 12f * claw + 18f * clawDown);
            Rotate("Head", Vector3.forward, 14f * Wave(0.8f, 1f));
            Rotate("Head", Vector3.right, 8f * Wave(1.3f, 2f) - 6f * claw);

            // 脚: 片足ずつ引きずって出す。
            for (int i = 0; i < 2; i++)
            {
                string s = i == 0 ? "L" : "R";
                float step = Mathf.Sin(stride + i * Mathf.PI);
                Rotate("Thigh" + s, Vector3.right, -18f * p01 * step);
                Rotate("Shin" + s, Vector3.right, 16f * p01 * Mathf.Max(0f, step));
                Rotate("Foot" + s, Vector3.right, -10f * p01 * Mathf.Max(0f, -step));
            }

            // 腕: だらりと前へ揺らす。包帯を投げるときは右腕を振りかぶって突き出し、つかむときは両腕を振り上げて下ろす。
            Rotate("ArmL", Vector3.right, 8f * Wave(1.2f, 3f) - 60f * claw + 70f * clawDown);
            Rotate("ArmL", Vector3.forward, 6f * Wave(1.0f, 4f));
            Rotate("ArmR", Vector3.right, 10f * Wave(1.2f, 5f) - 110f * claw + 90f * clawDown + 50f * windupBandage - 70f * throwOut);
            Rotate("ArmR", Vector3.forward, -6f * Wave(1.0f, 6f));
            Rotate("ForeR", Vector3.right, -25f * throwOut);
            Rotate("HandL", Vector3.right, 10f * Wave(2.2f, 7f));
            Rotate("HandR", Vector3.right, 10f * Wave(2.2f, 8f));
        }
    }
}
