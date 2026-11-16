using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    /// 操作キーの一覧（試合中の ESC メニュー → 設定 → 操作キー）。2列のリストで、左にキーの札、右にそのキーでできる操作。
    /// 札を押すと「キーを押す…」になり、次に押したキーかマウスのボタンがその操作に割り当たる（Esc でやめる）。
    /// 同じ側で使っているキーを選んだら、相手の操作と入れ替える。割り当ては KeyBindings が保存する。
    public class KeyBindingsPanel : MonoBehaviour
    {
        private const float RowHeight = 56f;
        private const float HeaderHeight = 62f;
        private const float KeyWidth = 230f;
        private static readonly Vector2 PanelSize = new Vector2(1180f, 900f);

        /// キーの入力待ちの間は、ESC をメニューに渡さない。
        public static bool Listening { get; private set; }

        private class Row
        {
            public KeyBindings.Entry Entry;
            public Text KeyText;
            public HudShape Cap;
        }

        private HudStyle.Palette pal;
        private readonly List<Row> rows = new List<Row>();
        private Row listening;
        private int listenFrame;
        private Text status;
        private bool waitingRelease;
        private int swallowUntilFrame = -1;

        public static KeyBindingsPanel Build(Transform parent, HudStyle.Palette p, Action onBack)
        {
            var rt = HudStyle.Place(parent, "Keys", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(650f, 0f), PanelSize);
            var panel = rt.gameObject.AddComponent<KeyBindingsPanel>();
            panel.pal = p;
            panel.BuildContents(rt, onBack);
            return panel;
        }

        private void BuildContents(RectTransform rt, Action onBack)
        {
            var top = new Vector2(0.5f, 1f);
            HudStyle.Paper(rt, pal, HudShape.Kind.Torn, 0f, 47, 7f);
            HudStyle.Label(rt, "Title", top, top, new Vector2(0f, -26f), new Vector2(600f, 56f), "操作キー", 44, pal.Ink, TextAnchor.MiddleCenter);
            HudStyle.Label(rt, "Help", top, top, new Vector2(0f, -84f), new Vector2(1080f, 32f),
                "左の札を押してから新しいキー（マウスのボタンも可）を押すと、割り当てを変えられる。Esc でやめる", 22, HudStyle.Brown, TextAnchor.MiddleCenter);

            // スクロールする一覧。
            var view = HudStyle.Node(rt, "View");
            HudStyle.Stretch(view);
            view.offsetMin = new Vector2(50f, 150f);
            view.offsetMax = new Vector2(-50f, -128f);
            var viewImage = HudStyle.AddGraphic<Image>(view);
            viewImage.color = new Color(0f, 0f, 0f, 0.001f);
            view.gameObject.AddComponent<RectMask2D>();
            var content = HudStyle.Node(view, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = view;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            // 右端の細いスクロールバー（布の溝に紙のつまみ）。
            var bar = HudStyle.Node(rt, "Scrollbar");
            bar.anchorMin = new Vector2(1f, 0f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(1f, 0.5f);
            bar.offsetMin = new Vector2(-42f, 150f);
            bar.offsetMax = new Vector2(-24f, -128f);
            var track = HudStyle.Shape(bar, "Track", HudShape.Kind.RoundRect, pal.Track, 9f, HudStyle.FeltTexture);
            track.raycastTarget = true;
            var slide = HudStyle.Node(bar, "Sliding Area");
            HudStyle.Stretch(slide);
            var handle = HudStyle.Node(slide, "Handle");
            HudStyle.Stretch(handle);
            var knob = HudStyle.Shape(handle, "Knob", HudShape.Kind.RoundRect, pal.Ribbon, 9f, HudStyle.FeltTexture);
            knob.raycastTarget = true;
            var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = knob;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            float y = 0f;
            y = Header(content, "移動（勇者・魔王）", y);
            foreach (var key in new[] { GameKey.MoveForward, GameKey.MoveBack, GameKey.MoveLeft, GameKey.MoveRight, GameKey.Jump, GameKey.Descend })
                y = KeyRow(content, KeyBindings.Get(key), y);
            y = Header(content, "勇者", y);
            foreach (var key in new[] { GameKey.Attack, GameKey.Skill, GameKey.SkillAlt, GameKey.SkillWheel, GameKey.Crouch })
                y = KeyRow(content, KeyBindings.Get(key), y);
            y = Header(content, "スキルの強化（勇者）／フィールド魔法（魔王）", y);
            foreach (var key in new[] { GameKey.Slot1, GameKey.Slot2, GameKey.Slot3, GameKey.Slot4, GameKey.Slot5 })
                y = KeyRow(content, KeyBindings.Get(key), y);
            y = Header(content, "魔王", y);
            y = KeyRow(content, KeyBindings.Get(GameKey.DemonView), y);
            y = KeyRow(content, KeyBindings.Get(GameKey.DemonAttack), y);
            y = FixedRow(content, "左クリック", "狙ったフィールド魔法を発動する", y);
            y = FixedRow(content, "右クリック", "フィールド魔法の狙いをやめる／塔の視点から俯瞰へ戻る", y);
            y = Header(content, "変えられない操作", y);
            y = FixedRow(content, "マウス", "視点を動かす", y);
            y = FixedRow(content, "Esc", "メニューを開く・閉じる", y);
            y = FixedRow(content, "/ ・ Tab", "コンソール（コマンドとチャット）を開く", y);
            y = FixedRow(content, "Backspace", "落下したときにリスポーンする", y);
            content.sizeDelta = new Vector2(0f, -y + 10f);

            var bottom = new Vector2(0.5f, 0f);
            status = HudStyle.Label(rt, "Status", bottom, bottom, new Vector2(0f, 112f), new Vector2(1080f, 30f), "", 22, pal.Ribbon, TextAnchor.MiddleCenter);
            FeltButton(rt, "初期設定に戻す", new Vector2(-170f, 62f), HudStyle.Brown, () =>
            {
                StopListening();
                KeyBindings.ResetAll();
                Refresh();
                status.text = "すべて初期設定に戻しました";
            });
            FeltButton(rt, "戻る", new Vector2(170f, 62f), pal.Felt, () =>
            {
                StopListening();
                onBack();
            });
        }

        private float Header(RectTransform content, string label, float y)
        {
            var topLeft = new Vector2(0f, 1f);
            var row = HudStyle.Place(content, "Header", topLeft, topLeft, new Vector2(0f, y - 12f), new Vector2(520f, 40f));
            var felt = HudStyle.Shape(row, "Felt", HudShape.Kind.Ribbon, pal.Ribbon, 0f, HudStyle.FeltTexture);
            felt.notch = 14f;
            HudStyle.AddText(HudStyle.Filled(row, "Text"), label, 23, Color.white, TextAnchor.MiddleCenter, 2.5f, pal.Outline);
            return y - HeaderHeight;
        }

        private float KeyRow(RectTransform content, KeyBindings.Entry entry, float y)
        {
            var row = new Row { Entry = entry };
            var cap = Cap(content, y, pal.Felt, out row.KeyText, out row.Cap);
            row.Cap.raycastTarget = true;
            var button = cap.gameObject.AddComponent<Button>();
            button.targetGraphic = row.Cap;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.25f, 1.2f, 1.1f, 1f);
            colors.pressedColor = new Color(0.8f, 0.78f, 0.76f, 1f);
            colors.selectedColor = Color.white;
            button.colors = colors;
            button.onClick.AddListener(() => { if (!Swallowing) StartListening(row); });
            Description(content, entry.Label, y);
            rows.Add(row);
            Refresh(row);
            return y - RowHeight;
        }

        private float FixedRow(RectTransform content, string keyName, string label, float y)
        {
            Cap(content, y, HudStyle.DownFelt, out var text, out _);
            text.text = keyName;
            text.color = new Color(1f, 1f, 1f, 0.8f);
            Description(content, label, y);
            return y - RowHeight;
        }

        // キーの札（布の角丸に縫い目、白い文字）。
        private RectTransform Cap(RectTransform content, float y, Color color, out Text text, out HudShape body)
        {
            var topLeft = new Vector2(0f, 1f);
            var cap = HudStyle.Place(content, "Key", topLeft, topLeft, new Vector2(20f, y - 4f), new Vector2(KeyWidth, RowHeight - 8f));
            HudStyle.DropShadow(cap, HudShape.Kind.RoundRect, pal.Shadow, 12f, 4f);
            body = HudStyle.Shape(cap, "Felt", HudShape.Kind.RoundRect, color, 12f, HudStyle.FeltTexture);
            HudStyle.Stitch(cap, pal.Stitch, false, 8f, 5f);
            text = HudStyle.AddText(HudStyle.Filled(cap, "Text"), "", 24, Color.white, TextAnchor.MiddleCenter, 2.5f, pal.Outline);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 14;
            text.resizeTextMaxSize = 24;
            return cap;
        }

        private void Description(RectTransform content, string label, float y)
        {
            var topLeft = new Vector2(0f, 1f);
            var text = HudStyle.Label(content, "Label", topLeft, topLeft, new Vector2(KeyWidth + 50f, y - 4f), new Vector2(790f, RowHeight - 8f), label, 24, pal.Ink, TextAnchor.MiddleLeft);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 16;
            text.resizeTextMaxSize = 24;
        }

        private void FeltButton(RectTransform parent, string label, Vector2 pos, Color color, Action onClick)
        {
            var bottom = new Vector2(0.5f, 0f);
            var rt = HudStyle.Place(parent, label, bottom, new Vector2(0.5f, 0.5f), pos, new Vector2(290f, 70f));
            HudStyle.DropShadow(rt, HudShape.Kind.Ribbon, pal.Shadow, 0f, 5f);
            var felt = HudStyle.Shape(rt, "Felt", HudShape.Kind.Ribbon, color, 0f, HudStyle.FeltTexture);
            felt.notch = 18f;
            felt.raycastTarget = true;
            HudStyle.Stitch(rt, pal.Stitch, false, 0f, 7f);
            HudStyle.AddText(HudStyle.Filled(rt, "Text"), label, 28, pal.Light, TextAnchor.MiddleCenter, 2.5f, pal.Outline);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = felt;
            button.onClick.AddListener(() => { if (!Swallowing) onClick(); });
        }

        // ---------------------------------------------------------------- 割り当ての変更

        // マウスのボタンを割り当てた直後は、そのクリックが下のボタンを押してしまわないようにする。
        private bool Swallowing => swallowUntilFrame >= Time.frameCount;

        private void StartListening(Row row)
        {
            StopListening();
            listening = row;
            listenFrame = Time.frameCount;
            Listening = true;
            row.KeyText.text = "キーを押す…";
            row.Cap.color = pal.Ribbon;
            status.text = "「" + ShortLabel(row.Entry) + "」に使うキーを押してください（Esc でやめる）";
        }

        private void StopListening()
        {
            if (listening == null) return;
            var row = listening;
            listening = null;
            Listening = false;
            Refresh(row);
        }

        private void OnDisable()
        {
            StopListening();
            waitingRelease = false;
            swallowUntilFrame = -1;
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (waitingRelease && (mouse == null || !(mouse.leftButton.isPressed || mouse.rightButton.isPressed || mouse.middleButton.isPressed)))
            {
                waitingRelease = false;
                swallowUntilFrame = Time.frameCount + 1;
            }
            if (listening == null || Time.frameCount <= listenFrame) return;

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    StopListening();
                    status.text = "やめました";
                    return;
                }
                foreach (var key in keyboard.allKeys)
                {
                    if (key == null || !key.wasPressedThisFrame) continue;
                    Assign("<Keyboard>/" + key.name);
                    return;
                }
            }
            if (mouse == null) return;
            string button = mouse.leftButton.wasPressedThisFrame ? "leftButton"
                : mouse.rightButton.wasPressedThisFrame ? "rightButton"
                : mouse.middleButton.wasPressedThisFrame ? "middleButton"
                : mouse.forwardButton.wasPressedThisFrame ? "forwardButton"
                : mouse.backButton.wasPressedThisFrame ? "backButton" : null;
            if (button == null) return;
            waitingRelease = true;
            swallowUntilFrame = int.MaxValue;
            Assign("<Mouse>/" + button);
        }

        private void Assign(string path)
        {
            var row = listening;
            if (KeyBindings.IsReserved(path))
            {
                status.text = KeyBindings.DisplayName(path) + " はメニューやコンソールで使うので割り当てられません。別のキーを押してください";
                return;
            }
            // 入れ替わる相手（同じ側で同じキーを使っている操作）を、変える前に覚えておく。
            string swapped = null;
            foreach (var other in KeyBindings.Entries)
                if (other != row.Entry && other.Path == path && (other.Side & row.Entry.Side) != 0)
                    swapped = (swapped == null ? "" : swapped + "・") + "「" + ShortLabel(other) + "」";
            KeyBindings.Set(row.Entry.Key, path);
            listening = null;
            Listening = false;
            Refresh();
            status.text = "「" + ShortLabel(row.Entry) + "」を " + KeyBindings.DisplayName(path) + " にしました"
                + (swapped != null ? "（" + swapped + "と入れ替え）" : "");
        }

        // 説明の「（」より前だけを短い名前として使う。
        private static string ShortLabel(KeyBindings.Entry entry)
        {
            int cut = entry.Label.IndexOf('（');
            return cut > 0 ? entry.Label.Substring(0, cut) : entry.Label;
        }

        private void Refresh()
        {
            foreach (var row in rows) Refresh(row);
        }

        private void Refresh(Row row)
        {
            row.KeyText.text = KeyBindings.DisplayName(row.Entry.Path);
            // 初期設定から変えたキーは金色の文字にする。
            row.KeyText.color = row.Entry.Path == row.Entry.DefaultPath ? Color.white : HudStyle.Gold;
            row.Cap.color = pal.Felt;
        }
    }
}
