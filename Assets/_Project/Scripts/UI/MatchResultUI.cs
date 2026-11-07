using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    public class MatchResultUI : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text winnerText;
        [SerializeField] private Text hintText;

        private MatchManager bound;

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
            if (bound != null)
            {
                bound.OnMatchFinished -= OnFinished;
                bound = null;
            }
            StopAllCoroutines();
        }

        private IEnumerator WaitAndHook()
        {
            while (MatchManager.Instance == null) yield return null;
            bound = MatchManager.Instance;
            bound.OnMatchFinished += OnFinished;
        }

        // 試合終了の演出と勝敗の画面は MatchEndDirector / VictoryScreen が出すので、この古い文字だけの板はもう出さない。
        private void OnFinished(MatchManager.Winner w)
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }
    }
}
