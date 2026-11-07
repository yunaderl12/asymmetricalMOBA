using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    /// 試合中に ESC で開くメニュー（勇者でも魔王でも同じ）。開いているあいだも試合は止まらない。
    ///  - ゲームに戻る / 設定（マウス感度・音量）/ 試合から退出（確認のあとタイトルへ戻る）
    /// 開いているあいだは自分のキャラクターの操作を止め、マウスカーソルを出す（BlocksGameInput）。
    /// ESC はほかの画面（コンソール、塔のポップアップ、魔物の選択、塔の俯瞰）が開いていればそちらが使う。
    [DefaultExecutionOrder(-1000)]
    public class InGameMenu : MonoBehaviour
    {
        private enum Page { Closed, Main, Settings, Leave }

        public static InGameMenu Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance.page != Page.Closed;

        /// 自分のキャラクターの操作を止めるか（メニューかコンソールが開いている、またはこのフレームに ESC でメニューを閉じた）。
        public static bool BlocksGameInput => IsOpen || ConsoleUI.IsConsoleActive || (Instance != null && Instance.closedFrame == Time.frameCount);

        [SerializeField] private string titleSceneName = "Title";

        private Page page = Page.Closed;
        private int closedFrame = -1;
        private CursorLockMode savedLock;
        private bool savedVisible;
        private GameObject root, mainPanel, settingsPanel, leavePanel;
        private Text sensitivityValue, volumeValue;

        /// MainStage で1つだけ作る。
        public static void Ensure()
        {
            if (Instance != null) return;
            new GameObject("InGameMenu").AddComponent<InGameMenu>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            var keys = Keyboard.current;
            if (page != Page.Closed && !CanStayOpen()) { Close(); return; }
            if (keys == null || !keys.escapeKey.wasPressedThisFrame) return;
            switch (page)
            {
                case Page.Closed:
                    if (CanOpen()) Open();
                    break;
                case Page.Main: Close(); break;
                default: Show(Page.Main); break;
            }
        }

        private static bool CanStayOpen()
        {
            var mm = MatchManager.Instance;
            return !MatchEndDirector.Playing && (mm == null || mm.CurrentState != MatchManager.MatchState.Finished);
        }

        // ほかの画面が ESC を使うときは開かない。
        private static bool CanOpen()
        {
            if (!CanStayOpen() || ConsoleUI.IsConsoleActive) return false;
            if (SpawnerActionPopup.Instance != null && SpawnerActionPopup.Instance.gameObject.activeSelf) return false;
            if (DemonMonsterPickerUI.AnyOpen) return false;
            foreach (var vs in FindObjectsByType<Game.DemonKing.DemonKingViewSwitcher>(FindObjectsSortMode.None))
                if (vs.CurrentMode == Game.DemonKing.DemonKingViewSwitcher.ViewMode.SpawnerOverhead) return false;
            return true;
        }

        private void Open()
        {
            savedLock = Cursor.lockState;
            savedVisible = Cursor.visible;
            if (root == null) Build();
            root.SetActive(true);
            Show(Page.Main);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Close()
        {
            if (page == Page.Closed) return;
            page = Page.Closed;
            closedFrame = Time.frameCount;
            if (root != null) root.SetActive(false);
            GameSettings.Save();
            if (CanStayOpen())
            {
                Cursor.lockState = savedLock;
                Cursor.visible = savedVisible;
            }
        }

        private void Show(Page p)
        {
            page = p;
            mainPanel.SetActive(true);
            settingsPanel.SetActive(p == Page.Settings);
            leavePanel.SetActive(p == Page.Leave);
        }

        private void LeaveMatch()
        {
            GameSettings.Save();
            Time.timeScale = 1f;
            page = Page.Closed;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Debug.Log("[InGameMenu] leaving the match -> " + titleSceneName);
            SceneManager.LoadScene(titleSceneName);
        }

        // ---------------------------------------------------------------- 見た目

        private void Build()
        {
            if (EventSystem.current == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

            var p = HudStyle.For(HudStyle.CurrentTheme);
            var canvasGo = new GameObject("InGameMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 110;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root = canvasGo;

            var shade = HudStyle.Filled(canvasGo.transform, "Shade");
            var shadeImg = HudStyle.AddGraphic<Image>(shade);
            shadeImg.color = new Color(0f, 0f, 0f, 0.42f);

            // 左のメニュー（布の板に紙の札のボタン）
            mainPanel = HudStyle.Place(canvasGo.transform, "Main", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(90f, 0f), new Vector2(520f, 560f)).gameObject;
            var mp = (RectTransform)mainPanel.transform;
            HudStyle.Felt(mp, p, HudShape.Kind.RoundRect, 26f, 10f, 8f);
            var title = HudStyle.Place(mp, "Title", new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), new Vector2(380f, 84f));
            HudStyle.DropShadow(title, HudShape.Kind.Ribbon, p.Shadow, 0f, 5f);
            var titleFelt = HudStyle.Shape(title, "Felt", HudShape.Kind.Ribbon, p.Ribbon, 0f, HudStyle.FeltTexture);
            titleFelt.notch = 22f;
            HudStyle.Stitch(title, p.Stitch, false, 0f, 8f);
            HudStyle.AddText(HudStyle.Filled(title, "Text"), "メニュー", 40, p.Light, TextAnchor.MiddleCenter, 3f, p.Outline);

            MenuButton(mp, "ゲームに戻る", new Vector2(0f, 120f), p, () => Close());
            MenuButton(mp, "設定", new Vector2(0f, 10f), p, () => Show(page == Page.Settings ? Page.Main : Page.Settings));
            MenuButton(mp, "試合から退出", new Vector2(0f, -100f), p, () => Show(page == Page.Leave ? Page.Main : Page.Leave));
            HudStyle.Label(mp, "Note", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(460f, 40f),
                "メニューを開いているあいだも試合は進みます", 22, new Color(p.Light.r, p.Light.g, p.Light.b, 0.75f), TextAnchor.MiddleCenter);

            settingsPanel = BuildSettings(canvasGo.transform, p);
            leavePanel = BuildLeave(canvasGo.transform, p);
            root.SetActive(false);
        }

        private GameObject BuildSettings(Transform parent, HudStyle.Palette p)
        {
            var rt = HudStyle.Place(parent, "Settings", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(680f, 0f), new Vector2(760f, 470f));
            rt.localRotation = Quaternion.Euler(0f, 0f, -0.6f);
            HudStyle.Paper(rt, p, HudShape.Kind.Torn, 0f, 21, 7f);
            HudStyle.Label(rt, "Title", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(600f, 60f), "設定", 46, p.Ink, TextAnchor.MiddleCenter);
            sensitivityValue = SliderRow(rt, "マウス感度", new Vector2(0f, 60f), GameSettings.MinSensitivity, GameSettings.MaxSensitivity, GameSettings.MouseSensitivity, p,
                v => { GameSettings.MouseSensitivity = v; sensitivityValue.text = "×" + v.ToString("0.0"); });
            sensitivityValue.text = "×" + GameSettings.MouseSensitivity.ToString("0.0");
            volumeValue = SliderRow(rt, "音量", new Vector2(0f, -50f), 0f, 1f, GameSettings.MasterVolume, p,
                v => { GameSettings.MasterVolume = v; volumeValue.text = Mathf.RoundToInt(v * 100f) + "%"; });
            volumeValue.text = Mathf.RoundToInt(GameSettings.MasterVolume * 100f) + "%";
            SmallButton(rt, "戻る", new Vector2(0f, -170f), p.Felt, p, () => Show(Page.Main));
            return rt.gameObject;
        }

        private GameObject BuildLeave(Transform parent, HudStyle.Palette p)
        {
            var rt = HudStyle.Place(parent, "Leave", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(680f, -40f), new Vector2(720f, 330f));
            rt.localRotation = Quaternion.Euler(0f, 0f, 0.7f);
            HudStyle.Paper(rt, p, HudShape.Kind.Torn, 0f, 33, 7f);
            HudStyle.Label(rt, "Question", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(640f, 130f),
                "試合から退出しますか？\nタイトル画面に戻ります", 38, p.Ink, TextAnchor.MiddleCenter);
            SmallButton(rt, "退出する", new Vector2(-150f, -95f), HudStyle.Danger, p, LeaveMatch);
            SmallButton(rt, "やめる", new Vector2(150f, -95f), p.Felt, p, () => Show(Page.Main));
            return rt.gameObject;
        }

        // 紙の札のボタン。カーソルを乗せると右へ少し出る。
        private static void MenuButton(RectTransform parent, string label, Vector2 pos, HudStyle.Palette p, Action onClick)
        {
            var rt = HudStyle.Place(parent, label, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(400f, 86f));
            HudStyle.DropShadow(rt, HudShape.Kind.Torn, p.Shadow, 0f, 5f, label.Length);
            var paper = HudStyle.Shape(rt, "Paper", HudShape.Kind.Torn, p.Paper, 0f, HudStyle.PaperTexture);
            paper.seed = label.Length * 7;
            paper.raycastTarget = true;
            HudStyle.AddText(HudStyle.Filled(rt, "Text"), label, 38, p.Ink, TextAnchor.MiddleCenter);
            AddButton(rt.gameObject, paper, onClick);
        }

        private static void SmallButton(RectTransform parent, string label, Vector2 pos, Color color, HudStyle.Palette p, Action onClick)
        {
            var rt = HudStyle.Place(parent, label, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(250f, 78f));
            HudStyle.DropShadow(rt, HudShape.Kind.Ribbon, p.Shadow, 0f, 5f);
            var felt = HudStyle.Shape(rt, "Felt", HudShape.Kind.Ribbon, color, 0f, HudStyle.FeltTexture);
            felt.notch = 18f;
            felt.raycastTarget = true;
            HudStyle.Stitch(rt, p.Stitch, false, 0f, 7f);
            HudStyle.AddText(HudStyle.Filled(rt, "Text"), label, 32, p.Light, TextAnchor.MiddleCenter, 2.5f, p.Outline);
            AddButton(rt.gameObject, felt, onClick);
        }

        private static void AddButton(GameObject go, Graphic target, Action onClick)
        {
            var b = go.AddComponent<Button>();
            b.targetGraphic = target;
            var c = b.colors;
            c.normalColor = Color.white;
            c.highlightedColor = new Color(1.12f, 1.1f, 1.02f, 1f);
            c.pressedColor = new Color(0.82f, 0.8f, 0.78f, 1f);
            c.selectedColor = Color.white;
            b.colors = c;
            b.onClick.AddListener(() => onClick());
            go.AddComponent<InGameMenuHover>();
        }

        // ラベル・つまみの付いた布の溝・値の表示を並べた1行。値の表示の Text を返す。
        private static Text SliderRow(RectTransform parent, string label, Vector2 pos, float min, float max, float value, HudStyle.Palette p, Action<float> onChange)
        {
            var row = HudStyle.Place(parent, label, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(680f, 70f));
            HudStyle.Label(row, "Label", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(190f, 60f), label, 32, p.Ink, TextAnchor.MiddleLeft);
            var value_ = HudStyle.Label(row, "Value", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-6f, 0f), new Vector2(110f, 60f), "", 32, p.Ink, TextAnchor.MiddleRight);

            var sliderRt = HudStyle.Place(row, "Slider", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(210f, 0f), new Vector2(340f, 40f));
            var track = HudStyle.Shape(sliderRt, "Track", HudShape.Kind.RoundRect, p.Track, 12f, HudStyle.FeltTexture);
            track.rectTransform.offsetMin = new Vector2(0f, 8f);
            track.rectTransform.offsetMax = new Vector2(0f, -8f);
            var fillArea = HudStyle.Node(sliderRt, "Fill Area");
            HudStyle.Stretch(fillArea);
            fillArea.offsetMin = new Vector2(4f, 11f);
            fillArea.offsetMax = new Vector2(-4f, -11f);
            var fill = HudStyle.Shape(fillArea, "Fill", HudShape.Kind.RoundRect, HudStyle.Gold, 8f);
            var handleArea = HudStyle.Node(sliderRt, "Handle Slide Area");
            HudStyle.Stretch(handleArea);
            handleArea.offsetMin = new Vector2(14f, 0f);
            handleArea.offsetMax = new Vector2(-14f, 0f);
            var handle = HudStyle.Node(handleArea, "Handle");
            handle.sizeDelta = new Vector2(38f, 0f);
            handle.anchorMin = new Vector2(0f, 0f);
            handle.anchorMax = new Vector2(0f, 1f);
            HudStyle.DropShadow(handle, HudShape.Kind.Circle, p.Shadow, 0f, 3f);
            var knob = HudStyle.Shape(handle, "Knob", HudShape.Kind.Circle, p.Paper, 0f);
            knob.raycastTarget = true;
            HudStyle.Stitch(handle, p.Ribbon, true, 10f, 6f);

            var slider = sliderRt.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle;
            slider.targetGraphic = knob;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            slider.onValueChanged.AddListener(v => onChange(v));
            track.raycastTarget = true;
            return value_;
        }
    }

    /// カーソルを乗せた札を右へ少しずらす。
    public class InGameMenuHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Vector2 home;
        private bool over;
        private RectTransform rt;

        private void Awake()
        {
            rt = (RectTransform)transform;
            home = rt.anchoredPosition;
        }

        public void OnPointerEnter(PointerEventData e) => over = true;
        public void OnPointerExit(PointerEventData e) => over = false;

        private void OnDisable() => over = false;

        private void Update()
        {
            Vector2 goal = home + (over ? new Vector2(14f, 0f) : Vector2.zero);
            rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, goal, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
            rt.localScale = Vector3.one * Mathf.Lerp(rt.localScale.x, over ? 1.04f : 1f, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
        }
    }
}
