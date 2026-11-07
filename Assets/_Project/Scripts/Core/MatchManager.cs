using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Combat;
using Game.Hero;

namespace Game.Core
{
    public class MatchManager : MonoBehaviour
    {
        public enum MatchState { WaitingForStart, DemonPrepPhase, InMatch, Finished }
        public enum Winner { None, Heroes, DemonKing, Draw }
        /// どうやって決まったか（試合終了の演出と勝敗の画面の文に使う）。
        public enum EndCause { DemonKingDefeated, CastleCoreDestroyed, TownCoreDestroyed, TimeUp }
        public EndCause MatchEndCause { get; private set; }

        public static MatchManager Instance { get; private set; }

        [Header("Timing")]
        [SerializeField] private float prepPhaseDuration = 60f;
        [SerializeField] private float matchDuration = 1800f;
        [SerializeField] private bool debugSpeedRun = true;

        [Header("Targets")]
        [SerializeField] private string startTownTag = "StartTown";
        [SerializeField] private string demonKingTag = "DemonKing";
        [SerializeField] private string demonCastleTag = "DemonCastle";
        [SerializeField] private string spawnerTag = "Spawner";

        public MatchState CurrentState { get; private set; } = MatchState.WaitingForStart;
        public Winner MatchWinner { get; private set; } = Winner.None;
        public float ElapsedInMatch { get; private set; }

        public float RemainingInMatch
        {
            get
            {
                if (CurrentState == MatchState.DemonPrepPhase) return Mathf.Max(0f, prepPhaseDuration - prepElapsed);
                if (CurrentState == MatchState.InMatch) return Mathf.Max(0f, matchDuration - ElapsedInMatch);
                return 0f;
            }
        }

        public event Action<MatchState> OnStateChanged;
        public event Action<Winner> OnMatchFinished;

        public void SkipPrepPhase()
        {
            if (CurrentState != MatchState.DemonPrepPhase) return;
            prepElapsed = prepPhaseDuration;
            Debug.Log("[Match] PrepPhase skipped by user.");
        }

        private float prepElapsed;
        private HealthComponent startTownHealth;
        private HealthComponent demonKingHealth;
        private HealthComponent demonCastleCoreHealth;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (debugSpeedRun)
            {
                prepPhaseDuration = 5f;
                matchDuration = 180f;
            }
            if (GameSession.Instance != null && GameSession.Instance.SkipPrepPhase)
            {
                prepPhaseDuration = 1f;
                GameSession.Instance.SkipPrepPhase = false;
            }
        }

        private void Start()
        {
            Game.UI.InGameMenu.Ensure();
            StartCoroutine(RunMatch());
        }

        private IEnumerator RunMatch()
        {
            // Let other Start() methods (spawners etc) settle one frame
            yield return null;
            BindTargets();

            yield return new WaitForSeconds(0.5f);
            EnterState(MatchState.DemonPrepPhase);
            while (CurrentState == MatchState.DemonPrepPhase && prepElapsed < prepPhaseDuration)
                yield return null;
            if (CurrentState == MatchState.Finished) yield break;

            EnterState(MatchState.InMatch);
            while (CurrentState == MatchState.InMatch && ElapsedInMatch < matchDuration)
            {
                ElapsedInMatch += Time.deltaTime;
                yield return null;
            }
            if (CurrentState == MatchState.InMatch)
            {
                Debug.Log("[Match] Time up - DemonKing wins (provisional)");
                FinishMatch(Winner.DemonKing, EndCause.TimeUp, startTownHealth);
            }
        }

        private void Update()
        {
            if (CurrentState == MatchState.DemonPrepPhase) prepElapsed += Time.deltaTime;
        }

        private void BindTargets()
        {
            var st = GameObject.FindWithTag(startTownTag);
            if (st != null)
            {
                var hc = st.GetComponent<HealthComponent>();
                if (hc == null) hc = st.GetComponentInChildren<HealthComponent>();
                if (hc != null)
                {
                    startTownHealth = hc;
                    startTownHealth.OnDeath += OnTownCoreDestroyed;
                    AttachCoreDefense(hc);
                }
            }

            var dk = GameObject.FindWithTag(demonKingTag);
            if (dk != null)
            {
                demonKingHealth = dk.GetComponent<HealthComponent>();
                if (demonKingHealth != null) demonKingHealth.OnDeath += OnDemonKingDefeated;
            }

            var castle = GameObject.FindWithTag(demonCastleTag);
            if (castle != null)
            {
                demonCastleCoreHealth = castle.GetComponent<HealthComponent>();
                if (demonCastleCoreHealth == null) demonCastleCoreHealth = castle.GetComponentInChildren<HealthComponent>();
                if (demonCastleCoreHealth != null)
                {
                    demonCastleCoreHealth.OnDeath += OnCastleCoreDestroyed;
                    AttachCoreDefense(demonCastleCoreHealth);
                }
            }
            Debug.Log($"[MatchManager] Bound. StartTown={(startTownHealth != null)} DK={(demonKingHealth != null)} DemonCastleCore={(demonCastleCoreHealth != null)}");
        }

        private static void AttachCoreDefense(HealthComponent core)
        {
            var def = core.GetComponent<FixedDefense>();
            if (def == null) def = core.gameObject.AddComponent<FixedDefense>();
            var cfg = BalanceConfig.Instance;
            def.Set(cfg.coreDf, cfg.coreMDf);
        }

        private void EnterState(MatchState newState)
        {
            CurrentState = newState;
            if (newState == MatchState.DemonPrepPhase) prepElapsed = 0f;
            if (newState == MatchState.InMatch) ElapsedInMatch = 0f;
            Debug.Log($"[Match] State changed: {newState}");
            OnStateChanged?.Invoke(newState);
        }

        private void OnTownCoreDestroyed()
        {
            if (CurrentState != MatchState.InMatch) return;
            FinishMatch(Winner.DemonKing, EndCause.TownCoreDestroyed, startTownHealth);
        }

        private void OnDemonKingDefeated()
        {
            if (CurrentState != MatchState.InMatch) return;
            FinishMatch(Winner.Heroes, EndCause.DemonKingDefeated, demonKingHealth);
        }

        private void OnCastleCoreDestroyed()
        {
            if (CurrentState != MatchState.InMatch) return;
            FinishMatch(Winner.Heroes, EndCause.CastleCoreDestroyed, demonCastleCoreHealth);
        }

        [Header("Result Transition")]
        [SerializeField] private string resultSceneName = "Result";
        [SerializeField] private float resultDelaySeconds = 3f;

        private void FinishMatch(Winner w, EndCause cause, HealthComponent focus)
        {
            if (CurrentState == MatchState.Finished) return;
            CurrentState = MatchState.Finished;
            MatchWinner = w;
            MatchEndCause = cause;
            Debug.Log($"[Match] Finished. Winner={w} Cause={cause}");
            OnStateChanged?.Invoke(CurrentState);
            OnMatchFinished?.Invoke(w);

            if (GameSession.Instance != null)
            {
                GameSession.Instance.LastWinner = w;
                GameSession.Instance.LastMatchStats = null; // don't carry over the previous match's stats
                var hero = GameObject.FindWithTag("Player");
                if (hero != null)
                {
                    var st = hero.GetComponent<StatTracker>();
                    if (st != null) GameSession.Instance.LastMatchStats = st.Stats;
                }
            }
            if (GameSession.Instance != null) GameSession.Instance.LastEndCause = cause;
            // 最後の一撃のカットイン → 消滅 → 勝敗の画面のあと、リザルトへ。
            Transform target = focus != null ? focus.transform : null;
            GameObject finisher = focus != null ? focus.LastAttacker : null;
            Game.UI.MatchEndDirector.Play(w, cause, target, finisher, LoadResult);
        }

        private void LoadResult()
        {
            Time.timeScale = 1f;
            if (string.IsNullOrEmpty(resultSceneName)) return;
            SceneManager.LoadScene(resultSceneName);
        }

        private void OnDestroy()
        {
            if (startTownHealth != null) startTownHealth.OnDeath -= OnTownCoreDestroyed;
            if (demonKingHealth != null) demonKingHealth.OnDeath -= OnDemonKingDefeated;
            if (demonCastleCoreHealth != null) demonCastleCoreHealth.OnDeath -= OnCastleCoreDestroyed;
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
        }
    }
}
