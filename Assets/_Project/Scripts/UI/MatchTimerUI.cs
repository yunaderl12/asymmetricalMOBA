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

        // Move the timer's Text (with any sibling background it has) onto a dedicated
        // MatchTimerCanvas at sortingOrder 8 so it stays above the BirdsEyeMap (7)
        // but below the spawner action popup (10). Anchor to top-center so the
        // position is consistent for both hero and demon king roles.
        private void EnsureTopCenter()
        {
            if (timerText == null) return;
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
            // Reparent the timer text (preserve its parent background if it has one).
            var timerHost = timerText.transform.parent != null ? timerText.transform.parent.gameObject : timerText.gameObject;
            timerHost.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)timerHost.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(420f, 56f);
            rt.anchoredPosition = new Vector2(0f, -16f);
            // Make sure the text fills the parent if it's a child element.
            var trt = (RectTransform)timerText.transform;
            if (timerHost != timerText.gameObject)
            {
                trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
                trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            }
            timerText.alignment = TextAnchor.MiddleCenter;
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
