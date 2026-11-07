using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Game.Core;
using Game.Monster;

namespace Game.UI
{
    /// 魔王ロビー。地図の上で山・森・砂漠の魔物と中ボス、橋の番人を置き、フィールド魔法5つを装備する（中身は DemonLoadoutView）。右下にマッチ開始、その左にタイトルへ戻る。
    public class DemonLobby : MonoBehaviour
    {
        [Header("Available Monster Pool")]
        [SerializeField] private List<MonsterData> availableMonsters = new List<MonsterData>();

        [Header("Spawner Initial")]
        [SerializeField] private SpawnerData initialSpawnerData;
        [SerializeField] private int fallbackMaxCost = 2;

        [Header("Bridge Guardians (3)")]
        [SerializeField] private List<MonsterData> availableGuardians = new List<MonsterData>();

        [Header("Scenes")]
        [SerializeField] private string matchSceneName = "MatchLobby";
        [SerializeField] private string titleSceneName = "Title";

        private DemonLoadoutView view;

        private void Awake()
        {
            GameSession.GetOrCreate();
        }

        private void Start()
        {
            var p = HudStyle.DemonPalette;
            var stage = LobbyStyle.CreateScreen("DemonLobbyCanvas", p, out var background);
            LobbyStyle.Glow(stage, 960f, 1140f, 2200f, 700f, new Color(0.66f, 0.2f, 0.18f, 0.4f));
            LobbyStyle.Vignette(background, new Color(0f, 0f, 0f, 0.6f));
            LobbyStyle.ScreenTitle(stage, "魔王ロビー", p);

            int maxCost = initialSpawnerData != null ? initialSpawnerData.maxCost : fallbackMaxCost;
            view = DemonLoadoutView.Create(stage, availableMonsters, availableGuardians, maxCost);

            LobbyStyle.PaperButton(LobbyStyle.At(stage, "BackToTitle", 1100f, 962f, 200f, 60f), p, "← タイトル", 22, BackToTitle);
            var start = LobbyStyle.At(stage, "StartMatch", 1350f, 920f, 530f, 130f);
            LobbyStyle.RibbonButton(start, p, p.Ribbon, "マッチ開始", 52, StartMatch, out _, out _);
            view.transform.SetAsLastSibling(); // 選択肢をボタンより手前に出す
        }

        private void Update()
        {
            if (ConsoleUI.IsConsoleActive || Keyboard.current == null || view == null) return;
            if (Keyboard.current.escapeKey.wasPressedThisFrame && view.PickerOpen) view.ClosePicker();
        }

        private void StartMatch()
        {
            view.SaveToSession();
            SceneManager.LoadScene(matchSceneName);
        }

        private void BackToTitle()
        {
            view.SaveToSession();
            SceneManager.LoadScene(titleSceneName);
        }
    }
}
