using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Combat;

namespace Game.Core
{
    public class MatchManager : MonoBehaviour
    {
        public enum MatchState { WaitingForStart, DemonPrepPhase, InMatch, Finished }
        public enum Winner { None, Heroes, DemonKing, Draw }

        public static MatchManager Instance { get; private set; }

        [Header("Timing")]
        [SerializeField] private float prepPhaseDuration = 60f;
        [SerializeField] private float matchDuration = 1800f;
        [SerializeField] private bool debugSpeedRun = true;

        [Header("Targets")]
        [SerializeField] private string startTownTag = "StartTown";
        [SerializeField] private string demonKingTag = "DemonKing";
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

        private float prepElapsed;
        private HealthComponent startTownHealth;
        private HealthComponent demonKingHealth;
        private readonly List<HealthComponent> spawnerHealths = new List<HealthComponent>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (debugSpeedRun)
            {
                prepPhaseDuration = 5f;
                matchDuration = 180f;
            }
        }

        private void Start()
        {
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
                FinishMatch(Winner.DemonKing);
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
                    startTownHealth.OnDeath += CheckHeroLoss;
                }
            }

            var dk = GameObject.FindWithTag(demonKingTag);
            if (dk != null)
            {
                demonKingHealth = dk.GetComponent<HealthComponent>();
                if (demonKingHealth != null) demonKingHealth.OnDeath += CheckHeroWin;
            }

            foreach (var sg in GameObject.FindGameObjectsWithTag(spawnerTag))
            {
                var hc = sg.GetComponent<HealthComponent>();
                if (hc != null)
                {
                    spawnerHealths.Add(hc);
                    hc.OnDeath += CheckSpawnerWipe;
                }
            }
            Debug.Log($"[MatchManager] Bound. StartTown={(startTownHealth != null)} DK={(demonKingHealth != null)} Spawners={spawnerHealths.Count}");
        }

        private void EnterState(MatchState newState)
        {
            CurrentState = newState;
            if (newState == MatchState.DemonPrepPhase) prepElapsed = 0f;
            if (newState == MatchState.InMatch) ElapsedInMatch = 0f;
            Debug.Log($"[Match] State changed: {newState}");
            OnStateChanged?.Invoke(newState);
        }

        private void CheckHeroLoss()
        {
            if (CurrentState != MatchState.InMatch) return;
            FinishMatch(Winner.DemonKing);
        }

        private void CheckHeroWin()
        {
            if (CurrentState != MatchState.InMatch) return;
            FinishMatch(Winner.Heroes);
        }

        private void CheckSpawnerWipe()
        {
            if (CurrentState != MatchState.InMatch) return;
            foreach (var s in spawnerHealths)
            {
                if (s == null) continue;
                if (!s.IsDead) return;
            }
            FinishMatch(Winner.Heroes);
        }

        private void FinishMatch(Winner w)
        {
            if (CurrentState == MatchState.Finished) return;
            CurrentState = MatchState.Finished;
            MatchWinner = w;
            Debug.Log($"[Match] Finished. Winner={w}");
            OnStateChanged?.Invoke(CurrentState);
            OnMatchFinished?.Invoke(w);
            Time.timeScale = 0f;
        }

        private void OnDestroy()
        {
            if (startTownHealth != null) startTownHealth.OnDeath -= CheckHeroLoss;
            if (demonKingHealth != null) demonKingHealth.OnDeath -= CheckHeroWin;
            foreach (var s in spawnerHealths) if (s != null) s.OnDeath -= CheckSpawnerWipe;
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
        }
    }
}
