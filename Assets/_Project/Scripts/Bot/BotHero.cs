using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using Game.Combat;
using Game.Core;
using Game.Hero;
using Game.Skill;

namespace Game.Bot
{
    public class BotHero : MonoBehaviour, IDefenseSource
    {
        public enum BotState { Idle, EngagingMonster }

        [SerializeField] private HeroClassData classData;
        private Game.Core.TerrainWalker terrain;
        [SerializeField] private float patrolRadius = 8f;
        [SerializeField] private float engageRadius = 12f;
        [SerializeField] private float attackRange = 3f;
        [SerializeField] private int attackDamage = 10;
        [SerializeField] private float attackInterval = 1.5f;
        [SerializeField] private float respawnSeconds = 10f;
        [SerializeField] private Text nameLabel;
        [SerializeField] private GameObject bodyVisual;
        [SerializeField] private GameObject magicProjectilePrefab;

        private const float BoltMuzzleForward = 0.7f;
        private const float BoltMuzzleRight = 0.3f;
        private const float BoltMuzzleHeight = 1.4f;
        private const float BoltPoseSeconds = 0.35f;
        private const float BoltPoseLean = 0.4f;
        private const float TurnDegreesPerSecond = 360f;
        private const string CastleCoreName = "DemonCastle_Core";

        private NavMeshAgent agent;
        private CharacterModelAnimator modelAnim;
        private HealthComponent health;
        private Vector3 homePosition;
        private float lastAttackTime;
        private Transform engagementTarget;
        private bool dead;
        private BotState state = BotState.Idle;
        private StatModifierContainer mods;
        private BotSkillUser skills;
        private SkillData freeSlotSkill;
        private float baseSpeed;
        private bool retreating;
        private float nextGoalTime;

        public Transform EngagementTarget => engagementTarget;
        public float AttackRange => IsMage ? BalanceConfig.Instance.botMageAttackRange : attackRange;
        private bool IsMage => classData != null && classData.className == "Mage" && magicProjectilePrefab != null;
        public int Df => classData != null ? Mathf.RoundToInt(classData.initialDf * mods.TotalDfMultiplier) : 0;
        public int MDf => classData != null ? classData.initialMDf : 0;

        public void SetFreeSlotSkill(SkillData skill)
        {
            freeSlotSkill = skill;
        }

        public void SetClass(HeroClassData c)
        {
            classData = c;
            if (c != null) Game.Core.TerrainWalker.For(gameObject).SlopePenalty = c.slopeSpeedPenalty;
            if (health != null && c != null) health.SetMaxHP(c.initialHP);
            if (classData != null) attackDamage = Mathf.Max(1, classData.initialAtk);
        }

        public void SetBotName(string n)
        {
            gameObject.name = n;
            if (nameLabel != null) nameLabel.text = n;
        }

        private void Awake()
        {
            gameObject.AddComponent<Game.Core.GroundFollower>();
            terrain = Game.Core.TerrainWalker.For(gameObject);
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<HealthComponent>();
            mods = gameObject.AddComponent<StatModifierContainer>();
            if (agent != null) baseSpeed = agent.speed;
        }

        private void Start()
        {
            if (classData != null && health != null) health.SetMaxHP(classData.initialHP);
            // 人の勇者と同じく、ジョブの Spd で走る（プレハブの NavMeshAgent の速さは使わない）。
            if (classData != null && classData.initialSpd > 0f) baseSpeed = classData.initialSpd;
            if (health != null) health.OnDeath += OnDeath;
            AttachModel();
            if (classData != null)
            {
                skills = gameObject.AddComponent<BotSkillUser>();
                skills.Init(this, classData, freeSlotSkill);
            }
            // スポーン地点（町の地面）を家にする。町の中心はコアの箱の内側で、
            // そこへワープするとコアの上の NavMesh に乗って降りられなくなる。
            homePosition = transform.position;
            InvokeRepeating(nameof(Tick), 0.1f, 0.2f);
        }

        private void AttachModel()
        {
            if (classData == null || string.IsNullOrEmpty(classData.modelPath)) return;
            modelAnim = gameObject.AddComponent<CharacterModelAnimator>();
            modelAnim.Configure(classData.modelPath, classData.modelMaterialPath, classData.modelScale, false, false);
        }

        // The model hides Body itself and plays its own death / respawn, so leave Body alone.
        private bool UsesModel => modelAnim != null && modelAnim.HasModel;

        private void OnDestroy()
        {
            if (health != null) health.OnDeath -= OnDeath;
        }

        // 止まって撃っている間は NavMeshAgent が向きを変えないので、相手の方を向き続ける。
        private void Update()
        {
            if (!IsMage || dead || engagementTarget == null || (skills != null && skills.IsCasting)) return;
            Vector3 flat = engagementTarget.position - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f || flat.magnitude > AttackRange) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(flat), TurnDegreesPerSecond * Time.deltaTime);
        }

        private void Tick()
        {
            if (dead || agent == null || !agent.isOnNavMesh) return;
            agent.speed = baseSpeed * mods.TotalSpdMultiplier * terrain.SpeedFactor;
            if (modelAnim != null) modelAnim.NominalSpeed = baseSpeed;
            HeroMpRecovery.RefillInTown(gameObject, GetComponent<ManaComponent>());
            if (StatusEffectController.IsStunnedObj(gameObject)) { agent.SetDestination(transform.position); return; }
            if (skills != null && skills.IsCasting) return;

            float bestDist = float.MaxValue;
            Transform best = null;
            foreach (var m in GameObject.FindGameObjectsWithTag("Monster"))
            {
                if (m == null) continue;
                float d = Vector3.Distance(transform.position, m.transform.position);
                if (d < bestDist) { bestDist = d; best = m.transform; }
            }
            if (best != null && bestDist <= engageRadius)
            {
                state = BotState.EngagingMonster;
                engagementTarget = best;
            }
            else if (state == BotState.Idle && !retreating && WantsToPush)
            {
                // 近くに魔物がいなければ、届く所にある塔やコアを殴る。
                Transform building = NearestBuilding();
                if (building != null && DistanceTo(building) <= AttackRange)
                {
                    state = BotState.EngagingMonster;
                    engagementTarget = building;
                }
            }

            if (state == BotState.EngagingMonster)
            {
                if (engagementTarget == null) { state = BotState.Idle; nextGoalTime = 0f; return; }
                var hp = engagementTarget.GetComponentInParent<HealthComponent>();
                if (hp == null || hp.IsDead) { engagementTarget = null; state = BotState.Idle; nextGoalTime = 0f; return; }
                float d = DistanceTo(engagementTarget);
                if (d > AttackRange)
                {
                    agent.SetDestination(engagementTarget.position);
                }
                else
                {
                    agent.SetDestination(transform.position);
                    if (Time.time - lastAttackTime >= attackInterval)
                    {
                        if (IsMage) FireMagicBolt(hp);
                        else
                        {
                            hp.TakeDamage(Mathf.RoundToInt(attackDamage * mods.TotalAtkMultiplier), gameObject, DamageType.Physical);
                            HeroMpRecovery.OnBasicHit(gameObject, hp);
                            lastAttackTime = Time.time;
                            if (modelAnim != null) modelAnim.PlayAttack();
                        }
                    }
                }
            }
            else
            {
                TickRoam();
            }
        }

        private static bool WantsToPush => DemonKingLevel.Current >= BalanceConfig.Instance.botPushDkLevel;

        // 敵が近くにいないときの行き先。序盤は魔物を探して狩り、魔王が育ったら塔、塔がなければ魔王城へ。
        private void TickRoam()
        {
            var config = BalanceConfig.Instance;
            if (health != null && health.MaxHP > 0)
            {
                float ratio = (float)health.CurrentHP / health.MaxHP;
                if (ratio < config.botRetreatHpRatio) retreating = true;
                else if (ratio >= config.botReturnHpRatio) retreating = false;
            }
            if (Time.time < nextGoalTime) return;
            nextGoalTime = Time.time + config.botGoalInterval;

            if (retreating) { ReturnHome(); return; }
            Transform goal = WantsToPush ? NearestBuilding() : NearestMonster();
            if (goal != null) agent.SetDestination(goal.position);
            else if (!agent.pathPending && agent.remainingDistance < 1.0f)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f);
                float r = Random.Range(1f, patrolRadius);
                agent.SetDestination(homePosition + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r));
            }
        }

        private Transform NearestMonster()
        {
            Transform best = null;
            float bestDist = float.MaxValue;
            foreach (var m in GameObject.FindGameObjectsWithTag("Monster"))
            {
                if (m.GetComponent<Game.Monster.BridgeGuardian>() != null) continue; // 番人は狩りの相手にしない
                if (m.GetComponent<Game.Monster.BossBase>() != null) continue;      // 広場のボスもわざわざ狩りに行かない
                float d = Vector3.Distance(transform.position, m.transform.position);
                if (d < bestDist) { bestDist = d; best = m.transform; }
            }
            return best;
        }

        // いちばん近い動いている塔。全部壊れていたら魔王城のコア。
        private Transform NearestBuilding()
        {
            Transform best = null;
            float bestDist = float.MaxValue;
            foreach (var s in Game.Monster.Spawner.All)
            {
                if (!s.IsFunctional) continue;
                float d = Vector3.Distance(transform.position, s.transform.position);
                if (d < bestDist) { bestDist = d; best = s.transform; }
            }
            if (best != null) return best;
            var core = GameObject.Find(CastleCoreName);
            return core != null ? core.transform : null;
        }

        // 塔やコアは大きいので、中心ではなく表面までの距離で測る。
        private float DistanceTo(Transform target)
        {
            var col = target.GetComponent<Collider>();
            Vector3 point = col != null && !col.isTrigger ? col.ClosestPoint(transform.position) : target.position;
            return Vector3.Distance(transform.position, point);
        }

        // 人間の魔法使いと同じ弾を、杖のあたりから相手の体の中心へ撃つ。消費MPも同じ。
        private void FireMagicBolt(HealthComponent target)
        {
            var mana = GetComponent<ManaComponent>();
            if (mana != null && !mana.TryUseMana(classData.basicAttackMpCost)) return;
            lastAttackTime = Time.time;

            Vector3 flat = target.transform.position - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(flat);
            Vector3 muzzle = transform.position + transform.forward * BoltMuzzleForward + transform.right * BoltMuzzleRight + Vector3.up * BoltMuzzleHeight;
            Vector3 aim = FireballProjectile.BodyBoundsOf(target.gameObject, null).center;
            Vector3 direction = (aim - muzzle).normalized;

            var go = Instantiate(magicProjectilePrefab, muzzle, Quaternion.LookRotation(direction));
            var bolt = go.GetComponent<MagicProjectile>();
            if (bolt != null) bolt.Init(gameObject, Mathf.Max(1, Mathf.RoundToInt(classData.initialMAtk * mods.TotalAtkMultiplier)), direction);
            if (modelAnim != null) StartCoroutine(PlayBoltPose(muzzle + direction));
        }

        // 殴りのモーションではなく、杖を突き出して放つ姿勢にする。
        private IEnumerator PlayBoltPose(Vector3 holdPoint)
        {
            for (float t = 0f; t < BoltPoseSeconds; t += Time.deltaTime)
            {
                if (skills != null && skills.IsCasting) yield break;
                float k = 1f - t / BoltPoseSeconds;
                modelAnim.SetCastPose(BoltPoseLean * k, k, holdPoint);
                yield return null;
            }
            modelAnim.SetCastPose(0f, 0f, Vector3.zero);
        }

        private void ReturnHome()
        {
            if (agent != null && agent.isOnNavMesh) agent.SetDestination(homePosition);
        }

        private void OnDeath()
        {
            dead = true;
            if (bodyVisual != null && !UsesModel) bodyVisual.SetActive(false);
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            StartCoroutine(RespawnAfter());
        }

        private IEnumerator RespawnAfter()
        {
            yield return new WaitForSeconds(respawnSeconds);
            if (agent != null)
            {
                agent.enabled = false;
                transform.position = homePosition;
                agent.enabled = true;
                if (agent.isOnNavMesh) agent.isStopped = false;
            }
            if (health != null) health.ResetHealth();
            if (bodyVisual != null && !UsesModel) bodyVisual.SetActive(true);
            dead = false;
            state = BotState.Idle;
        }
    }
}
