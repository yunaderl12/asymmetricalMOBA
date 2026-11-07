using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Core;

namespace Game.Monster
{
    /// 広場の中立ボスの共通部分。HP・経験値・防御、広場にいる勇者の選び方、倒されたときの演出、HPバーへの登録を受け持つ。
    /// 技と動きは派生クラス（DragonBoss / ScorpionBoss）が書く。勇者だけを狙い、魔王や魔物とは戦わない。
    [RequireComponent(typeof(HealthComponent))]
    public abstract class BossBase : MonoBehaviour, IDefenseSource
    {
        private const float DeathSeconds = 2.4f;
        private const int DeathParticles = 160;

        public static readonly List<BossBase> All = new List<BossBase>();

        protected BossData baseData;
        protected HealthComponent health;
        protected Vector3 arenaCenter;   // 広場の床の中心
        protected float arenaRadius;
        protected Transform model;       // 向きの基準。+Z が前
        protected BossRig rig;
        protected Transform target;
        protected float levelMultiplier = 1f;
        private BossSpawner spawner;
        private BoxCollider body;
        private float deathTimer;
        private bool dead;

        public string DisplayName => baseData != null ? baseData.bossName : name;
        public HealthComponent Health => health;
        public Vector3 ArenaCenter => arenaCenter;
        public bool IsDead => dead;
        public float AggroRadius => baseData != null ? baseData.aggroRadius : 0f;
        public int Df => baseData != null ? baseData.df : 0;
        public int MDf => baseData != null ? baseData.mDf : 0;
        /// いま勇者と戦っているか（HPバーを出すかどうか）。
        public bool Engaged => !dead && target != null;
        public int CurrentAtk => baseData != null ? Mathf.RoundToInt(baseData.atk * levelMultiplier) : 0;

        public void Init(BossData data, Vector3 center, float radius, BossSpawner owner)
        {
            baseData = data;
            arenaCenter = center;
            arenaRadius = radius;
            spawner = owner;
            gameObject.tag = "Monster";
            health = GetComponent<HealthComponent>();
            levelMultiplier = DemonKingLevel.MonsterStatMultiplier(DemonKingLevel.Current);
            health.SetMaxHP(data.ScaledHp(levelMultiplier));
            var reward = GetComponent<ExpReward>();
            if (reward == null) reward = gameObject.AddComponent<ExpReward>();
            reward.Exp = data.exp;

            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            body = gameObject.AddComponent<BoxCollider>();
            body.size = data.bodySize;
            body.center = Vector3.up * data.bodyCenterHeight;

            BuildModel();
            health.OnDeath += OnDied;
            Game.UI.BossHealthBarUI.EnsureExists();
            OnInit();
        }

        protected abstract void OnInit();
        protected abstract void Tick();
        /// 骨の姿勢。毎フレーム rig.Begin() のあとに呼ばれる。
        protected abstract void Pose();
        protected virtual void OnDeathStart() { }
        /// 倒れている途中（t は 0→1）。
        protected virtual void DeathTick(float t) { }

        private void BuildModel()
        {
            var container = new GameObject("Model").transform;
            container.SetParent(transform, false);
            model = container;
            var prefab = string.IsNullOrEmpty(baseData.modelPath) ? null : Resources.Load<GameObject>(baseData.modelPath);
            if (prefab == null)
            {
                Debug.LogWarning("[Boss] model not found: " + baseData.modelPath);
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(box.GetComponent<Collider>());
                box.transform.SetParent(container, false);
                box.transform.localScale = baseData.bodySize;
                box.transform.localPosition = Vector3.up * baseData.bodyCenterHeight;
                return;
            }
            var inst = Instantiate(prefab, container);
            inst.name = prefab.name;
            inst.transform.localRotation = Quaternion.Euler(0f, baseData.modelYaw, 0f) * inst.transform.localRotation;
            inst.transform.localScale = inst.transform.localScale * baseData.modelScale;
            var material = string.IsNullOrEmpty(baseData.modelMaterialPath) ? null : Resources.Load<Material>(baseData.modelMaterialPath);
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                if (material != null) r.sharedMaterial = material;
                if (r is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true; // 骨で大きく動くので、元の範囲で消えないように
            }
            rig = new BossRig(container);
        }

        protected virtual void Update()
        {
            if (dead)
            {
                deathTimer += Time.deltaTime;
                DeathTick(Mathf.Clamp01(deathTimer / DeathSeconds));
                if (deathTimer >= DeathSeconds) Vanish();
                return;
            }
            if (MatchManager.Instance != null && MatchManager.Instance.CurrentState != MatchManager.MatchState.InMatch) return;
            target = FindHero();
            if (target == null && health.CurrentHP < health.MaxHP)
                health.Heal(Mathf.CeilToInt(health.MaxHP * baseData.idleRegenShare * Time.deltaTime));
            Tick();
        }

        private void LateUpdate()
        {
            if (rig == null) return;
            rig.Begin();
            Pose();
        }

        protected bool Stunned => StatusEffectController.IsStunnedObj(gameObject);

        protected float SpeedFactor
        {
            get
            {
                var mods = GetComponent<StatModifierContainer>();
                return mods != null ? mods.TotalSpdMultiplier : 1f;
            }
        }

        /// 広場の中にいる、生きている勇者のうちいちばん近い者。
        protected Transform FindHero()
        {
            Transform best = null;
            float bestDist = float.MaxValue;
            foreach (var hero in GameObject.FindGameObjectsWithTag("Player"))
            {
                if (hero == null || !hero.activeInHierarchy) continue;
                var hp = hero.GetComponent<HealthComponent>();
                if (hp != null && hp.IsDead) continue;
                if (Flat(hero.transform.position - arenaCenter).magnitude > baseData.aggroRadius) continue;
                float d = Vector3.Distance(transform.position, hero.transform.position);
                if (d < bestDist) { bestDist = d; best = hero.transform; }
            }
            return best;
        }

        /// 勇者たちの中で、center から radius 以内（水平距離。高さは height 以内）にいる者。
        protected static void HeroesNear(Vector3 center, float radius, float height, List<HealthComponent> results)
        {
            results.Clear();
            foreach (var hero in GameObject.FindGameObjectsWithTag("Player"))
            {
                if (hero == null || !hero.activeInHierarchy) continue;
                var hp = hero.GetComponent<HealthComponent>();
                if (hp == null || hp.IsDead) continue;
                Vector3 d = hero.transform.position - center;
                if (Mathf.Abs(d.y) > height || Flat(d).magnitude > radius) continue;
                results.Add(hp);
            }
        }

        /// Atk × scale の物理ダメージ。
        protected void Hit(HealthComponent hero, float scale)
        {
            if (hero == null || hero.IsDead) return;
            hero.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(CurrentAtk * scale)), gameObject, DamageType.Physical);
        }

        protected static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        /// 広場の床の高さ（地面に向けた光線で求める。当たらなければ広場の中心の高さ）。
        protected float FloorY(Vector3 at)
        {
            if (Physics.Raycast(new Vector3(at.x, arenaCenter.y + 60f, at.z), Vector3.down, out var hit, 120f, ~0, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<BossBase>() == null && !hit.collider.CompareTag("Player"))
                return hit.point.y;
            return arenaCenter.y;
        }

        /// 水平に見て、広場の中に収まるよう押し戻した点。
        protected Vector3 ClampToArena(Vector3 p, float margin)
        {
            Vector3 d = Flat(p - arenaCenter);
            float max = Mathf.Max(0f, arenaRadius - margin);
            if (d.magnitude > max) d = d.normalized * max;
            return new Vector3(arenaCenter.x + d.x, p.y, arenaCenter.z + d.z);
        }

        protected void FaceTowards(Vector3 point, float degreesPerSecond)
        {
            Vector3 d = Flat(point - transform.position);
            if (d.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), degreesPerSecond * Time.deltaTime);
        }

        private void OnDied()
        {
            if (dead) return;
            dead = true;
            target = null;
            deathTimer = 0f;
            if (body != null) body.enabled = false;
            OnDeathStart();
            Debug.Log("[Boss] " + DisplayName + " defeated");
        }

        private void Vanish()
        {
            Game.Skill.DeathBurst.Spawn(transform.position + Vector3.up * baseData.bodyCenterHeight, DeathParticles);
            if (spawner != null) spawner.OnBossDefeated(this);
            Destroy(gameObject);
        }

        protected virtual void OnEnable() { All.Add(this); }
        protected virtual void OnDisable() { All.Remove(this); }

        private void OnDestroy()
        {
            if (health != null) health.OnDeath -= OnDied;
        }
    }
}
