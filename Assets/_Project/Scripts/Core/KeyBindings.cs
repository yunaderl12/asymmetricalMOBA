using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Game.Core
{
    /// プレイヤーが変えられる操作。
    public enum GameKey
    {
        MoveForward, MoveBack, MoveLeft, MoveRight, Jump, Descend, Crouch,
        Attack, Skill, SkillAlt, SkillWheel,
        Slot1, Slot2, Slot3, Slot4, Slot5,
        DemonView,
        // 後ろに足す（PlayerPrefs は名前で持つが、Entries は並び順で引く）。
        DemonAttack,
    }

    /// 操作キーの割り当て（試合中の ESC メニュー → 設定 → 操作キーで変える）。PlayerPrefs に保存して、次の起動でも使う。
    ///  - InputAction を使う操作は Track で登録しておくと、割り当てが変わるたびに上書きされる。
    ///  - キーを直接読む操作は IsPressed / WasPressed を使う。
    /// 同じ側（勇者・魔王）で使うキーが重なったら、相手の操作に元のキーを渡して入れ替える。
    public static class KeyBindings
    {
        [Flags]
        public enum Side { Hero = 1, Demon = 2, Both = 3 }

        public class Entry
        {
            public GameKey Key;
            public string DefaultPath;
            public string Path;
            public string Label;
            public Side Side;
            internal ButtonControl[] Controls;
        }

        private const string PrefPrefix = "Key.";

        // メニューやコンソールが使うので、割り当てには使えないキー。
        private static readonly string[] Reserved =
        {
            "<Keyboard>/escape", "<Keyboard>/slash", "<Keyboard>/tab", "<Keyboard>/enter", "<Keyboard>/numpadEnter", "<Keyboard>/backspace",
        };

        private static readonly List<Entry> entries = new List<Entry>();
        private static readonly List<WeakReference<InputActionAsset>> assets = new List<WeakReference<InputActionAsset>>();
        private static readonly List<KeyValuePair<WeakReference<InputAction>, GameKey>> actions = new List<KeyValuePair<WeakReference<InputAction>, GameKey>>();
        private static bool loaded;
        private static bool controlsDirty = true;

        /// 割り当てが変わったとき（案内の文字を書き直すなど）。
        public static event Action Changed;

        public static IReadOnlyList<Entry> Entries
        {
            get { Load(); return entries; }
        }

        private static void Load()
        {
            if (loaded) return;
            loaded = true;
            entries.Clear();
            Add(GameKey.MoveForward, "<Keyboard>/w", "前へ進む（勇者は素早く2回押して押し続けるとダッシュ）", Side.Both);
            Add(GameKey.MoveBack, "<Keyboard>/s", "後ろへ下がる", Side.Both);
            Add(GameKey.MoveLeft, "<Keyboard>/a", "左へ動く（剣士の高速避けは素早く2回）", Side.Both);
            Add(GameKey.MoveRight, "<Keyboard>/d", "右へ動く（剣士の高速避けは素早く2回）", Side.Both);
            Add(GameKey.Jump, "<Keyboard>/space", "ジャンプ（塔の視点・準備中の見学では上へ）", Side.Both);
            Add(GameKey.Descend, "<Keyboard>/leftCtrl", "下へ（塔の視点・準備中の見学）", Side.Both);
            Add(GameKey.Crouch, "<Keyboard>/v", "しゃがむ／立つ（ダッシュ中はスライディング）", Side.Hero);
            Add(GameKey.Attack, "<Mouse>/leftButton", "通常攻撃", Side.Hero);
            Add(GameKey.Skill, "<Mouse>/rightButton", "選んでいるスキルを使う", Side.Hero);
            Add(GameKey.SkillAlt, "<Keyboard>/e", "選んでいるスキルを使う（予備）", Side.Hero);
            Add(GameKey.SkillWheel, "<Keyboard>/leftShift", "スキルホイール（押している間、マウスで選ぶ）", Side.Hero);
            Add(GameKey.Slot1, "<Keyboard>/1", "スキル1を強化 ／ 魔王はフィールド魔法1", Side.Both);
            Add(GameKey.Slot2, "<Keyboard>/2", "スキル2を強化 ／ 魔王はフィールド魔法2", Side.Both);
            Add(GameKey.Slot3, "<Keyboard>/3", "スキル3を強化 ／ 魔王はフィールド魔法3", Side.Both);
            Add(GameKey.Slot4, "<Keyboard>/4", "スキル4を強化 ／ 魔王はフィールド魔法4", Side.Both);
            Add(GameKey.Slot5, "<Keyboard>/5", "スキル5を強化 ／ 魔王はフィールド魔法5", Side.Both);
            Add(GameKey.DemonView, "<Keyboard>/v", "俯瞰に切り替える（魔王。コアに触れているとき）", Side.Demon);
            Add(GameKey.DemonAttack, "<Mouse>/leftButton", "本体で斬る（フィールド魔法を狙っていないとき）", Side.Demon);
            InputSystem.onDeviceChange += (_, __) => controlsDirty = true;
        }

        private static void Add(GameKey key, string defaultPath, string label, Side side)
        {
            entries.Add(new Entry
            {
                Key = key,
                DefaultPath = defaultPath,
                Path = PlayerPrefs.GetString(PrefPrefix + key, defaultPath),
                Label = label,
                Side = side,
            });
        }

        public static Entry Get(GameKey key)
        {
            Load();
            return entries[(int)key];
        }

        public static string PathOf(GameKey key) => Get(key).Path;

        /// キーの札に出す名前（W、Space、左クリック など）。
        public static string DisplayName(GameKey key) => DisplayName(Get(key).Path);

        public static string DisplayName(string path)
        {
            switch (path)
            {
                case "<Mouse>/leftButton": return "左クリック";
                case "<Mouse>/rightButton": return "右クリック";
                case "<Mouse>/middleButton": return "中クリック";
                case "<Mouse>/forwardButton": return "マウス進む";
                case "<Mouse>/backButton": return "マウス戻る";
                case "<Keyboard>/space": return "Space";
                case "<Keyboard>/leftShift": return "左Shift";
                case "<Keyboard>/rightShift": return "右Shift";
                case "<Keyboard>/leftCtrl": return "左Ctrl";
                case "<Keyboard>/rightCtrl": return "右Ctrl";
                case "<Keyboard>/leftAlt": return "左Alt";
                case "<Keyboard>/rightAlt": return "右Alt";
                case "<Keyboard>/leftMeta": return "左Cmd";
                case "<Keyboard>/rightMeta": return "右Cmd";
                case "<Keyboard>/capsLock": return "Caps";
                case "<Keyboard>/upArrow": return "↑";
                case "<Keyboard>/downArrow": return "↓";
                case "<Keyboard>/leftArrow": return "←";
                case "<Keyboard>/rightArrow": return "→";
            }
            var control = InputSystem.FindControl(path);
            string name = control != null && !string.IsNullOrEmpty(control.displayName) ? control.displayName : path.Substring(path.LastIndexOf('/') + 1);
            return name.Length == 1 ? name.ToUpperInvariant() : name;
        }

        public static bool IsReserved(string path)
        {
            return Array.IndexOf(Reserved, path) >= 0;
        }

        /// 割り当てを変える。使えないキーなら false。同じ側の別の操作が同じキーを使っていたら、その操作に元のキーを渡す。
        public static bool Set(GameKey key, string path)
        {
            Load();
            if (string.IsNullOrEmpty(path) || IsReserved(path)) return false;
            var entry = entries[(int)key];
            if (entry.Path == path) return true;
            foreach (var other in entries)
            {
                if (other == entry || other.Path != path || (other.Side & entry.Side) == 0) continue;
                Store(other, entry.Path);
            }
            Store(entry, path);
            ApplyAll();
            return true;
        }

        public static void ResetAll()
        {
            Load();
            foreach (var entry in entries) Store(entry, entry.DefaultPath);
            ApplyAll();
        }

        private static void Store(Entry entry, string path)
        {
            entry.Path = path;
            if (path == entry.DefaultPath) PlayerPrefs.DeleteKey(PrefPrefix + entry.Key);
            else PlayerPrefs.SetString(PrefPrefix + entry.Key, path);
        }

        // ---- 直接読む ----

        public static bool IsPressed(GameKey key)
        {
            foreach (var control in ControlsOf(key))
                if (control.isPressed) return true;
            return false;
        }

        public static bool WasPressed(GameKey key)
        {
            foreach (var control in ControlsOf(key))
                if (control.wasPressedThisFrame) return true;
            return false;
        }

        private static ButtonControl[] ControlsOf(GameKey key)
        {
            Load();
            if (controlsDirty)
            {
                controlsDirty = false;
                foreach (var e in entries) e.Controls = null;
            }
            var entry = entries[(int)key];
            if (entry.Controls == null)
            {
                var found = new List<ButtonControl>();
                using (var list = InputSystem.FindControls(entry.Path))
                    foreach (var control in list)
                        if (control is ButtonControl button) found.Add(button);
                entry.Controls = found.ToArray();
            }
            return entry.Controls;
        }

        // ---- InputAction への反映 ----

        /// 生成された入力クラス（HeroInputActions / DemonKingInputActions）の asset を登録して、今の割り当てを反映する。
        public static void Track(InputActionAsset asset)
        {
            if (asset == null) return;
            Load();
            assets.Add(new WeakReference<InputActionAsset>(asset));
            Apply(asset);
        }

        /// 1つのキーだけを見る InputAction（最初のバインドを差し替える）。
        public static void Track(InputAction action, GameKey key)
        {
            if (action == null) return;
            Load();
            actions.Add(new KeyValuePair<WeakReference<InputAction>, GameKey>(new WeakReference<InputAction>(action), key));
            action.ApplyBindingOverride(0, PathOf(key));
        }

        private static void ApplyAll()
        {
            controlsDirty = true;
            for (int i = assets.Count - 1; i >= 0; i--)
            {
                if (assets[i].TryGetTarget(out var asset) && asset != null) Apply(asset);
                else assets.RemoveAt(i);
            }
            for (int i = actions.Count - 1; i >= 0; i--)
            {
                if (actions[i].Key.TryGetTarget(out var action)) action.ApplyBindingOverride(0, PathOf(actions[i].Value));
                else actions.RemoveAt(i);
            }
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        private static void Apply(InputActionAsset asset)
        {
            foreach (var map in asset.actionMaps)
            {
                foreach (var action in map.actions)
                {
                    var bindings = action.bindings;
                    for (int i = 0; i < bindings.Count; i++)
                    {
                        if (TryMatch(action.name, bindings[i], out var key))
                            action.ApplyBindingOverride(i, PathOf(key));
                    }
                }
            }
        }

        // 生成された入力クラスのバインド（アクション名と元のキー）から、対応する操作を決める。
        private static bool TryMatch(string actionName, InputBinding binding, out GameKey key)
        {
            key = default;
            if (binding.isComposite) return false;
            switch (actionName)
            {
                case "Move":
                    switch (binding.name)
                    {
                        case "up": key = GameKey.MoveForward; return true;
                        case "down": key = GameKey.MoveBack; return true;
                        case "left": key = GameKey.MoveLeft; return true;
                        case "right": key = GameKey.MoveRight; return true;
                    }
                    return false;
                case "Jump": key = GameKey.Jump; return true;
                case "Attack": key = GameKey.Attack; return true;
                case "SkillCast":
                    key = binding.path == "<Keyboard>/e" ? GameKey.SkillAlt : GameKey.Skill;
                    return true;
                case "SkillWheel": key = GameKey.SkillWheel; return true;
                case "ViewSwitch": key = GameKey.DemonView; return true;
            }
            return false;
        }
    }
}
