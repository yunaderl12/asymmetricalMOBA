using UnityEngine;

namespace Game.Core
{
    public class PrepPhaseManager : MonoBehaviour
    {
        [Header("Demon King")]
        [SerializeField] private GameObject demonKingObject;
        [SerializeField] private MonoBehaviour[] demonKingControlsToEnableInPrep;
        [SerializeField] private Camera demonKingCamera;
        [SerializeField] private AudioListener demonKingAudio;

        [Header("Hero")]
        [SerializeField] private MonoBehaviour[] heroControlsToDisableInPrep;
        [SerializeField] private Camera heroCamera;
        [SerializeField] private AudioListener heroAudio;
        [SerializeField] private MonoBehaviour heroSpectatorScript;

        [Header("HUD")]
        [SerializeField] private GameObject heroHudRoot;
        [SerializeField] private GameObject demonKingHudRoot;
        [SerializeField] private GameObject spawnerListUiRoot;

        private GameSession.PlayerRole effectiveRole;
        private MatchManager.MatchState lastState = MatchManager.MatchState.WaitingForStart;

        private void Start()
        {
            effectiveRole = GameSession.Instance != null ? GameSession.Instance.CurrentRole : GameSession.PlayerRole.Hero;
            if (effectiveRole == GameSession.PlayerRole.None) effectiveRole = GameSession.PlayerRole.Hero;

            if (MatchManager.Instance != null)
            {
                MatchManager.Instance.OnStateChanged += OnStateChanged;
                lastState = MatchManager.Instance.CurrentState;
            }
            ApplyState();
        }

        private void OnDestroy()
        {
            if (MatchManager.Instance != null) MatchManager.Instance.OnStateChanged -= OnStateChanged;
        }

        private void OnStateChanged(MatchManager.MatchState s)
        {
            lastState = s;
            ApplyState();
        }

        public void SwitchRole()
        {
            effectiveRole = effectiveRole == GameSession.PlayerRole.Hero ? GameSession.PlayerRole.DemonKing : GameSession.PlayerRole.Hero;
            Debug.Log("[PrepPhaseManager] effective role switched -> " + effectiveRole);
            ApplyState();
        }

        public GameSession.PlayerRole EffectiveRole => effectiveRole;

        private void ApplyState()
        {
            bool inMatch = lastState == MatchManager.MatchState.InMatch || lastState == MatchManager.MatchState.Finished;
            bool inPrep = lastState == MatchManager.MatchState.DemonPrepPhase;

            // === Hero side ===
            bool heroSideActive = effectiveRole == GameSession.PlayerRole.Hero;
            bool heroControlsOn = heroSideActive && inMatch;
            bool spectatorOn = heroSideActive && inPrep;
            if (heroCamera != null)
            {
                heroCamera.enabled = heroSideActive;
                heroCamera.gameObject.tag = heroSideActive ? "MainCamera" : "Untagged";
            }
            if (heroAudio != null) heroAudio.enabled = heroSideActive;
            if (heroSpectatorScript != null) heroSpectatorScript.enabled = spectatorOn;
            if (heroControlsToDisableInPrep != null)
                foreach (var c in heroControlsToDisableInPrep) if (c != null) c.enabled = heroControlsOn;
            if (heroHudRoot != null) heroHudRoot.SetActive(heroSideActive && inMatch);

            // === Demon King side ===
            bool dkSideActive = effectiveRole == GameSession.PlayerRole.DemonKing;
            bool dkControlsOn = dkSideActive;
            if (demonKingCamera != null)
            {
                demonKingCamera.enabled = dkSideActive;
                demonKingCamera.gameObject.tag = dkSideActive ? "MainCamera" : "Untagged";
            }
            if (demonKingAudio != null) demonKingAudio.enabled = dkSideActive;
            if (demonKingControlsToEnableInPrep != null)
                foreach (var c in demonKingControlsToEnableInPrep) if (c != null) c.enabled = dkControlsOn;
            if (demonKingHudRoot != null) demonKingHudRoot.SetActive(dkSideActive);
            if (spawnerListUiRoot != null) spawnerListUiRoot.SetActive(dkSideActive && inPrep);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Debug.Log("[PrepPhaseManager] ApplyState role=" + effectiveRole + " state=" + lastState);
        }
    }
}
