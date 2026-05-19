using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    public class DemonLobby : MonoBehaviour
    {
        [SerializeField] private Button startMatchButton;
        [SerializeField] private Button backToTitleButton;
        [SerializeField] private string matchSceneName = "MainStage";
        [SerializeField] private string titleSceneName = "Title";

        private void Awake()
        {
            GameSession.GetOrCreate();
        }

        private void Start()
        {
            if (startMatchButton != null) startMatchButton.onClick.AddListener(OnStartClicked);
            if (backToTitleButton != null) backToTitleButton.onClick.AddListener(OnBackToTitleClicked);
        }

        private void OnStartClicked()
        {
            SceneManager.LoadScene(matchSceneName);
        }

        private void OnBackToTitleClicked()
        {
            SceneManager.LoadScene(titleSceneName);
        }
    }
}
