using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    public class RespawnOverlay : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text countdownText;

        private RespawnManager bound;

        private void Awake()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            StartCoroutine(WaitAndHook());
        }

        private void OnDisable()
        {
            Unhook();
            StopAllCoroutines();
        }

        private IEnumerator WaitAndHook()
        {
            while (RespawnManager.Instance == null) yield return null;
            Hook(RespawnManager.Instance);
        }

        private void Hook(RespawnManager rm)
        {
            if (bound == rm) return;
            Unhook();
            bound = rm;
            bound.OnRespawnStarted.AddListener(OnStarted);
            bound.OnRespawnCountdown.AddListener(OnCountdown);
            bound.OnRespawnFinished.AddListener(OnFinished);
        }

        private void Unhook()
        {
            if (bound == null) return;
            bound.OnRespawnStarted.RemoveListener(OnStarted);
            bound.OnRespawnCountdown.RemoveListener(OnCountdown);
            bound.OnRespawnFinished.RemoveListener(OnFinished);
            bound = null;
        }

        private void OnStarted(int seconds)
        {
            if (panelRoot != null) panelRoot.SetActive(true);
            UpdateText(seconds);
        }

        private void OnCountdown(int remaining)
        {
            UpdateText(remaining);
        }

        private void OnFinished()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void UpdateText(int remaining)
        {
            if (countdownText != null) countdownText.text = "Respawning in " + remaining + " seconds...";
        }
    }
}
