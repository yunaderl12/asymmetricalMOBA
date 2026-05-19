using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    public class Title : MonoBehaviour
    {
        [SerializeField] private Button heroButton;
        [SerializeField] private Button demonButton;
        [SerializeField] private string heroLobbyScene = "HeroLobby";
        [SerializeField] private string demonLobbyScene = "DemonLobby";

        private void Awake()
        {
            GameSession.GetOrCreate();
        }

        private void Start()
        {
            if (heroButton != null) heroButton.onClick.AddListener(OnHeroClicked);
            if (demonButton != null) demonButton.onClick.AddListener(OnDemonClicked);
        }

        private void OnHeroClicked()
        {
            if (GameSession.Instance != null) GameSession.Instance.CurrentRole = GameSession.PlayerRole.Hero;
            SceneManager.LoadScene(heroLobbyScene);
        }

        private void OnDemonClicked()
        {
            if (GameSession.Instance != null) GameSession.Instance.CurrentRole = GameSession.PlayerRole.DemonKing;
            SceneManager.LoadScene(demonLobbyScene);
        }
    }
}
