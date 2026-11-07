using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Game.Core;
using Game.Hero;

namespace Game.UI
{
    /// マッチロビー。上に開始までの時計、右下にワールドと準備完了。
    /// 勇者: 左に自分と編成のまとめ、右に勇者チームと魔王。魔王: 魔王ロビーと同じ編成の画面と、下に相手の勇者。
    /// 編成のデータ（ジョブ・スキル・魔物など）はシーンの HeroLoadoutPanel / DemonLoadoutPanel が持っている。
    public class MatchLobby : MonoBehaviour
    {
        [SerializeField] private HeroLoadoutPanel heroLoadout;
        [SerializeField] private DemonLoadoutPanel demonLoadout;
        [Tooltip("開始までの秒数。0になると準備完了を押さなくても始まる。")]
        [SerializeField] private float countdownSeconds = 60f;
        [SerializeField] private string userName = "ユーザー名";

        private GameSession.PlayerRole role;
        private bool ready;
        private bool botMatched;
        private float remaining;
        private bool started;

        private HudStyle.Palette pal;
        private RectTransform stage;
        private Text clock;
        private Text readyLabel;
        private Text readySub;
        private Text worldText;

        // 勇者
        private HeroSkillLoadout heroSkills;
        private HeroShowcase showcase;
        private LoadoutSummary summary;
        private HeroLoadoutEditor editor;
        private RectTransform teamList;

        // 魔王
        private DemonLoadoutView demonView;
        private RectTransform heroStrip;

        private void Awake()
        {
            GameSession.GetOrCreate();
        }

        private void Start()
        {
            role = GameSession.Instance.CurrentRole;
            if (role == GameSession.PlayerRole.None) role = GameSession.PlayerRole.Hero;

            // 1人でも試合ができるように、空いた枠はすぐ BOT で埋める。
            GameSession.Instance.CurrentMatchBots = BotMatchmaker.GenerateBots(role);
            botMatched = true;

            pal = role == GameSession.PlayerRole.DemonKing ? HudStyle.DemonPalette : HudStyle.HeroPalette;
            stage = LobbyStyle.CreateScreen("MatchLobbyCanvas", pal, out var background);
            if (role == GameSession.PlayerRole.DemonKing)
                LobbyStyle.Glow(stage, 960f, 1140f, 2200f, 700f, new Color(0.66f, 0.2f, 0.18f, 0.4f));
            LobbyStyle.Vignette(background, role == GameSession.PlayerRole.DemonKing ? new Color(0f, 0f, 0f, 0.6f) : new Color(0.08f, 0.04f, 0.16f, 0.6f));
            LobbyStyle.ScreenTitle(stage, "マッチロビー", pal, 360f);
            clock = LobbyStyle.Clock(stage, pal, "開始まで");

            if (role == GameSession.PlayerRole.DemonKing) BuildDemon();
            else BuildHero();

            remaining = countdownSeconds;
            RefreshTeam();
            UpdateReadyLabel();
            UpdateClock();
        }

        // ---- 勇者 ----

        private void BuildHero()
        {
            var classes = heroLoadout != null ? heroLoadout.AllClasses : new List<HeroClassData>();
            var common = heroLoadout != null ? heroLoadout.CommonSkills : new List<Game.Skill.SkillData>();
            heroSkills = new HeroSkillLoadout(classes, common);
            heroSkills.LoadFromSession();

            showcase = HeroShowcase.Create(stage, 80f, 140f, 600f, 700f, pal);
            summary = LoadoutSummary.CreateCompact(stage, 60f, 860f, 820f, 180f, pal, () => editor.Show());

            var header = LobbyStyle.At(stage, "TeamTag", 1100f, 136f, 200f, 52f);
            LobbyStyle.Tilt(header, -1.5f);
            HudStyle.Paper(header, pal, HudShape.Kind.RoundRect, 4f, 6, 4f);
            HudStyle.AddText(HudStyle.Filled(header, "Text"), "勇者チーム", 26, pal.Ink, TextAnchor.MiddleCenter);
            teamList = LobbyStyle.At(stage, "Team", 1100f, 204f, 780f, 690f);

            BuildWorldAndReady(1100f, 900f, 330f, 1460f, 420f, 120f);

            editor = HeroLoadoutEditor.Create(stage);
            editor.OnLoadoutChanged = RefreshHero;
            editor.Bind(heroSkills);
            RefreshHero();
        }

        private void RefreshHero()
        {
            showcase.Show(heroSkills.CurrentClass, userName);
            summary.Show(heroSkills);
            RefreshTeam();
        }

        private void BuildPlayerCard(Transform parent, string name, float y, string title, string sub, Sprite face, Color faceColor,
            bool me, bool demon, string state, bool stateOn, bool waiting)
        {
            var cardPal = demon ? HudStyle.DemonPalette : HudStyle.HeroPalette;
            var rt = LobbyStyle.At(parent, name, 0f, y, 780f, 100f);
            var plate = LobbyStyle.At(rt, "Plate", 50f, 4f, 730f, 92f);
            if (me) LobbyStyle.Ring(plate, HudShape.Kind.RoundRect, 16f, 4f);
            var platePal = cardPal;
            if (me) { platePal.Felt = HudStyle.Hex(0x4a3678); platePal.Stitch = HudStyle.Gold; }
            if (waiting) { platePal.Felt = HudStyle.Hex(0x2a1f45); platePal.Stitch = new Color(cardPal.Stitch.r, cardPal.Stitch.g, cardPal.Stitch.b, 0.45f); }
            HudStyle.Felt(plate, platePal, HudShape.Kind.RoundRect, 16f, 6f, waiting ? 0f : 5f);
            LobbyStyle.Text(plate, "Name", 72f, 10f, 440f, 40f, title, 26, Color.white);
            LobbyStyle.Text(plate, "Sub", 72f, 50f, 440f, 30f, sub, 20, cardPal.Light);

            var stamp = LobbyStyle.At(plate, "State", 560f, 24f, 140f, 46f);
            LobbyStyle.Tilt(stamp, -6f);
            var stampColor = stateOn ? (demon ? HudStyle.Hex(0xff8a7a) : HudStyle.Gold) : new Color(cardPal.Light.r, cardPal.Light.g, cardPal.Light.b, waiting ? 0.5f : 0.9f);
            HudStyle.Stitch(stamp, stampColor, false, 12f, 0f).dash = 1000f;
            HudStyle.AddText(HudStyle.Filled(stamp, "Text"), state, 21, stampColor, TextAnchor.MiddleCenter);

            var faceRt = LobbyStyle.At(rt, "Face", 0f, 0f, 100f, 100f);
            if (demon) Title.DemonEmblem(faceRt, cardPal);
            else
            {
                var img = LobbyStyle.Sticker(faceRt, face, faceColor, cardPal);
                if (waiting)
                {
                    img.color = new Color(1f, 1f, 1f, 0.25f);
                    LobbyStyle.Text(faceRt, "Q", 0f, 0f, 100f, 100f, "?", 44, cardPal.Light, TextAnchor.MiddleCenter);
                }
            }
        }

        // ---- 魔王 ----

        private void BuildDemon()
        {
            var monsters = demonLoadout != null ? demonLoadout.AvailableMonsters : new List<Game.Monster.MonsterData>();
            var guardians = demonLoadout != null ? demonLoadout.AvailableGuardians : new List<Game.Monster.MonsterData>();
            int maxCost = demonLoadout != null ? demonLoadout.InitialMaxCost : 2;
            demonView = DemonLoadoutView.Create(stage, monsters, guardians, maxCost);

            heroStrip = LobbyStyle.At(stage, "HeroTeam", 40f, 950f, 1000f, 96f);
            BuildWorldAndReady(1060f, 932f, 260f, 1350f, 530f, 124f);
            demonView.transform.SetAsLastSibling();
        }

        private void BuildHeroStrip(List<BotData> heroes)
        {
            LobbyStyle.Clear(heroStrip);
            var stripPal = pal;
            stripPal.Felt = HudStyle.Hex(0x120a0e);
            LobbyStyle.FeltPlate(heroStrip, stripPal, 16f, 6f, 5f);
            LobbyStyle.Text(heroStrip, "Label", 24f, 0f, 160f, 96f, "相手の勇者", 22, pal.Light);
            for (int i = 0; i < heroes.Count; i++)
            {
                var b = heroes[i];
                var face = LobbyStyle.At(heroStrip, "Face_" + i, 190f + i * 200f, 18f, 60f, 60f);
                LobbyStyle.Sticker(face, b.heroClass != null ? b.heroClass.icon : null, b.heroClass != null ? b.heroClass.avatarColor : pal.Felt, HudStyle.HeroPalette);
                LobbyStyle.Text(heroStrip, "Name_" + i, 258f + i * 200f, 22f, 130f, 26f, LobbyStyle.BotLabel(b.botName), 17, pal.Light);
                LobbyStyle.Text(heroStrip, "Class_" + i, 258f + i * 200f, 48f, 130f, 26f, BotMatchmaker.GetClassDisplay(b.heroClass), 17, HudStyle.Gold);
            }
        }

        // ---- 共通 ----

        private void BuildWorldAndReady(float worldX, float y, float worldW, float readyX, float readyW, float readyH)
        {
            var world = LobbyStyle.At(stage, "WorldButton", worldX, y, worldW, readyH);
            var paper = HudStyle.Paper(world, pal, HudShape.Kind.RoundRect, 8f, 9, 5f);
            LobbyStyle.Text(world, "Caption", 0f, readyH * 0.5f - 46f, worldW, 28f, "ワールド", 19, LobbyStyle.SubInk, TextAnchor.MiddleCenter);
            worldText = LobbyStyle.Text(world, "Name", 0f, readyH * 0.5f - 14f, worldW, 46f, "", 28, pal.Ink, TextAnchor.MiddleCenter);
            LobbyStyle.Text(world, "Prev", 14f, readyH * 0.5f - 14f, 40f, 46f, "＜", 26, pal.Ribbon, TextAnchor.MiddleCenter);
            LobbyStyle.Text(world, "Next", worldW - 54f, readyH * 0.5f - 14f, 40f, 46f, "＞", 26, pal.Ribbon, TextAnchor.MiddleCenter);
            LobbyStyle.MakeButton(world, paper, NextWorld);
            ShowWorld();

            var readyRt = LobbyStyle.At(stage, "ReadyButton", readyX, y - (readyH - 120f) * 0.5f, readyW, readyH);
            LobbyStyle.RibbonButton(readyRt, pal, pal.Ribbon, "準備完了", 48, OnReadyClicked, out readyLabel, out readySub, " ");
        }

        private void NextWorld()
        {
            var session = GameSession.GetOrCreate();
            session.WorldIndex = (session.WorldIndex + 1) % WorldCatalog.All.Length;
            ShowWorld();
        }

        private void ShowWorld()
        {
            int world = GameSession.Instance != null ? GameSession.Instance.WorldIndex : 0;
            worldText.text = WorldCatalog.All[world].label;
        }

        private void RefreshTeam()
        {
            var bots = GameSession.Instance.CurrentMatchBots;
            var heroes = bots.FindAll(b => b.role == GameSession.PlayerRole.Hero);
            var demonKing = bots.Find(b => b.role == GameSession.PlayerRole.DemonKing);

            if (role == GameSession.PlayerRole.DemonKing)
            {
                BuildHeroStrip(heroes);
                return;
            }
            if (teamList == null) return;
            LobbyStyle.Clear(teamList);
            var cls = heroSkills.CurrentClass;
            BuildPlayerCard(teamList, "Me", 0f, userName + "（あなた）", cls != null ? HudStyle.ClassLabel(cls.className) : "",
                cls != null ? cls.icon : null, cls != null ? cls.avatarColor : pal.Felt, true, false, ready ? "準備OK" : "準備中", ready, false);
            for (int i = 0; i < 3; i++)
            {
                float y = 114f * (i + 1);
                if (i < heroes.Count)
                {
                    var b = heroes[i];
                    BuildPlayerCard(teamList, "Hero_" + i, y, LobbyStyle.BotLabel(b.botName), BotMatchmaker.GetClassDisplay(b.heroClass),
                        b.heroClass != null ? b.heroClass.icon : null, b.heroClass != null ? b.heroClass.avatarColor : pal.Felt, false, false,
                        b.isReady ? "準備OK" : "準備中", b.isReady, false);
                }
                else
                {
                    BuildPlayerCard(teamList, "Hero_" + i, y, "マッチング中…", "空きはボットが入る", null, pal.Felt, false, false, "待機", false, true);
                }
            }
            LobbyStyle.Text(teamList, "VS", 330f, 452f, 120f, 70f, "VS", 56, Color.white, TextAnchor.MiddleCenter, 7f, pal.Outline);
            BuildPlayerCard(teamList, "DemonKing", 540f, "魔王", demonKing != null ? LobbyStyle.BotLabel(demonKing.botName) : "マッチング中…",
                null, pal.Felt, false, true, demonKing != null && demonKing.isReady ? "準備OK" : "待機", demonKing != null && demonKing.isReady, false);
        }

        private void UpdateReadyLabel()
        {
            if (readyLabel == null) return;
            var bots = GameSession.Instance.CurrentMatchBots;
            int total = bots.Count + 1;
            int readyCount = ready ? 1 : 0;
            foreach (var b in bots) if (b.isReady) readyCount++;
            readyLabel.text = ready ? "準備完了 ✓" : "準備完了";
            readySub.text = readyCount + " / " + total + " 人";
        }

        private void UpdateClock()
        {
            if (clock != null) clock.text = HudStyle.Clock(remaining);
        }

        private void Update()
        {
            if (started) return;
            remaining -= Time.deltaTime;
            UpdateClock();
            if (remaining <= 0f)
            {
                SaveAndStart();
                return;
            }
            if (ConsoleUI.IsConsoleActive || Keyboard.current == null) return;
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (editor != null && editor.IsOpen) editor.Hide();
                if (demonView != null && demonView.PickerOpen) demonView.ClosePicker();
            }
            if (Keyboard.current.digit0Key.wasPressedThisFrame && !botMatched)
            {
                GameSession.Instance.CurrentMatchBots = BotMatchmaker.GenerateBots(role);
                botMatched = true;
                RefreshTeam();
                UpdateReadyLabel();
            }
        }

        private void OnReadyClicked()
        {
            if (ready) return;
            ready = true;
            UpdateReadyLabel();
            if (role != GameSession.PlayerRole.DemonKing) RefreshTeam();
            SaveAndStart();
        }

        private void SaveAndStart()
        {
            if (started) return;
            started = true;
            if (heroSkills != null) heroSkills.SaveToSession();
            if (demonView != null) demonView.SaveToSession();
            GameSession.Instance.SkipPrepPhase = true;
            int world = GameSession.Instance.WorldIndex;
            SceneManager.LoadScene(WorldCatalog.All[world].sceneName);
        }
    }
}
