using UnityEngine;

namespace Game.Monster
{
    /// 森のドライアド。いつも地面からほんの少し浮いてふわふわ漂い、止まっていても手足がゆらゆら動く。
    /// 勇者へ向かうときは前かがみになって脚と腕を後ろへ流し、すいーっと滑るように進む。
    /// 技: 右手を振って木の葉の刃を扇形に飛ばす（射程は魔法使いの通常攻撃より少し短い）。
    public class DryadBehaviour : RangedMonsterBehaviour
    {
        private float glide;

        protected override Vector3 Muzzle => rig != null && rig.Has("HandR") ? BonePosition("HandR") : transform.position + Vector3.up * 1.3f;

        protected override void Fire(Vector3 from, Vector3 direction)
        {
            FireFan(MonsterBolt.Kind.Leaf, from, direction);
        }

        protected override void Tick(float dt)
        {
            base.Tick(dt);
            glide = Mathf.MoveTowards(glide, Pace, dt * 2.5f);
            Lift = data.floatHeight + 0.09f * Wave(1.7f) + 0.12f * glide;
        }

        protected override void Pose(float dt)
        {
            float g = glide;
            // 体: 漂いながら少し揺れ、進むときは前へ傾く。
            Rotate("Body", Vector3.right, 16f * g + 3f * Wave(1.1f, 1f));
            Rotate("Body", Vector3.forward, 4f * Wave(0.9f, 2f));
            Rotate("Chest", Vector3.right, -4f * g + 2f * Wave(1.3f, 3f));
            Rotate("Head", Vector3.forward, 7f * Wave(0.7f, 4f));
            Rotate("Head", Vector3.right, -10f * g + 4f * Wave(0.9f, 5f));

            // 腕: T の字から下ろして、ふわふわ揺らす。進むときは後ろへ流す。
            float throwBack = phase == Phase.Windup ? Charge : 0f;
            float release = phase == Phase.Recover ? Charge : 0f;
            ArmPose("L", -1f, g, 0f, 0f);
            ArmPose("R", 1f, g, throwBack, release);

            // 脚: だらんと垂らして、ゆっくり交互に揺らす。進むときは後ろへ流す。
            for (int i = 0; i < 2; i++)
            {
                string s = i == 0 ? "L" : "R";
                float seed = i * 2.3f;
                Rotate("Thigh" + s, Vector3.right, 10f + 28f * g + 9f * Wave(1.4f, seed));
                Rotate("Thigh" + s, Vector3.forward, (i == 0 ? -1f : 1f) * 3f * Wave(1.1f, seed + 1f));
                Rotate("Shin" + s, Vector3.right, 18f + 22f * g + 10f * Wave(1.4f, seed + 0.8f));
                Rotate("Foot" + s, Vector3.right, 30f + 10f * g);
            }
        }

        /// side: 左 -1 / 右 +1。throwBack: 右手の振りかぶり（0〜1）、release: 振り抜いた後の残り（1→0）。
        private void ArmPose(string s, float side, float g, float throwBack, float release)
        {
            float seed = side > 0f ? 0f : 1.7f;
            // 下ろす（左腕は +Z まわりで下へ、右腕は -Z まわりで下へ）。
            float lower = 62f - 14f * g + 7f * Wave(1.3f, seed);
            Rotate("Arm" + s, Vector3.forward, -side * lower);
            // 後ろへ流す（左は -Y まわり、右は +Y まわりが後ろ）。
            float back = 30f * g + 6f * Wave(0.9f, seed + 1f);
            back += 55f * throwBack - 70f * release;
            Rotate("Arm" + s, Vector3.up, side * back);
            Rotate("Arm" + s, Vector3.forward, side * 40f * throwBack);
            Rotate("Fore" + s, Vector3.up, side * (15f + 8f * Wave(1.6f, seed + 2f)));
            Rotate("Hand" + s, Vector3.forward, -side * 10f * Wave(1.9f, seed + 3f));
        }
    }
}
