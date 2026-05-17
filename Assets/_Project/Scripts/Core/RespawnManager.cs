using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Game.Combat;
using Game.Hero;

namespace Game.Core
{
    public class RespawnManager : MonoBehaviour
    {
        public static RespawnManager Instance { get; private set; }

        [Header("Target Lookup")]
        [SerializeField] private string heroTag = "Player";
        [SerializeField] private string startTownTag = "StartTown";

        [Header("Timing")]
        [SerializeField] private float baseRespawnSeconds = 10f;
        [SerializeField] private float perDeathExtraSeconds = 5f;

        [Header("Events")]
        public UnityEvent<int> OnRespawnStarted;
        public UnityEvent<int> OnRespawnCountdown;
        public UnityEvent OnRespawnFinished;

        private GameObject hero;
        private HealthComponent heroHealth;
        private CharacterController heroController;
        private HeroFPSController heroFPS;
        private HeroAttack heroAttack;
        private HeroDeathCounter heroDeathCounter;
        private Transform heroBody;
        private bool respawning;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            BindHero();
        }

        private void BindHero()
        {
            hero = GameObject.FindWithTag(heroTag);
            if (hero == null)
            {
                Debug.LogWarning("[RespawnManager] Hero not found by tag '" + heroTag + "'");
                return;
            }
            heroHealth = hero.GetComponent<HealthComponent>();
            heroController = hero.GetComponent<CharacterController>();
            heroFPS = hero.GetComponent<HeroFPSController>();
            heroAttack = hero.GetComponent<HeroAttack>();
            heroDeathCounter = hero.GetComponent<HeroDeathCounter>();
            heroBody = hero.transform.Find("Body");
            if (heroHealth != null) heroHealth.OnDeath += OnHeroDied;
        }

        private void OnDestroy()
        {
            if (heroHealth != null) heroHealth.OnDeath -= OnHeroDied;
            if (Instance == this) Instance = null;
        }

        private void OnHeroDied()
        {
            if (respawning) return;
            int deaths = heroDeathCounter != null ? heroDeathCounter.CurrentDeathCount : 0;
            float wait = baseRespawnSeconds + perDeathExtraSeconds * deaths;
            StartCoroutine(RespawnRoutine(wait));
        }

        private IEnumerator RespawnRoutine(float waitSeconds)
        {
            respawning = true;

            if (heroController != null) heroController.enabled = false;
            if (heroFPS != null) heroFPS.enabled = false;
            if (heroAttack != null) heroAttack.enabled = false;
            if (heroBody != null) heroBody.gameObject.SetActive(false);

            int remaining = Mathf.CeilToInt(waitSeconds);
            OnRespawnStarted?.Invoke(remaining);
            for (int i = remaining; i > 0; i--)
            {
                Debug.Log("Respawning in " + i + " seconds...");
                OnRespawnCountdown?.Invoke(i);
                yield return new WaitForSeconds(1f);
            }

            var startTown = GameObject.FindWithTag(startTownTag);
            if (startTown != null)
            {
                hero.transform.position = startTown.transform.position;
            }
            else
            {
                Debug.LogWarning("[RespawnManager] StartTown tag not found, staying in place");
            }

            if (heroHealth != null) heroHealth.ResetHealth();
            if (heroController != null) heroController.enabled = true;
            if (heroFPS != null) heroFPS.enabled = true;
            if (heroAttack != null) heroAttack.enabled = true;
            if (heroBody != null) heroBody.gameObject.SetActive(true);

            if (heroDeathCounter != null) heroDeathCounter.IncrementDeathCount();

            Debug.Log("Respawned. DeathCount=" + (heroDeathCounter != null ? heroDeathCounter.CurrentDeathCount : -1));
            OnRespawnFinished?.Invoke();
            respawning = false;
        }
    }
}
