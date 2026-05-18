using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    public class Result : MonoBehaviour
    {
        [SerializeField] private Text winnerText;
        [SerializeField] private Text statsText;
        [SerializeField] private Button returnToLobbyButton;
        [SerializeField] private string lobbySceneName = "Lobby";

        private void Start()
        {
            Time.timeScale = 1f;

            if (returnToLobbyButton != null)
                returnToLobbyButton.onClick.AddListener(OnReturnClicked);

            var session = GameSession.Instance;
            if (session != null)
            {
                if (winnerText != null)
                {
                    switch (session.LastWinner)
                    {
                        case MatchManager.Winner.Heroes: winnerText.text = "勝者: 勇者チーム"; break;
                        case MatchManager.Winner.DemonKing: winnerText.text = "勝者: 魔王"; break;
                        case MatchManager.Winner.Draw: winnerText.text = "引き分け"; break;
                        default: winnerText.text = "..."; break;
                    }
                }
                var stats = session.LastMatchStats;
                if (stats != null && statsText != null)
                {
                    statsText.text =
                        "Kills          : " + stats.kills + "\n" +
                        "Deaths         : " + stats.deaths + "\n" +
                        "Assists        : " + stats.assists + "\n" +
                        "Damage Dealt   : " + stats.damageDealt + "\n" +
                        "Damage Taken   : " + stats.damageTaken + "\n" +
                        "Exp Gained     : " + stats.expGained;
                }
            }
        }

        public void SetWinner(string text)
        {
            if (winnerText != null) winnerText.text = text;
        }

        private void OnReturnClicked()
        {
            SceneManager.LoadScene(lobbySceneName);
        }
    }
}
