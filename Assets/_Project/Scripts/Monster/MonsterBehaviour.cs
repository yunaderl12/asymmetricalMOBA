using UnityEngine;
using UnityEngine.AI;
using Game.Combat;
using Game.Hero;

namespace Game.Monster
{
    /// 自分の技で戦う雑魚魔物（ドライアド・ミイラ・イフリート・ハーピー・コカトリス）の共通部分。
    /// 移動と間合い（勇者に重ならない距離で止まる・近すぎたら下がる）は Monster が受け持ち、ここは技とモデルの姿勢を受け持つ。
    ///  - Tick: 毎フレームの判断（技の開始・進行）。倒れている・湧いている途中・スタン・のけぞり中は呼ばない。
    ///  - Pose: 骨の姿勢。BossRig で毎フレーム元の姿勢に戻してから重ねる。Lift でモデルを地面から浮かせる。
    public abstract class MonsterBehaviour : MonoBehaviour
    {
        protected Monster monster;
        protected MonsterData data;
        protected HealthComponent health;
        protected NavMeshAgent agent;
        protected BossRig rig;
        protected Transform model;
        protected float cooldown;
        /// モデルを地面から持ち上げる高さ（m）。
        protected float Lift;
        protected float time;

        private Vector3 modelBase;
        private bool stalled;
        private Game.UI.HealthBarUI[] bars;
        private float[] barBase;

        public static void Attach(Monster m)
        {
            if (m.Data == null || m.GetComponent<MonsterBehaviour>() != null) return;
            switch (m.Data.kind)
            {
                case MonsterKind.Dryad: m.gameObject.AddComponent<DryadBehaviour>(); break;
                case MonsterKind.Mummy: m.gameObject.AddComponent<MummyBehaviour>(); break;
                case MonsterKind.Ifrit: m.gameObject.AddComponent<IfritBehaviour>(); break;
                case MonsterKind.Harpy: m.gameObject.AddComponent<HarpyBehaviour>(); break;
                case MonsterKind.Cockatrice: m.gameObject.AddComponent<CockatriceBehaviour>(); break;
            }
        }

        protected virtual void Awake()
        {
            monster = GetComponent<Monster>();
            data = monster.Data;
            health = GetComponent<HealthComponent>();
            agent = GetComponent<NavMeshAgent>();
            time = Random.value * 10f;
            cooldown = Random.Range(0.5f, 1.5f);
        }

        /// 追っている勇者。気づいていなければ null。
        protected Transform Target => monster.ChaseTarget;

        protected bool Dead => health != null && health.IsDead;

        /// 足の速さ（0 = 止まっている、1 = MonsterData.spd）。
        protected float Pace => agent != null && data.spd > 0f ? Mathf.Clamp01(agent.velocity.magnitude / data.spd) : 0f;

        protected int Damage(float scale) => Mathf.Max(1, Mathf.RoundToInt(monster.CurrentAtk * scale));

        /// モデルの大きさ（MonsterData.modelScale）。骨の位置などはこれを掛けた m。
        protected float Size => data.modelScale;

        protected Vector3 FlatTo(Vector3 point)
        {
            Vector3 to = point - transform.position;
            to.y = 0f;
            return to;
        }

        protected void FaceTowards(Vector3 point, float degreesPerSecond = 540f)
        {
            Vector3 to = FlatTo(point);
            if (to.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), degreesPerSecond * Time.deltaTime);
        }

        /// 勇者の胸の高さ。
        protected static Vector3 Chest(Transform hero) => hero.position + Vector3.up * 1.1f;

        private void Update()
        {
            if (Dead || monster.IsEmerging) return;
            float dt = Time.deltaTime;
            time += dt;
            bool blocked = StatusEffectController.IsStunnedObj(gameObject) || monster.IsFlinching;
            if (blocked)
            {
                if (!stalled) Interrupt();
                stalled = true;
                return;
            }
            stalled = false;
            if (cooldown > 0f) cooldown -= dt;
            Tick(dt);
        }

        private void LateUpdate()
        {
            if (Dead || monster.IsEmerging) return;
            if (rig == null)
            {
                var anim = GetComponent<CharacterModelAnimator>();
                if (anim == null || anim.ModelRoot == null) return;
                model = anim.ModelRoot;
                modelBase = model.localPosition;
                rig = new BossRig(model);
                bars = GetComponentsInChildren<Game.UI.HealthBarUI>(true);
                barBase = new float[bars.Length];
                for (int i = 0; i < bars.Length; i++) barBase[i] = bars[i].transform.localPosition.y;
                OnRigReady();
            }
            rig.Begin();
            model.localPosition = modelBase + Vector3.up * Lift;
            // HP バーも体といっしょに浮かせる。
            for (int i = 0; i < bars.Length; i++)
                if (bars[i] != null) bars[i].SetHeight(barBase[i] + Lift);
            Pose(Time.deltaTime);
        }

        protected virtual void OnRigReady() { }

        /// スタン・のけぞりで技を中断する。
        protected virtual void Interrupt()
        {
            monster.HoldMovement = false;
        }

        protected abstract void Tick(float dt);
        protected abstract void Pose(float dt);

        /// 骨の付け根の位置（ワールド）。骨がなければモデルの根元。
        protected Vector3 BonePosition(string bone)
        {
            var t = rig != null ? rig.Get(bone) : null;
            return t != null ? t.position : (model != null ? model.position : transform.position);
        }

        /// モデルの中の点（モデルの向きと大きさで、原点は足元）。
        protected Vector3 ModelPoint(Vector3 local)
        {
            return model != null ? model.TransformPoint(local) : transform.TransformPoint(local * Size);
        }

        protected void Rotate(string bone, Vector3 axis, float degrees)
        {
            rig.Rotate(bone, axis, degrees);
        }

        /// ゆっくりした揺れ（-1〜1）。seed で骨ごとにずらす。
        protected float Wave(float speed, float seed = 0f) => Mathf.Sin(time * speed + seed);

        private void OnDisable()
        {
            if (monster != null) monster.HoldMovement = false;
        }
    }
}
