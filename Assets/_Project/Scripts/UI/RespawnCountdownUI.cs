using UnityEngine;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    public class RespawnCountdownUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text countdownText;

        private void Awake()
        {
            if (root != null) root.SetActive(false);
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
