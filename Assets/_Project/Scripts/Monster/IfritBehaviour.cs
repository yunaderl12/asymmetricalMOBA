using UnityEngine;
using Game.Skill;

namespace Game.Monster
{
    /// 砂漠のイフリート。いつも地面からほんの少し浮いて漂い、炎の尾と組んだ腕がゆらゆら動く。
    /// 技: 両手を胸の前で合わせて火球を作り、大きく育ててから前へ放つ（魔法使いの火球と同じ見た目）。
    public class IfritBehaviour : RangedMonsterBehaviour
    {
        private const float StartRadius = 0.06f;
        private const float FullRadius = 0.5f;

        private float drift;
        private FireballVisual orb;

        /// 合わせた両手の間（モデルの中の点）。
        private Vector3 HandsPoint => ModelPoint(new Vector3(0f, 0.47f, 0.24f));

        protected override Vector3 Muzzle => orb != null ? orb.transform.position : HandsPoint;

        protected override void OnWindupStart()
        {
            orb = FireballVisual.Create(HandsPoint);
            orb.SetShape(StartRadius, 1f, 1f, 0f);
            orb.SetEnergy(0.3f);
        }

        protected override void OnWindup(float charge)
        {
            if (orb == null) return;
            orb.transform.position = HandsPoint;
            orb.SetShape(Mathf.Lerp(StartRadius, FullRadius, charge), 1f, 1f, 0f);
            orb.SetEnergy(Mathf.Lerp(0.3f, 1f, charge));
        }

        protected override void OnWindupCancelled()
        {
            if (orb != null) Destroy(orb.gameObject);
            orb = null;
        }

        protected override void Fire(Vector3 from, Vector3 direction)
        {
            if (orb == null) OnWindupStart();
            MonsterBolt.Spawn(MonsterBolt.Kind.Fire, gameObject, from, direction, data.projectileSpeed, data.attackRange + 4f,
                Mathf.Max(data.projectileHitRadius, FullRadius), Damage(data.attackDamageScale), orb);
            orb = null;
        }

        private void OnDestroy()
        {
            if (orb != null) Destroy(orb.gameObject);
        }

        protected override void Tick(float dt)
        {
            base.Tick(dt);
            drift = Mathf.MoveTowards(drift, Pace, dt * 2f);
            Lift = data.floatHeight + 0.12f * Wave(1.3f);
        }

        protected override void Pose(float dt)
        {
            float d = drift;
            float gather = phase == Phase.Windup ? Charge : 0f;
            float thrust = phase == Phase.Recover ? Charge : 0f;
            // 炎の尾は大きく、体は小さく揺れる。進むときは前へ傾き、尾が後ろへ流れる。
            Rotate("Tail", Vector3.right, 12f * d + 6f * Wave(2.1f));
            Rotate("Tail", Vector3.forward, 8f * Wave(1.7f, 1f));
            Rotate("Body", Vector3.right, -6f * d + 4f * Wave(1.5f, 2f) - 8f * gather);
            Rotate("Body", Vector3.forward, 4f * Wave(1.2f, 3f));
            Rotate("Head", Vector3.right, 5f * Wave(1.1f, 4f) + 10f * gather);
            Rotate("Head", Vector3.up, 10f * Wave(0.6f, 5f));

            // 腕: 組んだまま息をするように揺れる。溜めでは両手を前へ出して合わせ、放つと前へ突き出す。
            for (int i = 0; i < 2; i++)
            {
                string s = i == 0 ? "L" : "R";
                float side = i == 0 ? -1f : 1f;
                float seed = i * 1.9f;
                Rotate("Arm" + s, Vector3.forward, -side * (5f * Wave(1.4f, seed) + 18f * gather));
                Rotate("Arm" + s, Vector3.right, -35f * gather - 25f * thrust + 4f * Wave(1.4f, seed + 1f));
                Rotate("Fore" + s, Vector3.right, -20f * gather - 30f * thrust);
                Rotate("Fore" + s, Vector3.up, side * (6f * Wave(1.6f, seed + 2f) - 20f * thrust));
            }
        }
    }
}
