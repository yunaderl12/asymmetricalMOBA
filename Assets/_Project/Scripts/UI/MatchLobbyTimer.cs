using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public class MatchLobbyTimer : MonoBehaviour
    {
        [SerializeField] private Text timerText;
        [SerializeField] private float duration = 60f;

        private float remaining;
        private bool running;

        public event Action OnExpired;

        public float Remaining => remaining;
        public bool IsRunning => running;

        public void StartCountdown()
        {
            remaining = duration;
            running = true;
            UpdateText();
        }

        public void Stop()
        {
            running = false;
        }

        private void Update()
        {
            if (!running) return;
            remaining -= Time.deltaTime;
            if (remaining <= 0f)
            {
                remaining = 0f;
                running = false;
                UpdateText();
                OnExpired?.Invoke();
                return;
            }
            UpdateText();
        }

        private void UpdateText()
        {
            if (timerText == null) return;
            int s = Mathf.CeilToInt(remaining);
            int mm = s / 60;
            int ss = s % 60;
            timerText.text = "残り " + mm.ToString("00") + ":" + ss.ToString("00");
        }
    }
}
