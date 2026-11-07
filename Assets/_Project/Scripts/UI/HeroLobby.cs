using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Game.Core;
using Game.Hero;
using Game.Skill;

namespace Game.UI
{
    /// 勇者ロビー。左に自分の勇者、右に「いまの編成」、右下にマッチ開始。編成は「編成」ボタンかカードを押して開くパネルで変える。
    /// 見た目は docs/game-design.md の「ロビーのデザイン」（試合中HUDと同じ布と紙）。
    public class HeroLobby : MonoBehaviour
    {
        [Header("Classes / Skills (Inspector)")]
        [SerializeField] private List<HeroClassData> allClasses = new List<HeroClassData>();
        [SerializeField] private List<SkillData> commonSkills = new List<SkillData>();

        [Header("Scenes")]
        [SerializeField] private string matchSceneName = "MatchLobby";
        [SerializeField] private string titleSceneName = "Title";

        [Header("Player")]
        [SerializeField] private string userName = "ユーザー名";

        private HeroSkillLoadout loadout;
        private HeroShowcase showcase;
        private LoadoutSummary summary;
        private HeroLoadoutEditor editor;
        private Text toast;
        private float toastHideAt;

        private void Awake()
        {
            GameSession.GetOrCreate();
            loadout = new HeroSkillLoadout(allClasses, commonSkills);
        }

        private void Start()
        {
            loadout.LoadFromSession();
            Build();
            Refresh();
        }

        private void Build()
        {
            var p = HudStyle.HeroPalette;
            var stage = LobbyStyle.CreateScreen("HeroLobbyCanvas", p, out var background);
            LobbyStyle.Vignette(background, new Color(0.08f, 0.04f, 0.16f, 0.6f));

            LobbyStyle.ScreenTitle(stage, "勇者ロビー", p);
            LobbyStyle.PaperButton(LobbyStyle.At(stage, "BackToTitle", 40f, 120f, 180f, 50f), p, "← タイトル", 20, BackToTitle);

            showcase = HeroShowcase.Create(stage, 150f, 150f, 740f, 880f, p);

            summary = LoadoutSummary.Create(stage, 1040f, 220f, 840f, 480f, p, OpenEditor);

            // 右上: 編成と設定
            MenuButton(stage, "Btn_Loadout", 1648f, "編成", "剣", p, OpenEditor);
            MenuButton(stage, "Btn_Settings", 1766f, "設定", "設", p, () => ShowToast("設定はまだ作っていません"));
            toast = LobbyStyle.Text(stage, "Toast", 1380f, 168f, 500f, 40f, "", 21, p.Light, TextAnchor.MiddleRight, 4f, p.Outline);

            // 右下: マッチ開始
            LobbyStyle.KeyCap(LobbyStyle.At(stage, "EnterKey", 1290f, 952f, 96f, 46f), p, "Enter");
            var start = LobbyStyle.At(stage, "MatchStart", 1410f, 920f, 470f, 116f);
            LobbyStyle.RibbonButton(start, p, p.Ribbon, "マッチ開始", 52, StartMatch, out _, out _);

            editor = HeroLoadoutEditor.Create(stage);
            editor.OnLoadoutChanged = Refresh;
            editor.OnOpenChanged = open => showcase.SlideBy(open ? -170f : 0f);
            editor.Bind(loadout);
        }

        private static void MenuButton(Transform stage, string name, float x, string label, string glyph, HudStyle.Palette p, System.Action onClick)
        {
            var rt = LobbyStyle.At(stage, name, x, 30f, 96f, 96f);
            var body = HudStyle.Felt(rt, p, HudShape.Kind.Circle, 0f, 6f, 6f);
            HudStyle.AddText(HudStyle.Filled(rt, "Glyph"), glyph, 40, Color.white, TextAnchor.MiddleCenter, 6f, p.Outline);
            LobbyStyle.Text(rt, "Label", -20f, 102f, 136f, 30f, label, 22, p.Light, TextAnchor.MiddleCenter, 4f, p.Outline);
            LobbyStyle.MakeButton(rt, body, onClick);
        }

        private void Refresh()
        {
            showcase.Show(loadout.CurrentClass, userName);
            summary.Show(loadout);
        }

        private void OpenEditor()
        {
            editor.Show();
        }

        private void ShowToast(string message)
        {
            toast.text = message;
            toastHideAt = Time.unscaledTime + 2.5f;
        }

        private void Update()
        {
            if (toast != null && toast.text.Length > 0 && Time.unscaledTime >= toastHideAt) toast.text = "";
            if (ConsoleUI.IsConsoleActive || Keyboard.current == null) return;
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (editor != null && editor.IsOpen) editor.Hide();
            }
            else if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)
            {
                if (editor == null || !editor.IsOpen) StartMatch();
            }
        }

        private void StartMatch()
        {
            loadout.SaveToSession();
            SceneManager.LoadScene(matchSceneName);
        }

        private void BackToTitle()
        {
            loadout.SaveToSession();
            SceneManager.LoadScene(titleSceneName);
        }
    }
}
