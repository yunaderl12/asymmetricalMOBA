using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Skill;

namespace Game.Monster
{
    /// 山のコカトリス。ふだんは翼をたたみ、首を前後に振りながら鶏のように歩く。
    /// 勇者を見つけると勇者の半分くらいの高さへ飛び上がり、羽ばたきながら間合いを保つ。
    /// 技: 高く舞い上がってから、勇者めがけて斜めに滑空してダイブする。通り道の勇者に当たる。
    public class CockatriceBehaviour : MonsterBehaviour
    {
        private enum Phase { Free, Climb, Dive, Recover }

        private const float AimDegrees = 30f;
        private const float DiveBottom = 0.35f;

        private Phase phase = Phase.Free;
        private float phaseTime;
        private float fly;
        private float stride;
        private Vector3 diveDirection;
        private float diveLength;
        private float dived;
        private float diveStartLift;
        private readonly HashSet<HealthComponent> diveHits = new HashSet<HealthComponent>();

        protected override void Tick(float dt)
        {
            phaseTime += dt;
            stride += dt * (2.5f + 9f * Pace);
            var target = Target;
            float goal = target != null || phase != Phase.Free ? 1f : 0f;
            fly = Mathf.MoveTowards(fly, goal, dt / (goal > 0f ? 0.6f : 1.2f));
            float hover = data.hoverHeight * fly * fly * (3f - 2f * fly);

            switch (phase)
            {
                case Phase.Free:
                    Lift = hover + 0.12f * fly * Wave(8f);
                    if (target == null || cooldown > 0f || fly < 0.9f) return;
                    Vector3 to = FlatTo(target.position);
                    if (to.magnitude > data.diveRange) return;
                    FaceTowards(target.position);
                    if (Vector3.Angle(transform.forward, to) > AimDegrees) return;
                    monster.HoldMovement = true;
                    phase = Phase.Climb;
                    phaseTime = 0f;
                    break;
                case Phase.Climb:
                    // 舞い上がりながら、狙う勇者の方へ向き直る。
                    if (target != null) FaceTowards(target.position, 360f);
                    float k = Mathf.Clamp01(phaseTime / Mathf.Max(0.05f, data.diveWindup));
                    Lift = Mathf.Lerp(hover, data.diveClimbHeight, 1f - (1f - k) * (1f - k));
                    if (k < 1f) return;
                    Vector3 aim = target != null ? FlatTo(target.position) : transform.forward * data.diveRange;
                    diveDirection = aim.sqrMagnitude > 0.01f ? aim.normalized : transform.forward;
                    // 勇者を少し越えたところまで滑る。
                    diveLength = aim.magnitude + 2.5f;
                    dived = 0f;
                    diveStartLift = Lift;
                    diveHits.Clear();
                    phase = Phase.Dive;
                    phaseTime = 0f;
                    break;
                case Phase.Dive:
                    float step = data.diveSpeed * dt;
                    if (agent != null && agent.isOnNavMesh) agent.Move(diveDirection * step);
                    dived += step;
                    float t = Mathf.Clamp01(dived / Mathf.Max(0.1f, diveLength));
                    Lift = Mathf.Lerp(diveStartLift, DiveBottom, Mathf.Sqrt(t));
                    HitDuringDive();
                    if (t < 1f) return;
                    phase = Phase.Recover;
                    phaseTime = 0f;
                    break;
                case Phase.Recover:
                    Lift = Mathf.Lerp(DiveBottom, hover, Mathf.Clamp01(phaseTime / Mathf.Max(0.05f, data.diveRecover)));
                    if (phaseTime < data.diveRecover) return;
                    cooldown = data.diveCooldown;
                    monster.HoldMovement = false;
                    phase = Phase.Free;
                    break;
            }
        }

        private void HitDuringDive()
        {
            Vector3 body = transform.position + Vector3.up * (Lift + 0.6f * Size * 0.4f);
            foreach (var col in Physics.OverlapSphere(body, data.diveHitRadius, ~0, QueryTriggerInteraction.Collide))
            {
                if (!col.CompareTag("Player")) continue;
                var hp = col.GetComponentInParent<HealthComponent>();
                if (hp == null || hp.IsDead || !diveHits.Add(hp)) continue;
                hp.TakeDamage(Damage(data.diveDamageScale), gameObject, DamageType.Physical);
                SkillKnockback.Play(hp.gameObject, diveDirection, data.diveKnockback);
            }
        }

        protected override void Interrupt()
        {
            base.Interrupt();
            if (phase != Phase.Free) cooldown = Mathf.Max(cooldown, data.diveCooldown * 0.5f);
            phase = Phase.Free;
        }

        protected override void Pose(float dt)
        {
            float f = fly * fly * (3f - 2f * fly);
            float walk = (1f - f) * Pace;
            bool diving = phase == Phase.Dive;
            float climb = phase == Phase.Climb ? 1f : 0f;
            float flap = Wave(phase == Phase.Climb ? 13f : 8f);

            // 体: 歩くときは首を前後に振り、飛ぶときは前へ傾く。ダイブでは頭を下げて突っ込む。
            Rotate("Body", Vector3.right, 6f * f + (diving ? 22f : 0f) - 10f * climb);
            Rotate("Neck", Vector3.right, 14f * walk * Mathf.Sin(stride * 2f) + (diving ? 10f : 0f) + 3f * Wave(1.3f));
            Rotate("Head", Vector3.right, -8f * walk * Mathf.Sin(stride * 2f + 0.8f) + (diving ? 12f : 0f));
            Rotate("Head", Vector3.up, (1f - f) * 20f * Wave(0.7f, 1f));
            Rotate("Tail", Vector3.right, -6f * Wave(1.1f, 2f) - 10f * f);

            for (int i = 0; i < 2; i++)
            {
                string s = i == 0 ? "L" : "R";
                float side = i == 0 ? -1f : 1f;
                // 翼: たたむ（下ろして後ろへ）⇔ 広げて羽ばたく。ダイブでは後ろへ流して止める。
                float folded = 1f - f;
                float down = folded * 55f + f * (diving ? -5f : 5f + 35f * flap);
                Rotate("Wing" + s, Vector3.forward, -side * down);
                Rotate("Wing" + s, Vector3.up, side * (folded * 50f + (diving ? 45f : 0f)));
                Rotate("WingTip" + s, Vector3.forward, -side * (folded * 30f + f * 15f * Wave(8f, 0.5f)));
                // 脚: 歩くと交互に振る。飛ぶとたたむ。
                float swing = Mathf.Sin(stride + i * Mathf.PI);
                Rotate("Thigh" + s, Vector3.right, -30f * walk * swing + 45f * f);
                Rotate("Shin" + s, Vector3.right, 25f * walk * Mathf.Max(0f, -swing) + 30f * f);
            }
        }
    }
}
