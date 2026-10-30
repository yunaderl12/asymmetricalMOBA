using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using Game.Combat;
using Game.Hero;

namespace Game.Bot
{
    public class BotHero : MonoBehaviour
    {
        public enum BotState { Idle, EngagingMonster }

        [SerializeField] private HeroClassData classData;
        [SerializeField] private float patrolRadius = 8f;
        [SerializeField] private float engageRadius = 12f;
        [SerializeField] private float attackRange = 3f;
        [SerializeField] private int attackDamage = 10;
        [SerializeField] private float attackInterval = 1.5f;
        [SerializeField] private float respawnSeconds = 10f;
        [SerializeField] private Text nameLabel;
        [SerializeField] private GameObject bodyVisual;

        private NavMeshAgent agent;
        private HealthComponent health;
        private Vector3 homePosition;
        private float lastAttackTime;
        private Transform engagementTarget;
        private bool dead;
        private BotState state = BotState.Idle;

        public void SetClass(HeroClassData c)
        {
            classData = c;
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
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<HealthComponent>();
        }

        private void Start()
        {
            if (classData != null && health != null) health.SetMaxHP(classData.initialHP);
            if (health != null) health.OnDeath += OnDeath;
            var st = GameObject.FindWithTag("StartTown");
            homePosition = st != null ? st.transform.position : transform.position;
            InvokeRepeating(nameof(Tick), 0.1f, 0.2f);
        }

        private void OnDestroy()
        {
            if (health != null) health.OnDeath -= OnDeath;
        }

        private void Tick()
        {
            if (dead || agent == null || !agent.isOnNavMesh) return;

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

            if (state == BotState.EngagingMonster)
            {
                if (engagementTarget == null) { state = BotState.Idle; ReturnHome(); return; }
                var hp = engagementTarget.GetComponentInParent<HealthComponent>();
                if (hp == null || hp.IsDead) { engagementTarget = null; state = BotState.Idle; ReturnHome(); return; }
                float d = Vector3.Distance(transform.position, engagementTarget.position);
                if (d > attackRange)
                {
                    agent.SetDestination(engagementTarget.position);
                }
                else
                {
                    agent.SetDestination(transform.position);
                    if (Time.time - lastAttackTime >= attackInterval)
                    {
                        hp.TakeDamage(attackDamage, gameObject, DamageType.Physical);
                        lastAttackTime = Time.time;
                    }
                }
            }
            else
            {
                if (!agent.pathPending && agent.remainingDistance < 1.0f)
                {
                    float ang = Random.Range(0f, Mathf.PI * 2f);
                    float r = Random.Range(1f, patrolRadius);
                    Vector3 dest = homePosition + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                    agent.SetDestination(dest);
                }
            }
        }

        private void ReturnHome()
        {
            if (agent != null && agent.isOnNavMesh) agent.SetDestination(homePosition);
        }

        private void OnDeath()
        {
            dead = true;
            if (bodyVisual != null) bodyVisual.SetActive(false);
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
            if (bodyVisual != null) bodyVisual.SetActive(true);
            dead = false;
            state = BotState.Idle;
        }
    }
}
