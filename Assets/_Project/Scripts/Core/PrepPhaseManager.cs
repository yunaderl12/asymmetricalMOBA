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

        private bool inPrep;

        private void Start()
        {
            if (MatchManager.Instance != null)
            {
                MatchManager.Instance.OnStateChanged += OnStateChanged;
                if (MatchManager.Instance.CurrentState == MatchManager.MatchState.DemonPrepPhase) EnterPrep();
                else ExitPrep();
            }
        }

        private void OnDestroy()
        {
            if (MatchManager.Instance != null) MatchManager.Instance.OnStateChanged -= OnStateChanged;
        }

        private void OnStateChanged(MatchManager.MatchState s)
        {
            if (s == MatchManager.MatchState.DemonPrepPhase) EnterPrep();
            else if (inPrep) ExitPrep();
        }

        private void EnterPrep()
        {
            if (inPrep) return;
            inPrep = true;

            // Disable hero controls (spectator stays enabled — its camera detach is harmless while DK camera is active)
            if (heroControlsToDisableInPrep != null)
                foreach (var c in heroControlsToDisableInPrep) if (c != null) c.enabled = false;
            if (heroSpectatorScript != null) heroSpectatorScript.enabled = true;

            // Camera/audio swap
            if (heroCamera != null) { heroCamera.enabled = false; heroCamera.gameObject.tag = "Untagged"; }
            if (heroAudio != null) heroAudio.enabled = false;
            if (demonKingCamera != null) { demonKingCamera.enabled = true; demonKingCamera.gameObject.tag = "MainCamera"; }
            if (demonKingAudio != null) demonKingAudio.enabled = true;

            // Enable demon king controls (so they can move during prep)
            if (demonKingControlsToEnableInPrep != null)
                foreach (var c in demonKingControlsToEnableInPrep) if (c != null) c.enabled = true;

            // HUD swap
            if (heroHudRoot != null) heroHudRoot.SetActive(false);
            if (demonKingHudRoot != null) demonKingHudRoot.SetActive(true);
            if (spawnerListUiRoot != null) spawnerListUiRoot.SetActive(true);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Debug.Log("[PrepPhaseManager] EnterPrep: DemonKing controllable, Hero locked, camera switched.");
        }

        private void ExitPrep()
        {
            inPrep = false;

            // Disable demon king controls again
            if (demonKingControlsToEnableInPrep != null)
                foreach (var c in demonKingControlsToEnableInPrep) if (c != null) c.enabled = false;

            // Camera/audio swap back
            if (demonKingCamera != null) { demonKingCamera.enabled = false; demonKingCamera.gameObject.tag = "Untagged"; }
            if (demonKingAudio != null) demonKingAudio.enabled = false;
            if (heroCamera != null) { heroCamera.enabled = true; heroCamera.gameObject.tag = "MainCamera"; }
            if (heroAudio != null) heroAudio.enabled = true;

            // Re-enable hero controls
            if (heroControlsToDisableInPrep != null)
                foreach (var c in heroControlsToDisableInPrep) if (c != null) c.enabled = true;
            if (heroSpectatorScript != null) heroSpectatorScript.enabled = true;

            // HUD swap back
            if (demonKingHudRoot != null) demonKingHudRoot.SetActive(false);
            if (spawnerListUiRoot != null) spawnerListUiRoot.SetActive(false);
            if (heroHudRoot != null) heroHudRoot.SetActive(true);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Debug.Log("[PrepPhaseManager] ExitPrep: Hero unlocked, DemonKing locked, camera restored.");
        }
    }
}
