using UnityEngine;
using UnityEngine.AI;
using Game.Hero;
using Game.Skill;

namespace Game.Monster
{
    /// 番人の重い足どり。足が地面に着くたびに体がズシンと沈み、足元から土煙が上がり、近くの勇者の画面が揺れる。
    /// 足の骨の高さを見て、上がった足が下りてきた瞬間を一歩とする（歩きのアニメーションの速さが変わってもずれない）。
    public class GolemFootsteps : MonoBehaviour
    {
        private const float LiftShare = 0.05f;      // 体の高さに対して、この高さまで上がったら「足を上げた」
        private const float LandShare = 0.02f;      // この高さまで下りたら「着いた」
        private const float MinMoveSpeed = 0.6f;    // これより遅いときは足音を出さない (m/s)
        private const float DipSeconds = 0.32f;     // 沈んで戻るまで
        private const float StepShakeSeconds = 0.28f;
        private const int DustCount = 18;
        private const float DustRadiusShare = 0.08f;
        private const float DustSpeed = 3f;
        private const float GroundDrift = 0.25f;    // 足の一番低い高さを少しずつ上げて、坂でも測り直す (m/s)

        private MonsterData data;
        private CharacterModelAnimator model;
        private NavMeshAgent agent;
        private Transform root;
        private Vector3 rootBase;
        private Combat.HealthComponent health;
        private readonly Transform[] feet = new Transform[2];
        private readonly float[] lowest = { float.PositiveInfinity, float.PositiveInfinity };
        private readonly bool[] lifted = new bool[2];
        private float dipTimer = float.PositiveInfinity;
        private bool bonesCached;
        private bool sunk;

        public void Init(MonsterData monsterData)
        {
            data = monsterData;
            model = GetComponent<CharacterModelAnimator>();
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Combat.HealthComponent>();
            if (model != null) model.RunAnimSpeedScale = data.guardWalkAnimScale;
        }

        private void LateUpdate()
        {
            if (data == null || !CacheBones()) return;
            if (health != null && health.IsDead)
            {
                dipTimer = float.PositiveInfinity;
                if (sunk) { root.localPosition = rootBase; sunk = false; }
                return;
            }
            float speed = agent != null && agent.enabled ? agent.velocity.magnitude : 0f;
            float body = data.guardBodyHeight;
            float lowestFoot = float.PositiveInfinity;
            for (int i = 0; i < 2; i++)
            {
                // 体ごと沈めた分に左右されないよう、モデルの根元から測る。
                float height = feet[i].position.y - root.position.y;
                lowestFoot = Mathf.Min(lowestFoot, height);
                lowest[i] = Mathf.Min(lowest[i] + GroundDrift * Time.deltaTime, height);
                if (height > lowest[i] + body * LiftShare) lifted[i] = true;
                else if (lifted[i] && height < lowest[i] + body * LandShare)
                {
                    lifted[i] = false;
                    if (speed >= MinMoveSpeed) Step(feet[i].position);
                }
            }
            // 走りのアニメーションは両足が浮く瞬間があるので、浮いた分だけ体を下げて、いつもどちらかの足を地面に着ける。
            float airborne = speed >= MinMoveSpeed ? Mathf.Max(0f, lowestFoot - Mathf.Min(lowest[0], lowest[1])) : 0f;
            ApplySink(airborne);
        }

        private void Step(Vector3 foot)
        {
            dipTimer = 0f;
            Vector3 ground = new Vector3(foot.x, transform.position.y, foot.z);
            MeleeFx.DustBurst(ground, data.guardBodyHeight * DustRadiusShare, DustCount, DustSpeed);
            HeroFPSController.ShakeNear(ground, data.guardStepShake, data.guardStepShakeRadius, StepShakeSeconds);
        }

        // 着地の直後にすっと沈み、ゆっくり戻る。モデル全体を下げる（止まれば元の位置にぴったり戻す）。
        private void ApplySink(float airborne)
        {
            float sink = 0f;
            if (dipTimer < DipSeconds)
            {
                dipTimer += Time.deltaTime;
                float t = Mathf.Clamp01(dipTimer / DipSeconds);
                sink = t >= 1f ? 0f : t < 0.25f ? t / 0.25f : 1f - Mathf.SmoothStep(0f, 1f, (t - 0.25f) / 0.75f);
            }
            float down = data.guardStepDip * sink + airborne;
            if (down <= 0f && !sunk) return;
            sunk = down > 0f;
            Vector3 axis = root.parent != null ? root.parent.InverseTransformVector(Vector3.down) : Vector3.down;
            root.localPosition = rootBase + axis * down;
        }

        private bool CacheBones()
        {
            if (bonesCached) return root != null && feet[0] != null && feet[1] != null;
            if (model == null || model.ModelRoot == null) return false;
            bonesCached = true;
            root = model.ModelRoot;
            rootBase = root.localPosition;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "LeftFoot") feet[0] = t;
                else if (t.name == "RightFoot") feet[1] = t;
            }
            return feet[0] != null && feet[1] != null;
        }
    }
}
