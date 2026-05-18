using UnityEngine;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    public class MatchTimerUI : MonoBehaviour
    {
        [SerializeField] private Text timerText;
        [SerializeField] private GameObject prepHintRoot;

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
