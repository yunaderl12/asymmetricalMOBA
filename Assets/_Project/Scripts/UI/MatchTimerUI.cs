using UnityEngine;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    public class MatchTimerUI : MonoBehaviour
    {
        [SerializeField] private Text timerText;
        [SerializeField] private GameObject prepHintRoot;

        private void Awake()
        {
            EnsureTopCenter();
        }

        // Reparent ONLY the timer Text (which has a RectTransform) to a dedicated
        // MatchTimerCanvas at sortingOrder 8. We deliberately do NOT touch the
        // MatchTimer parent GameObject because in MainStage its component is a
        // plain Transform, not a RectTransform — moving it under a Canvas + casting
        // to RectTransform was throwing, which silently killed this script's Update.
        private void EnsureTopCenter()
        {
            if (timerText == null) return;
            var trt = timerText.GetComponent<RectTransform>();
            if (trt == null) return;

            var canvasGO = GameObject.Find("MatchTimerCanvas");
            Canvas canvas;
            if (canvasGO != null) canvas = canvasGO.GetComponent<Canvas>();
            else
            {
                canvasGO = new GameObject("MatchTimerCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasGO.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 8;
                var scaler = canvasGO.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            trt.SetParent(canvas.transform, false);
            trt.anchorMin = new Vector2(0.5f, 1f);
            trt.anchorMax = new Vector2(0.5f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.sizeDelta = new Vector2(420f, 56f);
            trt.anchoredPosition = new Vector2(0f, -16f);
            timerText.alignment = TextAnchor.MiddleCenter;
            Debug.Log($"[MatchTimerUI] reparented Text to {canvas.name} (sortingOrder={canvas.sortingOrder})");
        }

        private void Update()
        {
            if (timerText == null || MatchManager.Instance == null) return;
            var m = MatchManager.Instance;
            bool inPrep = m.CurrentState == MatchManager.MatchState.DemonPrepPhase;
            if (prepHintRoot != null && prepHintRoot.activeSelf != inPrep) prepHintRoot.SetActive(inPrep);
            switch (m.CurrentState)
            {
                case MatchManager.MatchState.WaitingForStart:
                    timerText.text = "開始準備中...";
                    break;
                case MatchManager.MatchState.DemonPrepPhase:
                    timerText.text = "魔王準備中: 残り " + Format(m.RemainingInMatch);
                    break;
                case MatchManager.MatchState.InMatch:
                    timerText.text = "残り " + Format(m.RemainingInMatch);
                    break;
                case MatchManager.MatchState.Finished:
                    timerText.text = "マッチ終了";
                    break;
            }
        }

        private static string Format(float seconds)
        {
            int s = Mathf.CeilToInt(seconds);
            int mm = s / 60;
            int ss = s % 60;
            return mm.ToString("00") + ":" + ss.ToString("00");
        }
    }
}
