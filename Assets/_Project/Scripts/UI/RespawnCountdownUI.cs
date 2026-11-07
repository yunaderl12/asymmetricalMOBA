using UnityEngine;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    public class RespawnCountdownUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text countdownText;

        private bool shifted;

        private void Awake()
        {
            if (root != null) root.SetActive(false);
        }

        // 死んでいるあいだは上から自分の勇者を見るので、真ん中をふさがないように上へずらす。
        private void ShiftUp()
        {
            if (shifted || root == null) return;
            var rt = root.GetComponent<RectTransform>();
            var canvas = root.GetComponentInParent<Canvas>();
            if (rt == null || canvas == null) return;
            float canvasHeight = ((RectTransform)canvas.rootCanvas.transform).rect.height;
            if (canvasHeight <= 0f) return;
            rt.anchoredPosition += new Vector2(0f, canvasHeight * 0.22f);
            shifted = true;
        }

        private void Start()
        {
            HookEvents();
        }

        private void OnDestroy()
        {
            UnhookEvents();
        }

        private void HookEvents()
        {
            var rm = RespawnManager.Instance;
            if (rm == null) return;
            rm.OnRespawnStarted.AddListener(OnStarted);
            rm.OnRespawnCountdown.AddListener(OnCountdown);
            rm.OnRespawnFinished.AddListener(OnFinished);
        }

        private void UnhookEvents()
        {
            var rm = RespawnManager.Instance;
            if (rm == null) return;
            rm.OnRespawnStarted.RemoveListener(OnStarted);
            rm.OnRespawnCountdown.RemoveListener(OnCountdown);
            rm.OnRespawnFinished.RemoveListener(OnFinished);
        }

        private void OnStarted(int seconds)
        {
            if (root != null) root.SetActive(true);
            ShiftUp();
            UpdateText(seconds);
        }

        private void OnCountdown(int remaining)
        {
            UpdateText(remaining);
        }

        private void OnFinished()
        {
            if (root != null) root.SetActive(false);
        }

        private void UpdateText(int seconds)
        {
            if (countdownText == null) return;
            countdownText.text = $"あなたは倒された\nリスポーンまで {seconds} 秒";
        }
    }
}
