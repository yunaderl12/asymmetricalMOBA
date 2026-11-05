using UnityEngine;
using UnityEngine.AI;
using Game.Combat;
using Game.Core;
using Game.Hero;

namespace Game.Monster
{
    /// 橋の前に立つ番人。近づいた勇者を追い、両手を組んで振りかぶり、地面へ叩きつけて岩の槍を前へ走らせる。
    public class BridgeGuardian : MonoBehaviour
    {
        private enum Phase { Idle, Chase, Windup, Down, Recover }

        private const float ThinkInterval = 0.25f;
        private const float PostArriveDistance = 2f;
        private const float TurnDegreesPerSecond = 110f;
        private const float AimLockShare = 0.75f; // 振りかぶりのこの割合までは相手の方へ向き直る
        private const float RaisedHeightShare = 1.15f; // 両手の高さ（体の高さに対する割合）
        private const float RaisedForwardShare = 0.3f; // 真上だと腕がねじれるので、少し前へ上げる
        private const float RaisedLean = -0.55f;
        private const float SlamLean = 1f;
        private const float RecoverHoldShare = 0.45f; // 硬直のうち、手を地面につけたままでいる割合
        private const float AlertLift = 0.4f;

        private Monster monster;
        private MonsterData data;
        private NavMeshAgent agent;
        private HealthComponent health;
        private CharacterModelAnimator model;
        private Vector3 post;
        private Quaternion postFacing;
        private Phase phase = Phase.Idle;
        private Transform target;
        private float thinkTimer;
        private float phaseTimer;
        private float cooldown;
        private Vector3 slamDirection;

        public void Init(Monster owner, Vector3 postPosition, Quaternion facing)
        {
            monster = owner;
            data = owner.Data;
            post = postPosition;
            postFacing = facing;
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<HealthComponent>();
            model = GetComponent<CharacterModelAnimator>();
            FitBody();
        }

        // 体が大きいので、当たり判定と経路の太さ、HPバーの高さを合わせる。触れただけのダメージはない。
        private void FitBody()
        {
            var capsule = GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                capsule.height = data.guardBodyHeight;
                capsule.radius = data.guardBodyRadius;
                capsule.center = Vector3.up * data.guardBodyHeight * 0.5f;
            }
            if (agent != null)
            {
                agent.radius = data.guardBodyRadius;
                agent.height = data.guardBodyHeight;
            }
            foreach (var contact in GetComponentsInChildren<ContactDamage>(true)) contact.gameObject.SetActive(false);
            var bar = GetComponentInChildren<Game.UI.HealthBarUI>(true);
            if (bar != null) bar.transform.localPosition = Vector3.up * (data.guardBodyHeight + AlertLift);
        }

        private void Update()
        {
            if (data == null || agent == null || !agent.isOnNavMesh) return;
            if (health != null && health.IsDead) { ClearPose(); return; }
            if (MatchManager.Instance != null && MatchManager.Instance.CurrentState != MatchManager.MatchState.InMatch) return;
            if (StatusEffectController.IsStunnedObj(gameObject))
            {
                agent.isStopped = true;
                ClearPose();
                phase = Phase.Idle;
                return;
            }
            if (cooldown > 0f) cooldown -= Time.deltaTime;

            switch (phase)
            {
                case Phase.Idle: TickIdle(); break;
                case Phase.Chase: TickChase(); break;
                case Phase.Windup: TickWindup(); break;
                case Phase.Down: TickDown(); break;
                case Phase.Recover: TickRecover(); break;
            }
        }

        private void TickIdle()
        {
            agent.isStopped = false;
            if (!Think()) return;
            target = FindHero();
            if (target != null)
            {
                phase = Phase.Chase;
                AlertMark.Show(transform, data.guardBodyHeight + AlertLift);
                return;
            }
            if (Vector3.Distance(transform.position, post) > PostArriveDistance) agent.SetDestination(post);
            else
            {
                agent.ResetPath();
                transform.rotation = Quaternion.RotateTowards(transform.rotation, postFacing, TurnDegreesPerSecond * ThinkInterval);
            }
        }

        private void TickChase()
        {
            if (Think())
            {
                target = FindHero();
                if (target == null) { phase = Phase.Idle; return; }
                agent.isStopped = false;
                agent.SetDestination(target.position);
            }
            if (target == null) return;
            if (Vector3.Distance(transform.position, target.position) <= data.slamRange && cooldown <= 0f) BeginSlam();
        }

        private void BeginSlam()
        {
            phase = Phase.Windup;
            phaseTimer = 0f;
            agent.isStopped = true;
            agent.ResetPath();
            slamDirection = transform.forward;
        }

        private void TickWindup()
        {
            phaseTimer += Time.deltaTime;
            float k = Mathf.Clamp01(phaseTimer / Mathf.Max(0.01f, data.slamWindupSeconds));
            if (k < AimLockShare && target != null)
            {
                Vector3 flat = target.position - transform.position;
                flat.y = 0f;
                if (flat.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(flat), TurnDegreesPerSecond * Time.deltaTime);
                slamDirection = transform.forward;
            }
            float eased = Mathf.SmoothStep(0f, 1f, k);
            if (model != null) model.SetCastPose(RaisedLean * eased, eased, RaisedPoint());
            if (k >= 1f)
            {
                phase = Phase.Down;
                phaseTimer = 0f;
            }
        }

        private void TickDown()
        {
            phaseTimer += Time.deltaTime;
            float k = Mathf.Clamp01(phaseTimer / Mathf.Max(0.01f, data.slamDownSeconds));
            float eased = k * k;
            if (model != null) model.SetCastPose(Mathf.Lerp(RaisedLean, SlamLean, eased), 1f, Vector3.Lerp(RaisedPoint(), ImpactPoint(), eased));
            if (k < 1f) return;
            RockSpearLine.Spawn(ImpactPoint(), slamDirection, data, gameObject, monster.CurrentAtk);
            phase = Phase.Recover;
            phaseTimer = 0f;
        }

        private void TickRecover()
        {
            phaseTimer += Time.deltaTime;
            float seconds = Mathf.Max(0.01f, data.slamRecoverSeconds);
            float fade = Mathf.InverseLerp(seconds * RecoverHoldShare, seconds, phaseTimer);
            float reach = 1f - fade;
            if (model != null) model.SetCastPose(SlamLean * reach, reach, ImpactPoint());
            if (phaseTimer < seconds) return;
            ClearPose();
            cooldown = data.slamCooldown;
            phase = Phase.Chase;
            thinkTimer = 0f;
        }

        private Vector3 RaisedPoint()
        {
            return transform.position + Vector3.up * data.guardBodyHeight * RaisedHeightShare + transform.forward * data.guardBodyHeight * RaisedForwardShare;
        }

        private Vector3 ImpactPoint()
        {
            return transform.position + slamDirection * data.slamFrontDistance;
        }

        private void ClearPose()
        {
            if (model != null) model.SetCastPose(0f, 0f, Vector3.zero);
        }

        private bool Think()
        {
            thinkTimer -= Time.deltaTime;
            if (thinkTimer > 0f) return false;
            thinkTimer = ThinkInterval;
            return true;
        }

        // 持ち場から離れすぎていない、いちばん近い勇者。
        private Transform FindHero()
        {
            Transform best = null;
            float bestDist = data.guardAggroRadius;
            foreach (var hero in GameObject.FindGameObjectsWithTag("Player"))
            {
                if (hero == null) continue;
                var hp = hero.GetComponent<HealthComponent>();
                if (hp != null && hp.IsDead) continue;
                if (Vector3.Distance(hero.transform.position, post) > data.guardLeashRadius) continue;
                float d = Vector3.Distance(transform.position, hero.transform.position);
                if (d < bestDist) { bestDist = d; best = hero.transform; }
            }
            return best;
        }

        private void OnDestroy()
        {
            ClearPose();
        }
    }
}
