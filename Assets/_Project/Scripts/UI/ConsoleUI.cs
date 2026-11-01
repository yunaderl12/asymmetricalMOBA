using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    public class ConsoleUI : MonoBehaviour
    {
        public static ConsoleUI Instance { get; private set; }
        public static bool IsConsoleActive => Instance != null && Instance.IsActive;

        [SerializeField] private GameObject inputPanel;
        [SerializeField] private InputField inputField;
        [SerializeField] private GameObject chatLogPanel;
        [SerializeField] private RectTransform chatLogContent;
        [SerializeField] private int maxEntries = 10;
        [SerializeField] private float fadeStart = 10f;
        [SerializeField] private float removeAfter = 20f;

        public bool IsActive { get; private set; }

        private struct ChatEntry { public float time; public Text uiText; }
        private readonly List<ChatEntry> entries = new List<ChatEntry>();

        private CursorLockMode prevLock;
        private bool prevCursorVisible;
        private string buffer = "";
        private bool textInputSubscribed;
        private int suppressOpenFrame = -1;
        private Graphic chatLogBackground;

        private void Awake()
        {
            Instance = this;
            if (chatLogPanel != null) chatLogBackground = chatLogPanel.GetComponent<Graphic>();
            if (inputPanel != null) inputPanel.SetActive(false);
            if (inputField != null) { inputField.readOnly = true; inputField.interactable = false; }
        }

        private void OnDestroy()
        {
            UnsubscribeText();
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            if (!IsActive)
            {
                if (Time.frameCount == suppressOpenFrame) return;
                if (Keyboard.current.slashKey.wasPressedThisFrame) OpenInput("/");
                else if (Keyboard.current.tabKey.wasPressedThisFrame) OpenInput("");
            }
            else
            {
                if (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.tabKey.wasPressedThisFrame)
                {
                    Close(false);
                }
                else if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)
                {
                    Close(true);
                }
            }

            UpdateChatFade();
            // The log background only shows while typing or while there are messages.
            if (chatLogBackground != null) chatLogBackground.enabled = IsActive || entries.Count > 0;
        }

        private void OpenInput(string prefix)
        {
            IsActive = true;
            buffer = prefix;
            if (inputPanel != null) inputPanel.SetActive(true);
            UpdateFieldText();
            SubscribeText();

            prevLock = Cursor.lockState;
            prevCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Close(bool submit)
        {
            string text = buffer;
            IsActive = false;
            UnsubscribeText();
            buffer = "";
            UpdateFieldText();
            if (inputPanel != null) inputPanel.SetActive(false);
            suppressOpenFrame = Time.frameCount; // prevent the close key (tab) from re-opening

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (!submit) return;
            if (string.IsNullOrEmpty(text)) return;

            if (text.StartsWith("/"))
            {
                var result = DebugCommandRegistry.Execute(text);
                AddChatMessage("[Cmd] " + text);
                if (!string.IsNullOrEmpty(result)) AddChatMessage(result);
            }
            else
            {
                AddChatMessage("[Player] " + text);
            }
        }

        private void SubscribeText()
        {
            if (textInputSubscribed) return;
            if (Keyboard.current == null) return;
            Keyboard.current.onTextInput += OnTextInput;
            textInputSubscribed = true;
        }

        private void UnsubscribeText()
        {
            if (!textInputSubscribed) return;
            if (Keyboard.current != null) Keyboard.current.onTextInput -= OnTextInput;
            textInputSubscribed = false;
        }

        private void OnTextInput(char c)
        {
            if (!IsActive) return;
            if (c == '\n' || c == '\r') return;
            if (c == '\t') return;
            if (c == (char)27) return; // Esc
            if (c == '\b' || c == (char)127) // backspace or DEL
            {
                if (buffer.Length > 0) { buffer = buffer.Substring(0, buffer.Length - 1); UpdateFieldText(); }
                return;
            }
            if (c < 32) return;        // other control chars
            buffer += c;
            UpdateFieldText();
        }

        private void UpdateFieldText()
        {
            if (inputField != null) inputField.text = buffer;
        }

        public static void AddChatMessage(string msg)
        {
            if (Instance != null) Instance.AddChatInternal(msg);
            else Debug.Log("[Chat] " + msg);
        }

        private void AddChatInternal(string msg)
        {
            if (chatLogContent == null) return;
            var lines = msg.Split('\n');
            foreach (var line in lines)
            {
                var go = CreateChatLine(line);
                entries.Add(new ChatEntry { time = Time.time, uiText = go });
            }
            while (entries.Count > maxEntries)
            {
                if (entries[0].uiText != null) Destroy(entries[0].uiText.gameObject);
                entries.RemoveAt(0);
            }
            ReflowChat();
        }

        private Text CreateChatLine(string text)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("Entry", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            go.transform.SetParent(chatLogContent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(0f, 22f);
            var t = go.GetComponent<Text>();
            t.font = font; t.fontSize = 16; t.alignment = TextAnchor.MiddleLeft; t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.text = text;
            var outline = go.GetComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(1f, -1f);
            return t;
        }

        private void ReflowChat()
        {
            float y = 0f;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i].uiText == null) continue;
                var rt = entries[i].uiText.rectTransform;
                rt.anchoredPosition = new Vector2(8f, -y);
                y += rt.sizeDelta.y + 2f;
            }
        }

        private void UpdateChatFade()
        {
            float now = Time.time;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var e = entries[i];
                if (e.uiText == null) { entries.RemoveAt(i); continue; }
                float age = now - e.time;
                if (age > removeAfter) { Destroy(e.uiText.gameObject); entries.RemoveAt(i); ReflowChat(); continue; }
                float a = 1f;
                if (age > fadeStart) a = Mathf.Clamp01(1f - (age - fadeStart) / (removeAfter - fadeStart));
                var c = e.uiText.color; c.a = a; e.uiText.color = c;
                var outline = e.uiText.GetComponent<Outline>();
                if (outline != null) { var oc = outline.effectColor; oc.a = a; outline.effectColor = oc; }
            }
        }
    }
}
