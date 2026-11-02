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

        [Header("Spawn Location")]
        [Tooltip("セットすればこのTransformの位置を直接スポーン地点に使う(オフセット無視)。空ならStartTown基準。")]
        [SerializeField] private Transform spawnPoint;
        [Tooltip("StartTownローカル座標でのスポーン位置オフセット。 (0,0,-3) で StartTown のローカル -Z 方向に 3m。")]
        [SerializeField] private Vector3 spawnOffsetLocal = new Vector3(0f, 0f, -3f);
        [Tooltip("スポーン位置の上方から下向きにレイキャストして地面に着地させる。ワールド外への落下を防ぐ。")]
        [SerializeField] private bool snapToGround = true;
        [Tooltip("地面検出レイの上方開始距離(m)。")]
        [SerializeField] private float groundProbeHeight = 30f;
        [Tooltip("地面から何m浮かせて配置するか(CharacterControllerが埋まらない余裕)。")]
        [SerializeField] private float groundClearance = 0.1f;
        [Tooltip("地面検出に使うLayerMask。既定は全レイヤー(~0)。")]
        [SerializeField] private LayerMask groundMask = ~0;
        [Tooltip("Start時にも自動でスポーン位置にテレポートする。初期スポーンをこの設定で揃える。")]
        [SerializeField] private bool teleportOnStart = true;

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
            if (teleportOnStart) TeleportHeroToSpawn();
        }

        /// <summary>
        /// 現在のスポーン位置にヒーローを即座にテレポートする。
        /// CharacterController の状態は保存・復元する。
        /// </summary>
        public void TeleportHeroToSpawn()
        {
            if (hero == null) return;
            // 自分自身のCharacterControllerカプセルに地面レイキャストが当たらないよう、
            // 計測前にCCを切る。位置設定後に元の状態に戻す。
            bool ccWasEnabled = heroController != null && heroController.enabled;
            if (heroController != null) heroController.enabled = false;
            Vector3 pos = ComputeSpawnPosition();
            hero.transform.position = pos;
            if (heroController != null) heroController.enabled = ccWasEnabled;
        }

        /// 勇者の復活地点（Teleport の着地点にも使う）。自分の当たり判定を切ってから呼ぶこと。
        public Vector3 GetSpawnPosition()
        {
            return ComputeSpawnPosition();
        }

        /// <summary>
        /// スポーン地点(spawnPoint or StartTown + spawnOffsetLocal)を計算し、
        /// snapToGround が有効なら下方向レイキャストで地面の上に乗せる。
        /// </summary>
        private Vector3 ComputeSpawnPosition()
        {
            Vector3 basePos;
            if (spawnPoint != null)
            {
                basePos = spawnPoint.position;
            }
            else
            {
                var st = GameObject.FindWithTag(startTownTag);
                if (st == null)
                {
                    Debug.LogWarning("[RespawnManager] StartTown tag not found, using hero current position");
                    return hero != null ? hero.transform.position : Vector3.zero;
                }
                basePos = st.transform.TransformPoint(spawnOffsetLocal);
            }

            if (!snapToGround) return basePos;

            Vector3 castStart = basePos + Vector3.up * groundProbeHeight;
            if (Physics.Raycast(castStart, Vector3.down, out var hit, groundProbeHeight * 2f, groundMask, QueryTriggerInteraction.Ignore))
            {
                return hit.point + Vector3.up * groundClearance;
            }
            Debug.LogWarning("[RespawnManager] Ground raycast missed at " + basePos + " — placing hero at raw spawn position");
            return basePos;
        }

        private void BindHero()
        {
            hero = HumanHeroMarker.Instance != null ? HumanHeroMarker.Instance.gameObject : GameObject.FindWithTag(heroTag);
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

            // スポーン位置を計算してテレポート。地面スナップ付きでワールド外落下を防ぐ。
            hero.transform.position = ComputeSpawnPosition();

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
