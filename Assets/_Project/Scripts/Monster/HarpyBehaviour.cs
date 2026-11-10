using UnityEngine;

namespace Game.Monster
{
    /// 山のハーピー。ふだんは翼をたたんで鳥の足でとことこ歩く。
    /// 勇者を見つけると勇者の半分くらいの高さへ飛び上がり、羽ばたきながら間合いを保つ。
    /// 技: 翼を後ろへ引いて、羽根を一枚まっすぐ飛ばす（射程は魔法使いの通常攻撃より少し短い）。
    public class HarpyBehaviour : RangedMonsterBehaviour
    {
        private float fly;
        private float stride;

        protected override bool ReadyToShoot => fly > 0.8f;

        protected override Vector3 Muzzle => rig != null && rig.Has("ForeR") ? BonePosition("ForeR") : transform.position + Vector3.up * 1.6f;

        protected override void Fire(Vector3 from, Vector3 direction)
        {
            MonsterBolt.Spawn(MonsterBolt.Kind.Feather, gameObject, from, direction, data.projectileSpeed, data.attackRange + 4f,
                data.projectileHitRadius, Damage(data.attackDamageScale));
        }

        /// 羽ばたきの速さ（rad/s）。
        private const float FlapSpeed = 11f;

        protected override void Tick(float dt)
        {
            base.Tick(dt);
            fly = Mathf.MoveTowards(fly, Target != null ? 1f : 0f, dt / (Target != null ? 0.6f : 1.2f));
            float smooth = fly * fly * (3f - 2f * fly);
            // 翼を打ち下ろした少しあとに体が浮く。
            Lift = data.hoverHeight * smooth + 0.15f * smooth * Wave(FlapSpeed, -1.2f);
            stride += dt * (2f + 9f * Pace);
        }

        protected override void Pose(float dt)
        {
            float f = fly * fly * (3f - 2f * fly);
            float walk = (1f - f) * Pace;
            float pull = phase == Phase.Windup ? Charge : 0f;
            float snap = phase == Phase.Recover ? Charge : 0f;

            // 体: 歩くときは小さく上下に揺れる。飛ぶときは鳥のように前へ傾け（進むほど深く）、頭は前を向いたまま。
            float lean = f * (22f + 12f * Pace);
            Rotate("Body", Vector3.right, lean + 4f * walk * Mathf.Sin(stride * 2f) + 3f * f * Wave(FlapSpeed, -1.2f));
            Rotate("Head", Vector3.right, -lean * 0.7f + 3f * Wave(1.2f, 1f));
            Rotate("Head", Vector3.up, (1f - f) * 18f * Wave(0.5f, 2f));

            for (int i = 0; i < 2; i++)
            {
                string s = i == 0 ? "L" : "R";
                float side = i == 0 ? -1f : 1f;
                float folded = 1f - f;
                // 翼: たたむ（下ろして後ろへ）⇔ 大きく羽ばたく。打ち下ろしは前へ、打ち上げは後ろへ少し流れ、
                // 先（前腕・手）は遅れてしなる。
                float flap = Wave(FlapSpeed);
                float down = folded * 70f + f * (10f + 36f * flap);
                Rotate("Arm" + s, Vector3.forward, -side * down);
                Rotate("Arm" + s, Vector3.up, side * (folded * 45f + f * 12f * Wave(FlapSpeed, Mathf.PI * 0.5f)));
                Rotate("Fore" + s, Vector3.forward, -side * (folded * 35f + f * 22f * Wave(FlapSpeed, -0.7f)));
                Rotate("Hand" + s, Vector3.forward, -side * (folded * 25f + f * 26f * Wave(FlapSpeed, -1.4f)));
                if (i == 1)
                {
                    // 右の翼を後ろへ引き、振り抜いて羽根を飛ばす。
                    Rotate("ArmR", Vector3.up, 55f * pull - 40f * snap);
                }
                // 脚: 歩くと交互に振る。飛ぶと左右を揃えて閉じ、後ろへ流してつま先を伸ばす。
                float swing = Mathf.Sin(stride + i * Mathf.PI);
                Rotate("Thigh" + s, Vector3.right, -28f * walk * swing + 28f * f + 3f * f * Wave(FlapSpeed, -2f));
                Rotate("Thigh" + s, Vector3.forward, -side * 20f * f);
                Rotate("Shin" + s, Vector3.right, 22f * walk * Mathf.Max(0f, -swing) + 18f * f);
                Rotate("Foot" + s, Vector3.right, 50f * f);
            }
        }
    }
}
