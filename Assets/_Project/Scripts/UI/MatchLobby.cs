using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    public class MatchLobby : MonoBehaviour
    {
        [SerializeField] private GameObject heroPanel;
        [SerializeField] private GameObject demonPanel;
        [SerializeField] private HeroLoadoutPanel heroLoadout;
        [SerializeField] private DemonLoadoutPanel demonLoadout;

        [SerializeField] private MatchLobbyTimer timer;
        [SerializeField] private Button readyButton;
        [SerializeField] private Text readyButtonLabel;
        [SerializeField] private string matchSceneName = "MainStage";

        private bool ready;
        private bool botMatched;

        private void Awake()
        {
            GameSession.GetOrCreate();
        }

        private void Start()
        {
            var role = GameSession.Instance != null ? GameSession.Instance.CurrentRole : GameSession.PlayerRole.None;
            if (heroPanel != null) heroPanel.SetActive(role == GameSession.PlayerRole.Hero);
            if (demonPanel != null) demonPanel.SetActive(role == GameSession.PlayerRole.DemonKing);

            // Auto-spawn bots so single-player has a full match without pressing 0
            if (GameSession.Instance != null)
            {
                var bots = BotMatchmaker.GenerateBots(role);
                GameSession.Instance.CurrentMatchBots = bots;
                botMatched = true;
                Debug.Log($"[MatchLobby] Auto-matched {bots.Count} bots for {role}");
            }
            RefreshActivePanelTeam();
            UpdateReadyLabel();

            if (timer != null)
            {
                timer.OnExpired += OnTimerExpired;
                timer.StartCountdown();
            }
            if (readyButton != null) readyButton.onClick.AddListener(OnReadyClicked);
        }

        private void OnDestroy()
        {
            if (timer != null) timer.OnExpired -= OnTimerExpired;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.digit0Key.wasPressedThisFrame && !botMatched)
            {
                var role = GameSession.Instance != null ? GameSession.Instance.CurrentRole : GameSession.PlayerRole.None;
                var bots = BotMatchmaker.GenerateBots(role);
                if (GameSession.Instance != null) GameSession.Instance.CurrentMatchBots = bots;
                botMatched = true;
                Debug.Log("[BotMatch] " + bots.Count + " bots generated for role " + role);
                RefreshActivePanelTeam();
                UpdateReadyLabel();
            }
        }

        private void RefreshActivePanelTeam()
        {
            if (heroLoadout != null && heroPanel != null && heroPanel.activeSelf) heroLoadout.RefreshTeamDisplay();
            if (demonLoadout != null && demonPanel != null && demonPanel.activeSelf) demonLoadout.RefreshTeamDisplay();
        }

        private void UpdateReadyLabel()
        {
            if (readyButtonLabel == null) return;
            if (ready) { readyButtonLabel.text = "準備完了 ✓"; return; }
            if (botMatched)
            {
                int botCount = GameSession.Instance != null ? GameSession.Instance.CurrentMatchBots.Count : 0;
                int total = botCount; // 4 BOT visible; human +1 implied
                int readyCount = 0;
                if (GameSession.Instance != null) foreach (var b in GameSession.Instance.CurrentMatchBots) if (b.isReady) readyCount++;
                readyButtonLabel.text = "準備完了 (" + readyCount + "/" + total + " Ready)";
            }
            else
            {
                readyButtonLabel.text = "準備完了";
            }
        }

        private void OnReadyClicked()
        {
            if (ready) return;
            ready = true;
            UpdateReadyLabel();
            if (readyButton != null) readyButton.interactable = false;
            SaveAndStart();
        }

        private void OnTimerExpired()
        {
            SaveAndStart();
        }

        private void SaveAndStart()
        {
            if (heroLoadout != null && heroPanel != null && heroPanel.activeSelf) heroLoadout.SaveToSession();
            if (demonLoadout != null && demonPanel != null && demonPanel.activeSelf) demonLoadout.SaveToSession();
            if (GameSession.Instance != null) GameSession.Instance.SkipPrepPhase = true;
            SceneManager.LoadScene(matchSceneName);
        }
    }
}
