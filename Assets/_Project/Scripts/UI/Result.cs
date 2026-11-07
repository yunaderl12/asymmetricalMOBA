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
        [SerializeField] private string lobbySceneName = "Title";

        private void Start()
        {
            Time.timeScale = 1f;

            if (returnToLobbyButton != null)
                returnToLobbyButton.onClick.AddListener(OnReturnClicked);

            // Stats are only recorded for a human hero (StatTracker). Hide the placeholder otherwise.
            var session = GameSession.Instance;
            if (statsText != null && (session == null || session.LastMatchStats == null))
                statsText.gameObject.SetActive(false);
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
            BuildStyledScreen(session);
        }

        // 試合の最後と同じ布と紙の勝敗の画面を、元の文字だけの表示の上にかぶせる。成績は紙の札、戻るボタンは布のリボン。
        private void BuildStyledScreen(GameSession session)
        {
            var winner = session != null ? session.LastWinner : MatchManager.Winner.Draw;
            var cause = session != null ? session.LastEndCause : MatchManager.EndCause.TimeUp;
            var screen = VictoryScreen.Create(winner, cause, 50);
            if (winnerText != null) winnerText.gameObject.SetActive(false);
            if (statsText != null) statsText.gameObject.SetActive(false);
            if (returnToLobbyButton != null) returnToLobbyButton.gameObject.SetActive(false);
            var p = winner == MatchManager.Winner.Heroes ? HudStyle.HeroPalette : HudStyle.DemonPalette;
            screen.Content.anchoredPosition += new Vector2(0f, 150f);

            var stats = session != null ? session.LastMatchStats : null;
            if (stats != null)
            {
                var sheet = HudStyle.Place(screen.transform, "Stats", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(1200f, 170f));
                sheet.localRotation = Quaternion.Euler(0f, 0f, 0.8f);
                HudStyle.Paper(sheet, p, HudShape.Kind.Torn, 0f, 11);
                string[] labels = { "倒した数", "倒された数", "アシスト", "与ダメージ", "被ダメージ", "獲得経験値" };
                int[] values = { stats.kills, stats.deaths, stats.assists, stats.damageDealt, stats.damageTaken, stats.expGained };
                for (int i = 0; i < labels.Length; i++)
                {
                    float x = -380f + (i % 3) * 380f;
                    float y = i < 3 ? 34f : -40f;
                    HudStyle.Label(sheet, "L" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x - 60f, y), new Vector2(200f, 50f),
                        labels[i], 26, p.Ink * 0.8f + new Color(0, 0, 0, 0.2f), TextAnchor.MiddleRight);
                    HudStyle.Label(sheet, "V" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x + 130f, y), new Vector2(160f, 50f),
                        HudStyle.Thousands(values[i]), 36, p.Ink, TextAnchor.MiddleLeft);
                }
            }

            var button = HudStyle.Place(screen.transform, "BackButton", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(520f, 100f));
            HudStyle.DropShadow(button, HudShape.Kind.Ribbon, p.Shadow, 0f, 6f);
            var felt = HudStyle.Shape(button, "Felt", HudShape.Kind.Ribbon, p.Ribbon, 0f, HudStyle.FeltTexture);
            felt.notch = 24f;
            felt.raycastTarget = true;
            HudStyle.Stitch(button, p.Stitch, false, 0f, 9f);
            HudStyle.AddText(HudStyle.Filled(button, "Text"), "タイトルへ戻る", 40, p.Light, TextAnchor.MiddleCenter, 3f, p.Outline);
            var b = button.gameObject.AddComponent<Button>();
            b.targetGraphic = felt;
            var colors = b.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            b.colors = colors;
            b.onClick.AddListener(OnReturnClicked);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
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
