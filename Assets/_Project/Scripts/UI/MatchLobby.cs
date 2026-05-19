using UnityEngine;
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

        private void Awake()
        {
            GameSession.GetOrCreate();
        }

        private void Start()
        {
            var role = GameSession.Instance != null ? GameSession.Instance.CurrentRole : GameSession.PlayerRole.None;
            if (heroPanel != null) heroPanel.SetActive(role == GameSession.PlayerRole.Hero);
            if (demonPanel != null) demonPanel.SetActive(role == GameSession.PlayerRole.DemonKing);

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

        private void OnReadyClicked()
        {
            if (ready) return;
            ready = true;
            if (readyButtonLabel != null) readyButtonLabel.text = "準備完了 ✓";
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
